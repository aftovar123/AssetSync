using AssetSync.Domain;
using MediatR;

namespace AssetSync.Application.Assets;

public record CreateAssetCommand(string Code, string Name, string? Location) : IRequest<Asset>;

public class CreateAssetCommandHandler(IAssetRepository repository) : IRequestHandler<CreateAssetCommand, Asset>
{
    public async Task<Asset> Handle(CreateAssetCommand request, CancellationToken cancellationToken)
    {
        var asset = new Asset
        {
            Code = request.Code,
            Name = request.Name,
            Location = request.Location,
        };

        await repository.AddAsync(asset, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return asset;
    }
}
