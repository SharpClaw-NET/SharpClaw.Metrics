using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Contracts.Kernel;
using SharpClaw.ModuleSDK;

namespace SharpClaw.Modules.Metrics;

/// <summary>First-party SharpClaw metrics module identity and lifecycle implementation.</summary>
public sealed class MetricsModule : ISharpClawModule
{
    public ModuleIdentity Identity { get; } = new(
        "sharpclaw_metrics",
        "Metrics",
        "metric");

    public void ConfigureServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
    }

    public ValueTask StartAsync(ServiceStartContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
