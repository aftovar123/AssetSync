using AssetSync.Application.Assets;
using AssetSync.Application.WorkOrders;
using FluentValidation.TestHelper;
using Moq;

namespace AssetSync.Tests.WorkOrders;

public class CreateWorkOrderCommandValidatorTests
{
    private readonly Mock<IAssetRepository> _assetRepository = new();

    private CreateWorkOrderCommandValidator CreateValidator() => new(_assetRepository.Object);

    [Fact]
    public async Task NonPositiveAssetId_HasValidationError()
    {
        var result = await CreateValidator().TestValidateAsync(new CreateWorkOrderCommand(0, "Cambio de filtro"));
        result.ShouldHaveValidationErrorFor(x => x.AssetId);
    }

    [Fact]
    public async Task AssetDoesNotExist_HasValidationError()
    {
        _assetRepository.Setup(r => r.ExistsAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateValidator().TestValidateAsync(new CreateWorkOrderCommand(5, "Cambio de filtro"));

        result.ShouldHaveValidationErrorFor(x => x.AssetId);
    }

    [Fact]
    public async Task EmptyDescription_HasValidationError()
    {
        _assetRepository.Setup(r => r.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateValidator().TestValidateAsync(new CreateWorkOrderCommand(1, ""));

        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public async Task ValidCommand_HasNoValidationErrors()
    {
        _assetRepository.Setup(r => r.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateValidator().TestValidateAsync(new CreateWorkOrderCommand(1, "Cambio de filtro"));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
