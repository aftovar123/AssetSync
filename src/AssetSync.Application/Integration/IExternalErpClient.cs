using AssetSync.Domain;

namespace AssetSync.Application.Integration;

/// <summary>
/// Stands in for the real external system (an ERP like SAP in production).
/// Throws on failure; the handler decides how to react (retry, log, notify).
/// </summary>
public interface IExternalErpClient
{
    Task SubmitWorkOrderAsync(WorkOrder workOrder, string submissionCode, CancellationToken cancellationToken);
}
