using FluentValidation;

namespace AssetSync.Application.Integration;

public class CompleteWorkOrderCommandValidator : AbstractValidator<CompleteWorkOrderCommand>
{
    public CompleteWorkOrderCommandValidator()
    {
        RuleFor(x => x.WorkOrderId).GreaterThan(0);
    }
}
