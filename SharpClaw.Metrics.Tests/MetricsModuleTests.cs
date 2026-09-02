using System.Text.Json;

using SharpClaw.Contracts.Modules;
using SharpClaw.ModuleSDK;
using SharpClaw.Modules.Metrics;

namespace SharpClaw.Metrics.Tests;

public sealed class MetricsModuleTests
{
    [Test]
    public void ModuleIdentityMatchesPublicManifest()
    {
        var module = new MetricsModule();
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "module.json")));
        var root = document.RootElement;

        Assert.That(module.Identity.Id, Is.EqualTo("sharpclaw_metrics"));
        Assert.That(module.Identity.DisplayName, Is.EqualTo("Metrics"));
        Assert.That(module.Identity.ToolPrefix, Is.EqualTo("metric"));
        Assert.That(root.GetProperty("id").GetString(), Is.EqualTo(module.Identity.Id));
        Assert.That(root.GetProperty("version").GetString(), Is.EqualTo("0.5.0-beta.4"));
        Assert.That(root.GetProperty("entryAssembly").GetString(), Is.EqualTo("SharpClaw.Modules.Metrics.dll"));
        Assert.That(root.GetProperty("moduleType").GetString(), Is.EqualTo(typeof(MetricsModule).FullName));
        Assert.That(root.GetProperty("runtime").GetString(), Is.EqualTo(ModuleManifestRuntimeInfo.DotNet));
        Assert.That(root.GetProperty("hostMode").GetString(), Is.EqualTo(ModuleManifestRuntimeInfo.HostModeSidecar));
        Assert.That(root.GetProperty("defaultEnabled").GetBoolean(), Is.True);
    }

    [Test]
    public void ModuleCompilerBuildsEmptyOutOfProcessContributionGraph()
    {
        var module = new MetricsModule();
        var manifest = JsonSerializer.Deserialize<ModuleManifest>(
            File.ReadAllText(Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "module.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var graph = SharpClawModuleCompiler.Compile(
            module,
            manifest,
            new ModuleCompilationOptions
            {
                HostingMode = ModuleHostingMode.OutOfProcess,
            });

        Assert.That(graph.Identity, Is.EqualTo(module.Identity));
        Assert.That(graph.HostingMode, Is.EqualTo(ModuleHostingMode.OutOfProcess));
        Assert.That(graph.Services, Is.Empty);
        Assert.That(graph.Contracts, Is.Empty);
        Assert.That(graph.Storage, Is.Empty);
        Assert.That(graph.Actions, Is.Empty);
        Assert.That(graph.Events, Is.Empty);
        Assert.That(graph.ActionHooks, Is.Empty);
        Assert.That(graph.EventHooks, Is.Empty);
        Assert.That(graph.Tools, Is.Empty);
        Assert.That(graph.ActionEntries, Is.Empty);
        Assert.That(graph.Chat.ContextContributors, Is.Empty);
        Assert.That(graph.Application.IsEmpty, Is.True);
    }

    [Test]
    public async Task LifecycleHonorsCancellationWithoutAddingContributions()
    {
        var module = new MetricsModule();
        var context = new ModuleStartContext(
            module.Identity,
            "0.5.0-beta.37",
            "metrics-test-contract",
            ExtensionFeatureSet.Empty);

        await module.StartAsync(context, CancellationToken.None);
        await module.StopAsync(CancellationToken.None);

        using var startCancellation = new CancellationTokenSource();
        startCancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await module.StartAsync(context, startCancellation.Token));

        using var stopCancellation = new CancellationTokenSource();
        stopCancellation.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await module.StopAsync(stopCancellation.Token));
    }
}
