# SharpClaw Metrics

SharpClaw Metrics is the first-party SharpClaw module that owns the
`sharpclaw_metrics` runtime module identity and the `metric` tool prefix. It
provides the host-loadable Metrics module boundary and lifecycle surface.

The package is intended to be loaded by a SharpClaw host as a module payload.
The NuGet package carries the runtime files under `sharpclaw\`, including
`package.json`, `SharpClaw.Modules.Metrics.dll`, the dependency file, and the
dependency assemblies needed by the sidecar module. The manifest keeps the
public module type as `SharpClaw.Modules.Metrics.MetricsModule`, defaults the
module to enabled, and declares sidecar .NET runtime loading.

The module has no tools or provider registrations. Its runtime behavior is
intentionally limited to the public module identity and the lifecycle contract
supplied by `SharpClaw.Contracts`. Execution-specific metric behavior belongs
to the host repository and is not compiled into this package.
