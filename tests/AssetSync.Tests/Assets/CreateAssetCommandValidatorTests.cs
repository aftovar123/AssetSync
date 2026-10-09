using AssetSync.Application.Assets;
using FluentValidation.TestHelper;
using Moq;

namespace AssetSync.Tests.Assets;

public class CreateAssetCommandValidatorTests
{
    private readonly Mock<IAssetRepository> _repository = new();
    private readonly CreateAssetCommandValidator _validator;

    public CreateAssetCommandValidatorTests() => _validator = new CreateAssetCommandValidator(_repository.Object);

    [Fact]
    public async Task EmptyCode_HasValidationError()
    {
        var result = await _validator.TestValidateAsync(new CreateAssetCommand("", "Bomba de agua", null));
        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public async Task EmptyName_HasValidationError()
    {
        var result = await _validator.TestValidateAsync(new CreateAssetCommand("AC-001", "", null));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public async Task DuplicateCode_HasValidationErrorNamingTheCode()
    {
        _repository.Setup(r => r.CodeExistsAsync("AC-001", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _validator.TestValidateAsync(new CreateAssetCommand("AC-001", "Aire acondicionado", null));

        result.ShouldHaveValidationErrorFor(x => x.Code).WithErrorMessage("An asset with code AC-001 already exists.");
    }

    [Fact]
    public async Task ValidCommand_HasNoValidationErrors()
    {
        var result = await _validator.TestValidateAsync(new CreateAssetCommand("AC-001", "Aire acondicionado", "Bodega Norte"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
