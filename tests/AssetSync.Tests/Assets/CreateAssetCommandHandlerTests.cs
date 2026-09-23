using AssetSync.Application.Assets;
using Moq;

namespace AssetSync.Tests.Assets;

public class CreateAssetCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsAssetAndSaves()
    {
        var repository = new Mock<IAssetRepository>();
        var handler = new CreateAssetCommandHandler(repository.Object);

        var result = await handler.Handle(
            new CreateAssetCommand("AC-001", "Aire acondicionado", "Bodega Norte"), CancellationToken.None);

        Assert.Equal("AC-001", result.Code);
        Assert.Equal("Aire acondicionado", result.Name);
        Assert.Equal("Bodega Norte", result.Location);
        repository.Verify(r => r.AddAsync(result, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
