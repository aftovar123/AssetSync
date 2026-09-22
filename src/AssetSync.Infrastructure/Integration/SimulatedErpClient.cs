using AssetSync.Application.Integration;
using AssetSync.Domain;

namespace AssetSync.Infrastructure.Integration;

/// <summary>
/// Stands in for a real external ERP endpoint (SAP in production) so the
/// project is demo-able without external credentials. Fails occasionally
/// on purpose so the retry path in the handler runs for real, not just in
/// tests.
/// </summary>
public class SimulatedErpClient : IExternalErpClient
{
    public Task SubmitWorkOrderAsync(WorkOrder workOrder, string submissionCode, CancellationToken cancellationToken)
    {
        if (Random.Shared.Next(0, 10) == 0)
        {
            throw new InvalidOperationException("Simulated transient ERP timeout.");
        }

        return Task.CompletedTask;
    }
}
