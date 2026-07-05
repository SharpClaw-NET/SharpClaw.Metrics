using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using SharpClaw.Contracts.Modules;
using SharpClaw.Contracts.Tasks;
using SharpClaw.Modules.Metrics;

namespace SharpClaw.Metrics.Tests;

public sealed class MetricsModuleTests
{
    [Test]
    public void ModuleIdentityMatchesPublicManifest()
    {
        var module = new MetricsModule();
        using var manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "module.json")));
        var root = manifest.RootElement;

        Assert.That(module.Id, Is.EqualTo("sharpclaw_metrics"));
        Assert.That(module.DisplayName, Is.EqualTo("Metrics"));
        Assert.That(module.ToolPrefix, Is.EqualTo("metric"));
        Assert.That(module.GetToolDefinitions(), Is.Empty);

        Assert.That(root.GetProperty("id").GetString(), Is.EqualTo(module.Id));
        Assert.That(root.GetProperty("version").GetString(), Is.EqualTo("0.1.1-beta"));
        Assert.That(root.GetProperty("toolPrefix").GetString(), Is.EqualTo(module.ToolPrefix));
        Assert.That(root.GetProperty("entryAssembly").GetString(), Is.EqualTo("SharpClaw.Modules.Metrics.dll"));
        Assert.That(root.GetProperty("moduleType").GetString(), Is.EqualTo(typeof(MetricsModule).FullName));
        Assert.That(root.GetProperty("hostMode").GetString(), Is.EqualTo("sidecar"));
        Assert.That(root.GetProperty("enabled").GetBoolean(), Is.True);
        Assert.That(root.GetProperty("defaultEnabled").GetBoolean(), Is.True);
    }

    [Test]
    public void ConfigureServicesRegistersMetricProvidersAndTriggerSource()
    {
        var services = new ServiceCollection();
        var module = new MetricsModule();

        module.ConfigureServices(services);

        Assert.That(
            services.Where(service => service.ServiceType == typeof(ITaskMetricProvider))
                .Select(service => service.ImplementationType),
            Is.EquivalentTo(new[]
            {
                typeof(PendingJobCountMetricProvider),
                typeof(PendingTaskCountMetricProvider),
                typeof(SchedulerPendingJobCountMetricProvider),
            }));

        Assert.That(
            services.Single(service => service.ServiceType == typeof(ITaskTriggerSource)).ImplementationType,
            Is.EqualTo(typeof(MetricTriggerSource)));
    }

    [Test]
    public async Task BuiltInProvidersForwardToHostQueueMetrics()
    {
        var host = new RecordingHostQueueMetrics();

        Assert.That(await new PendingJobCountMetricProvider(host).GetValueAsync(CancellationToken.None),
            Is.EqualTo(11));
        Assert.That(await new PendingTaskCountMetricProvider(host).GetValueAsync(CancellationToken.None),
            Is.EqualTo(22));
        Assert.That(await new SchedulerPendingJobCountMetricProvider(host).GetValueAsync(CancellationToken.None),
            Is.EqualTo(33));

        Assert.That(host.Calls, Is.EqualTo(new[]
        {
            nameof(IHostQueueMetrics.GetPendingJobCountAsync),
            nameof(IHostQueueMetrics.GetPendingTaskCountAsync),
            nameof(IHostQueueMetrics.GetSchedulerPendingJobCountAsync),
        }));
    }

    [Test]
    public void ParserExtensionExposesMetricThresholdAttributeHandler()
    {
        var extension = MetricsParserExtension.Instance;

        Assert.That(extension.OperationKeyMappings, Is.Empty);
        Assert.That(extension.EventTriggerMappings, Is.Empty);
        Assert.That(extension.SingleArgExpressionMethods, Is.Empty);
        Assert.That(extension.TriggerAttributeHandlers.Keys, Is.EqualTo(new[] { "OnMetricThreshold" }));
    }

    [Test]
    public void MetricThresholdHandlerParsesMetricSourceAndThreshold()
    {
        var trigger = HandleMetricThreshold(new TestTriggerAttributeContext(
            "OnMetricThreshold",
            positionalStrings: ["System.CpuPercent"],
            namedNumbers: new Dictionary<string, double>
            {
                ["Threshold"] = 90.0,
            },
            namedEnums: new Dictionary<string, string>
            {
                ["Direction"] = nameof(ThresholdDirection.Above),
            }));

        Assert.That(trigger!.TriggerKey, Is.EqualTo(MetricTriggerKeys.MetricThreshold));
        Assert.That(trigger.Parameters[MetricTriggerKeys.Source], Is.EqualTo("System.CpuPercent"));
        Assert.That(trigger.Parameters[MetricTriggerKeys.Threshold], Is.EqualTo("90"));
        Assert.That(trigger.Parameters[MetricTriggerKeys.Direction], Is.EqualTo(ThresholdDirection.Above.ToString()));
        Assert.That(trigger.Parameters.ContainsKey(MetricTriggerKeys.PollIntervalSecs), Is.False);
    }

    [TestCase("ThresholdDirection.Above", nameof(ThresholdDirection.Above))]
    [TestCase("ThresholdDirection.Below", nameof(ThresholdDirection.Below))]
    [TestCase("ThresholdDirection.Either", nameof(ThresholdDirection.Either))]
    public void MetricThresholdHandlerPreservesDirection(string sourceDirection, string expected)
    {
        var trigger = HandleMetricThreshold(new TestTriggerAttributeContext(
            "OnMetricThreshold",
            positionalStrings: ["Queue.PendingJobCount"],
            namedNumbers: new Dictionary<string, double>
            {
                ["Threshold"] = 4.5,
            },
            namedEnums: new Dictionary<string, string>
            {
                ["Direction"] = sourceDirection,
            }));

        Assert.That(trigger!.Parameters[MetricTriggerKeys.Threshold], Is.EqualTo("4.5"));
        Assert.That(trigger.Parameters[MetricTriggerKeys.Direction], Is.EqualTo(expected));
    }

    [Test]
    public void MetricThresholdHandlerDefaultsDirectionToEither()
    {
        var trigger = HandleMetricThreshold(new TestTriggerAttributeContext(
            "OnMetricThreshold",
            positionalStrings: ["Queue.PendingTaskCount"],
            namedNumbers: new Dictionary<string, double>
            {
                ["Threshold"] = 3,
            }));

        Assert.That(trigger!.Parameters[MetricTriggerKeys.Source], Is.EqualTo("Queue.PendingTaskCount"));
        Assert.That(trigger.Parameters[MetricTriggerKeys.Threshold], Is.EqualTo("3"));
        Assert.That(trigger.Parameters[MetricTriggerKeys.Direction], Is.EqualTo(ThresholdDirection.Either.ToString()));
    }

    [Test]
    public void MetricThresholdHandlerPreservesPollInterval()
    {
        var trigger = HandleMetricThreshold(new TestTriggerAttributeContext(
            "OnMetricThreshold",
            positionalStrings: ["Scheduler.PendingJobCount"],
            namedNumbers: new Dictionary<string, double>
            {
                ["Threshold"] = 1,
            },
            namedEnums: new Dictionary<string, string>
            {
                ["Direction"] = nameof(ThresholdDirection.Below),
            },
            namedInts: new Dictionary<string, int>
            {
                ["PollInterval"] = 15,
            }));

        Assert.That(trigger!.Parameters[MetricTriggerKeys.Source], Is.EqualTo("Scheduler.PendingJobCount"));
        Assert.That(trigger.Parameters[MetricTriggerKeys.PollIntervalSecs], Is.EqualTo("15"));
        Assert.That(trigger.Parameters[MetricTriggerKeys.Direction], Is.EqualTo(ThresholdDirection.Below.ToString()));
    }

    [Test]
    public void MetricThresholdHandlerOmitsMissingOptionalFields()
    {
        var trigger = HandleMetricThreshold(new TestTriggerAttributeContext(
            "OnMetricThreshold",
            positionalStrings: []));

        Assert.That(trigger!.Parameters.ContainsKey(MetricTriggerKeys.Source), Is.False);
        Assert.That(trigger.Parameters.ContainsKey(MetricTriggerKeys.Threshold), Is.False);
        Assert.That(trigger.Parameters[MetricTriggerKeys.Direction], Is.EqualTo(ThresholdDirection.Either.ToString()));
        Assert.That(trigger.Parameters.ContainsKey(MetricTriggerKeys.PollIntervalSecs), Is.False);
    }

    [Test]
    public void MetricTriggerSourceUsesHandlerMetricSourceAsBindingValue()
    {
        var trigger = HandleMetricThreshold(new TestTriggerAttributeContext(
            "OnMetricThreshold",
            positionalStrings: ["Queue.PendingJobCount"],
            namedNumbers: new Dictionary<string, double>
            {
                ["Threshold"] = 1,
            },
            namedEnums: new Dictionary<string, string>
            {
                ["Direction"] = nameof(ThresholdDirection.Above),
            }));
        var source = new MetricTriggerSource([], NullLogger<MetricTriggerSource>.Instance);

        Assert.That(trigger, Is.Not.Null);
        Assert.That(source.GetBindingValue(trigger!), Is.EqualTo("Queue.PendingJobCount"));
    }

    [Test]
    public void MetricTriggerSourcePreservesTriggerIdentityAndBindingValue()
    {
        var source = new MetricTriggerSource([], NullLogger<MetricTriggerSource>.Instance);
        var definition = new TaskTriggerDefinition
        {
            TriggerKey = MetricTriggerKeys.MetricThreshold,
            Parameters = new Dictionary<string, string?>
            {
                [MetricTriggerKeys.Source] = "Queue.PendingJobCount",
                [MetricTriggerKeys.Threshold] = "5",
            },
        };

        Assert.That(source.TriggerKey, Is.EqualTo("MetricThreshold"));
        Assert.That(source.GetBindingValue(definition), Is.EqualTo("Queue.PendingJobCount"));
    }

    [Test]
    public void ExecuteToolRejectsEveryToolName()
    {
        var module = new MetricsModule();
        var context = new AgentJobContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "metric_unknown");

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() =>
            module.ExecuteToolAsync("unknown", default, context, new ServiceCollection().BuildServiceProvider(),
                CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("Metrics module has no job-pipeline tools"));
    }

    private sealed class RecordingHostQueueMetrics : IHostQueueMetrics
    {
        public List<string> Calls { get; } = [];

        public Task<double> GetPendingJobCountAsync(CancellationToken ct)
        {
            Calls.Add(nameof(GetPendingJobCountAsync));
            return Task.FromResult(11d);
        }

        public Task<double> GetPendingTaskCountAsync(CancellationToken ct)
        {
            Calls.Add(nameof(GetPendingTaskCountAsync));
            return Task.FromResult(22d);
        }

        public Task<double> GetSchedulerPendingJobCountAsync(CancellationToken ct)
        {
            Calls.Add(nameof(GetSchedulerPendingJobCountAsync));
            return Task.FromResult(33d);
        }
    }

    private static TaskTriggerDefinition? HandleMetricThreshold(TaskTriggerAttributeContext context) =>
        MetricsParserExtension.Instance.TriggerAttributeHandlers["OnMetricThreshold"].Handle(context);

    private sealed class TestTriggerAttributeContext(
        string attributeName,
        IReadOnlyList<string?>? positionalStrings = null,
        IReadOnlyDictionary<string, string?>? namedStrings = null,
        IReadOnlyDictionary<string, int>? namedInts = null,
        IReadOnlyDictionary<string, double>? namedNumbers = null,
        IReadOnlyDictionary<string, string>? namedEnums = null,
        int line = 1) : TaskTriggerAttributeContext
    {
        private readonly IReadOnlyList<string?> _positionalStrings = positionalStrings ?? [];
        private readonly IReadOnlyDictionary<string, string?> _namedStrings = namedStrings ??
            new Dictionary<string, string?>(StringComparer.Ordinal);
        private readonly IReadOnlyDictionary<string, int> _namedInts = namedInts ??
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly IReadOnlyDictionary<string, double> _namedNumbers = namedNumbers ??
            new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly IReadOnlyDictionary<string, string> _namedEnums = namedEnums ??
            new Dictionary<string, string>(StringComparer.Ordinal);

        public override string AttributeName { get; } = attributeName;
        public override int Line { get; } = line;
        public override int ArgumentCount => _positionalStrings.Count;
        public List<(TaskTriggerAttributeDiagnosticSeverity Severity, string Code, string Message)> Diagnostics { get; } = [];

        public override string? GetStringArg(int index) =>
            index >= 0 && index < _positionalStrings.Count ? _positionalStrings[index] : null;

        public override int? GetIntArg(int index) => null;

        public override string? GetNamedStringArg(string name) =>
            _namedStrings.GetValueOrDefault(name);

        public override int? GetNamedIntArg(string name) =>
            _namedInts.TryGetValue(name, out var value) ? value : null;

        public override double? GetNamedDoubleArg(string name) =>
            _namedNumbers.TryGetValue(name, out var value) ? value : null;

        public override T? GetNamedEnumArg<T>(string name)
        {
            if (!_namedEnums.TryGetValue(name, out var value))
                return null;

            var enumMember = value[(value.LastIndexOf('.') + 1)..];
            return Enum.TryParse<T>(enumMember, ignoreCase: true, out var parsed) ? parsed : null;
        }

        public override string? GetRawArgText(int index) => GetStringArg(index);

        public override void Report(
            TaskTriggerAttributeDiagnosticSeverity severity,
            string code,
            string message) =>
            Diagnostics.Add((severity, code, message));
    }
}
