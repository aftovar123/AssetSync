using AssetSync.Application.Integration;
using FluentValidation.TestHelper;

namespace AssetSync.Tests.Integration;

public class CompleteWorkOrderCommandValidatorTests
{
    private readonly CompleteWorkOrderCommandValidator _validator = new();

    [Fact]
    public void NonPositiveId_HasValidationError()
    {
        var result = _validator.TestValidate(new CompleteWorkOrderCommand(0));
        result.ShouldHaveValidationErrorFor(x => x.WorkOrderId);
    }

    [Fact]
    public void PositiveId_HasNoValidationErrors()
    {
        var result = _validator.TestValidate(new CompleteWorkOrderCommand(1));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
