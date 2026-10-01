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
    private static readonly string[] IgnoredPaths = ["/health", "/openapi", "/scalar"];

    public static WebApplicationBuilder AddAssetSyncTelemetry(this WebApplicationBuilder builder)
    {
        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing
                .SetSampler(new DropOrphanClientSpansSampler())
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context => !IgnoredPaths.Any(p => context.Request.Path.StartsWithSegments(p));
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

        var appInsights = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(appInsights))
        {
            otel.UseAzureMonitorExporter(options => options.ConnectionString = appInsights);
        }

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            otel.UseOtlpExporter();
        }

        return builder;
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
