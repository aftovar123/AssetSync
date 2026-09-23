using AssetSync.Application.Assets;
using FluentValidation.TestHelper;

namespace AssetSync.Tests.Assets;

public class CreateAssetCommandValidatorTests
{
    private readonly CreateAssetCommandValidator _validator = new();

    [Fact]
    public void EmptyCode_HasValidationError()
    {
        var result = _validator.TestValidate(new CreateAssetCommand("", "Bomba de agua", null));
        result.ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void EmptyName_HasValidationError()
    {
        var result = _validator.TestValidate(new CreateAssetCommand("AC-001", "", null));
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void ValidCommand_HasNoValidationErrors()
    {
        var result = _validator.TestValidate(new CreateAssetCommand("AC-001", "Aire acondicionado", "Bodega Norte"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
