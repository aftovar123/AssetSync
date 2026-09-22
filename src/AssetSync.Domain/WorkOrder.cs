namespace AssetSync.Domain;

public enum WorkOrderStatus
{
    Open,
    InProgress,
    Completed,
    Cancelled,
}

public class WorkOrder
{
    public int Id { get; set; }
    public int AssetId { get; set; }
    public required string Description { get; set; }
    public WorkOrderStatus Status { get; set; } = WorkOrderStatus.Open;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsSynced { get; set; }
}
