using AssetSync.Application.Assets;
using FluentValidation;

namespace AssetSync.Application.WorkOrders;

public class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
{
    public CreateWorkOrderCommandValidator(IAssetRepository assetRepository)
    {
        RuleFor(x => x.AssetId)
            .GreaterThan(0)
            .MustAsync(assetRepository.ExistsAsync)
            .WithMessage(x => $"Asset {x.AssetId} does not exist.");

        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
    }
}
