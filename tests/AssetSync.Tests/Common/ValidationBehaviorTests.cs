using AssetSync.Application.Common.Behaviors;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;

namespace AssetSync.Tests.Common;

public record FakeRequest(string Value);

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidatorsRegistered_CallsNext()
    {
        var behavior = new ValidationBehavior<FakeRequest, string>(Array.Empty<IValidator<FakeRequest>>());
        var nextCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        };

        var result = await behavior.Handle(new FakeRequest("x"), next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Handle_ValidatorPasses_CallsNext()
    {
        var validator = new Mock<IValidator<FakeRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<FakeRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var behavior = new ValidationBehavior<FakeRequest, string>([validator.Object]);

        var result = await behavior.Handle(new FakeRequest("x"), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Handle_ValidatorFails_ThrowsAndNeverCallsNext()
    {
        var failure = new ValidationFailure("Value", "Value is required");
        var validator = new Mock<IValidator<FakeRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<FakeRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([failure]));
        var behavior = new ValidationBehavior<FakeRequest, string>([validator.Object]);
        var nextCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult("ok");
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new FakeRequest(""), next, CancellationToken.None));

        Assert.False(nextCalled);
        Assert.Contains(ex.Errors, e => e.ErrorMessage == "Value is required");
    }
}
