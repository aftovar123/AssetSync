using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace AssetSync.Application.Common;

/// <summary>
/// The app's own traces and metrics, on top of what the HTTP and SQL
/// instrumentation already records. Built only on System.Diagnostics, so
/// Application stays free of OpenTelemetry packages: the Api project decides
/// whether anything listens (Azure Monitor, an OTLP dashboard, or nothing —
/// with no listener these calls are close to free and spans are null).
/// </summary>
public static class AssetSyncTelemetry
{
    public const string Name = "AssetSync";

    public static readonly ActivitySource ActivitySource = new(Name);

    private static readonly Meter Meter = new(Name);

    /// <summary>Outbox messages handled per outcome: processed, retry, failed.</summary>
    public static readonly Counter<long> OutboxMessages = Meter.CreateCounter<long>(
        "assetsync.outbox.messages", unit: "{message}",
        description: "Outbox messages handled by the processor, by outcome.");

    /// <summary>Time spent submitting one work order to the ERP, retries included.</summary>
    public static readonly Histogram<double> ErpSyncDuration = Meter.CreateHistogram<double>(
        "assetsync.erp.sync.duration", unit: "ms",
        description: "Duration of a work order sync with the ERP, including resilience retries.");
}
