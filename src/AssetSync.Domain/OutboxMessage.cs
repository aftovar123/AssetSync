namespace AssetSync.Domain;

public enum OutboxMessageStatus
{
    Pending,
    Processed,
    Failed,
}

/// <summary>
/// Durable queue of "this work order needs to be synced" intents. Written
/// in the same transaction as the business change that creates the intent
/// (see CompleteWorkOrderCommand), so a crash can never leave one without
/// the other. A background processor works through these independently of
/// any single HTTP request, so an outage doesn't lose the retry.
/// </summary>
public class OutboxMessage
{
    public const int MaxAttempts = 5;

    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    public void MarkProcessed(DateTime processedAt)
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = processedAt;
    }

    /// <summary>
    /// Records one more failed attempt. Once it reaches MaxAttempts, the
    /// message stops being picked up by the poll — it needs a human to
    /// look at it instead of retrying forever against something that
    /// clearly isn't going to start working.
    /// </summary>
    public void RecordFailedAttempt(string error)
    {
        Attempts++;
        LastError = error;

        if (Attempts >= MaxAttempts)
        {
            Status = OutboxMessageStatus.Failed;
        }
    }
}
