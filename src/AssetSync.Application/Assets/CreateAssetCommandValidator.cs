using FluentValidation;

namespace AssetSync.Application.Assets;

public class CreateAssetCommandValidator : AbstractValidator<CreateAssetCommand>
{
    public CreateAssetCommandValidator(IAssetRepository assetRepository)
    {
        // The unique index on Code would reject a duplicate anyway, but as a
        // database error (a 500); checking first gives the client a 400 that
        // says which field is wrong.
        RuleFor(x => x.Code)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(50)
            .MustAsync(async (code, ct) => !await assetRepository.CodeExistsAsync(code, ct))
            .WithMessage(x => $"An asset with code {x.Code} already exists.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Location).MaximumLength(200);
    }
}
