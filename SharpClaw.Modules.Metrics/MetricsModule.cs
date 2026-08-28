using SharpClaw.Contracts.Modules;

namespace SharpClaw.Modules.Metrics;

/// <summary>First-party SharpClaw metrics module identity and lifecycle implementation.</summary>
public sealed class MetricsModule : ISharpClawModule
{
    public ModuleIdentity Identity { get; } = new(
        "sharpclaw_metrics",
        "Metrics",
        "metric");

    public void Configure(ISharpClawModuleBuilder module)
    {
        ArgumentNullException.ThrowIfNull(module);
    }

    public ValueTask StartAsync(ModuleStartContext context, CancellationToken ct)
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
