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
            .WithMessage(x => $"El activo {x.AssetId} no existe.")
            .WithName("Activo");

        RuleFor(x => x.Description).NotEmpty().MaximumLength(500).WithName("Descripción");
    }
}
