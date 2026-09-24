namespace AssetSync.Domain;

public enum OutboxMessageStatus
{
    Pending,
    Processing,
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

    /// <summary>
    /// Set when a processor claims this message (status moves to
    /// Processing). Lets a later poll tell a message someone is actively
    /// working on apart from one whose claimer crashed and never finished.
    /// </summary>
    public DateTime? ClaimedAt { get; set; }

    public void MarkProcessed(DateTime processedAt)
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = processedAt;
    }

    /// <summary>
    /// Records one more failed attempt. Below MaxAttempts it goes back to
    /// Pending so the next poll retries it; at MaxAttempts it stops being
    /// picked up at all — it needs a human to look at it instead of
    /// retrying forever against something that clearly isn't going to
    /// start working.
    /// </summary>
    public void RecordFailedAttempt(string error)
    {
        Attempts++;
        LastError = error;
        Status = Attempts >= MaxAttempts ? OutboxMessageStatus.Failed : OutboxMessageStatus.Pending;
    }
}
