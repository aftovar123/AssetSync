namespace AssetSync.Application.Integration;

/// <summary>
/// Published once a work order has been successfully synchronized with the
/// external system — separate from IExternalErpClient (which talks to that
/// system) and INotificationService (which alerts a human). This is for
/// other systems that want to react in real time (reporting, analytics,
/// downstream services) without polling the database or the ERP.
/// </summary>
public record WorkOrderSyncedEvent(int WorkOrderId, string SubmissionCode, DateTime SyncedAt);
