namespace AssetSync.Domain;

public class MaintenanceRecord
{
    public int Id { get; set; }
    public int WorkOrderId { get; set; }
    public required string Notes { get; set; }
    public DateTime PerformedAt { get; set; }
}
