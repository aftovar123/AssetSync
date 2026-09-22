namespace AssetSync.Application.Integration;

public interface INotificationService
{
    Task NotifySuccessAsync(string submissionCode, CancellationToken cancellationToken);
    Task NotifyFailureAsync(string submissionCode, string errorMessage, CancellationToken cancellationToken);
}
