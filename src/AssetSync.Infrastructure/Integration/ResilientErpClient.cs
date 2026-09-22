using AssetSync.Application.Integration;
using AssetSync.Domain;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace AssetSync.Infrastructure.Integration;

/// <summary>
/// Wraps any IExternalErpClient with retry + exponential backoff and
/// jitter, via Polly. "How many times, how long to wait" is an
/// infrastructure/operational concern — tunable here without touching the
/// handler that decides what to do with the final success or failure.
/// </summary>
public class ResilientErpClient : IExternalErpClient
{
    private readonly IExternalErpClient _inner;
    private readonly ResiliencePipeline _pipeline;

    public ResilientErpClient(IExternalErpClient inner, ILogger<ResilientErpClient> logger)
    {
        _inner = inner;
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2, // + the first try = 3 attempts total
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(200),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "ERP submission attempt {AttemptNumber} failed; retrying after {Delay}.",
                        args.AttemptNumber + 1, args.RetryDelay);
                    return default;
                },
            })
            .Build();
    }

    public async Task SubmitWorkOrderAsync(WorkOrder workOrder, string submissionCode, CancellationToken cancellationToken)
    {
        await _pipeline.ExecuteAsync(
            async ct => await _inner.SubmitWorkOrderAsync(workOrder, submissionCode, ct),
            cancellationToken);
    }
}
