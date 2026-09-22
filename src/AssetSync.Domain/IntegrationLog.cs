namespace AssetSync.Domain;

/// <summary>
/// One row per sync attempt against the external system. The submission
/// code is the idempotency key: the same code is reused across retries of
/// one logical attempt, so a receiver that already processed it can
/// recognize a retried request instead of double-applying it.
/// </summary>
public class IntegrationLog
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public required string SubmissionCode { get; set; }
    public bool Sent { get; set; }
    public DateTime AttemptedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
