using AssetSync.Application.Integration;
using Microsoft.Extensions.Logging;

namespace AssetSync.Infrastructure.Integration;

/// <summary>
/// Stands in for the real notification channel (email, in production).
/// Logs the outcome instead so the project needs no mail server to run.
/// </summary>
public class ConsoleNotificationService(ILogger<ConsoleNotificationService> logger) : INotificationService
{
    public Task NotifySuccessAsync(string submissionCode, CancellationToken cancellationToken)
    {
        logger.LogInformation("Work order sync {SubmissionCode} succeeded.", submissionCode);
        return Task.CompletedTask;
    }

    public Task NotifyFailureAsync(string submissionCode, string errorMessage, CancellationToken cancellationToken)
    {
        logger.LogWarning("Work order sync {SubmissionCode} failed: {Error}", submissionCode, errorMessage);
        return Task.CompletedTask;
    }
}
