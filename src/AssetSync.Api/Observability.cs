using System.Diagnostics;
using AssetSync.Application.Common;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AssetSync.Api;

/// <summary>
/// OpenTelemetry traces and metrics: incoming HTTP requests, outgoing HTTP
/// calls, SQL commands, runtime metrics, and the app's own outbox/ERP
/// telemetry (see AssetSyncTelemetry). The instrumentation is always the
/// same; where it goes depends only on configuration:
/// - APPLICATIONINSIGHTS_CONNECTION_STRING set → Azure Monitor (production);
/// - OTEL_EXPORTER_OTLP_ENDPOINT set → any OTLP backend, e.g. the Aspire
///   dashboard running locally in Docker;
/// - neither → nothing is exported (tests, plain local runs).
/// Logs stay on Serilog and are not exported, to keep the ingested volume
/// inside the Azure Monitor free allowance.
/// </summary>
public static class Observability
{
    public const string ServiceName = "assetsync-api";

    // Health probes and API docs would only add noise and ingestion volume.
    // /robots933456.txt is the probe App Service sends on every container
    // start; it and the root (no endpoint, only probes and bots) always 404
    // and would otherwise show up as failed requests.
    private static readonly string[] IgnoredPaths = ["/health", "/openapi", "/scalar", "/robots933456.txt"];

    /// <param name="warning">
    /// Set when telemetry is misconfigured but the app can run without it;
    /// Program logs it once the logger exists.
    /// </param>
    public static WebApplicationBuilder AddAssetSyncTelemetry(this WebApplicationBuilder builder, out string? warning)
    {
        warning = null;

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .SetSampler(new DropOrphanClientSpansSampler())
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !IsIgnoredPath(context.Request.Path);
                    options.RecordException = true;
                })
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .AddSource(AssetSyncTelemetry.Name))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(AssetSyncTelemetry.Name));

        // Telemetry is optional, so a bad value must never take the API down.
        // The exporter throws while the host starts if the connection string
        // is malformed — e.g. the bare instrumentation key pasted instead of
        // the full "InstrumentationKey=...;IngestionEndpoint=..." string,
        // which crash-looped the app on its free plan until a quota stopped it.
        var appInsights = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(appInsights))
        {
            if (IsValidAzureMonitorConnectionString(appInsights))
            {
                otel.UseAzureMonitorExporter(options => options.ConnectionString = appInsights);
            }
            else
            {
                warning = "APPLICATIONINSIGHTS_CONNECTION_STRING is not a valid connection string " +
                    "(expected \"InstrumentationKey=...;IngestionEndpoint=...\"); telemetry will not be sent to Azure Monitor.";
            }
        }

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>Requests that are not traced: probes, docs and the root.</summary>
    public static bool IsIgnoredPath(PathString path) =>
        !path.HasValue || path.Value == "/" || IgnoredPaths.Any(p => path.StartsWithSegments(p));

    /// <summary>
    /// Same shape check the exporter does: "key=value" segments separated by
    /// ';', one of them InstrumentationKey with a value.
    /// </summary>
    public static bool IsValidAzureMonitorConnectionString(string value)
    {
        var segments = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0
            && segments.All(s => s.IndexOf('=') > 0)
            && segments.Any(s => s.StartsWith("InstrumentationKey=", StringComparison.OrdinalIgnoreCase)
                && s.Length > "InstrumentationKey=".Length);
    }

    /// <summary>
    /// The outbox processor polls with an UPDATE every 10 seconds, even when
    /// there is nothing to claim. Recorded as-is, that is a standalone SQL
    /// trace every 10 seconds (~8,600 a day) that says nothing and eats the
    /// free ingestion allowance. This drops outgoing (client) spans that have
    /// no parent — a SQL command or HTTP call not running inside an incoming
    /// request or an outbox batch — and keeps the parent's decision for
    /// everything else, so the queries inside a real batch are still traced.
    /// </summary>
    private sealed class DropOrphanClientSpansSampler : Sampler
    {
        private readonly Sampler _inner = new ParentBasedSampler(new AlwaysOnSampler());

        public override SamplingResult ShouldSample(in SamplingParameters parameters) =>
            parameters.ParentContext.TraceId == default && parameters.Kind == ActivityKind.Client
                ? new SamplingResult(SamplingDecision.Drop)
                : _inner.ShouldSample(parameters);
    }
}
