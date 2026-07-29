using System.Text.Json;

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

        Assert.That(module.Id, Is.EqualTo("sharpclaw_metrics"));
        Assert.That(module.DisplayName, Is.EqualTo("Metrics"));
        Assert.That(module.ToolPrefix, Is.EqualTo("metric"));
        Assert.That(module.GetToolDefinitions(), Is.Empty);
        Assert.That(root.GetProperty("id").GetString(), Is.EqualTo(module.Id));
        Assert.That(root.GetProperty("version").GetString(), Is.EqualTo("0.1.1-beta.1"));
        Assert.That(root.GetProperty("entryAssembly").GetString(), Is.EqualTo("SharpClaw.Modules.Metrics.dll"));
        Assert.That(root.GetProperty("moduleType").GetString(), Is.EqualTo(typeof(MetricsModule).FullName));
        Assert.That(root.GetProperty("defaultEnabled").GetBoolean(), Is.True);
    }

    [Test]
    public void ServiceConfigurationRegistersNoServices()
    {
        var module = new MetricsModule();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        module.ConfigureServices(services);
        Assert.That(services, Is.Empty);
    }
}
