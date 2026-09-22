using MediatR;

namespace AssetSync.Application.Integration;

public record SyncWorkOrderCommand(int WorkOrderId) : IRequest<SyncWorkOrderResult>;

public record SyncWorkOrderResult(bool Success, string SubmissionCode, string? ErrorMessage);
