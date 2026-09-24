using AssetSync.Domain;
using Xunit;

namespace AssetSync.Tests.Domain;

/// <summary>
/// Pure domain rule, no repository or database involved: how many failed
/// attempts it takes before a message is dead-lettered.
/// </summary>
public class OutboxMessageTests
{
    private static OutboxMessage MakeMessage() => new() { WorkOrderId = 1, CreatedAt = DateTime.UtcNow };

    [Fact]
    public void RecordFailedAttempt_BelowMaxAttempts_StaysPending()
    {
        var message = MakeMessage();

        for (var i = 0; i < OutboxMessage.MaxAttempts - 1; i++)
        {
            message.RecordFailedAttempt("boom");
        }

        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(OutboxMessage.MaxAttempts - 1, message.Attempts);
        Assert.Equal("boom", message.LastError);
    }

    [Fact]
    public void RecordFailedAttempt_ReachingMaxAttempts_MarksFailed()
    {
        var message = MakeMessage();

        for (var i = 0; i < OutboxMessage.MaxAttempts; i++)
        {
            message.RecordFailedAttempt("boom");
        }

        Assert.Equal(OutboxMessageStatus.Failed, message.Status);
        Assert.Equal(OutboxMessage.MaxAttempts, message.Attempts);
    }

    [Fact]
    public void RecordFailedAttempt_WhileProcessing_ResetsToPendingForRetry()
    {
        var message = MakeMessage();
        message.Status = OutboxMessageStatus.Processing;
        message.ClaimedAt = DateTime.UtcNow;

        message.RecordFailedAttempt("timeout");

        // Back to Pending, not stuck in Processing — the next poll (from
        // this instance or another) can claim and retry it.
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
    }

    [Fact]
    public void MarkProcessed_SetsStatusAndTimestamp()
    {
        var message = MakeMessage();
        var processedAt = new DateTime(2026, 9, 22, 15, 0, 0, DateTimeKind.Utc);

        message.MarkProcessed(processedAt);

        Assert.Equal(OutboxMessageStatus.Processed, message.Status);
        Assert.Equal(processedAt, message.ProcessedAt);
    }
}
