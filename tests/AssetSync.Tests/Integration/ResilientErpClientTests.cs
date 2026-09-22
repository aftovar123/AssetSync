using AssetSync.Application.Integration;
using AssetSync.Domain;
using AssetSync.Infrastructure.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AssetSync.Tests.Integration;

public class ResilientErpClientTests
{
    private readonly Mock<IExternalErpClient> _inner = new();
    private readonly WorkOrder _workOrder = new() { Id = 1, AssetId = 1, Description = "Test", CreatedAt = DateTime.UtcNow };

    private ResilientErpClient CreateClient() => new(_inner.Object, NullLogger<ResilientErpClient>.Instance);

    [Fact]
    public async Task SubmitWorkOrderAsync_TransientFailureThenSuccess_RetriesWithSameCodeAndSucceeds()
    {
        var callCount = 0;
        var codesUsed = new List<string>();
        _inner
            .Setup(c => c.SubmitWorkOrderAsync(_workOrder, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((WorkOrder _, string code, CancellationToken _) =>
            {
                callCount++;
                codesUsed.Add(code);
                if (callCount < 3)
                {
                    throw new InvalidOperationException("Simulated transient ERP timeout.");
                }
                return Task.CompletedTask;
            });

        var client = CreateClient();
        await client.SubmitWorkOrderAsync(_workOrder, "fixed-code", CancellationToken.None);

        Assert.Equal(3, callCount);
        Assert.All(codesUsed, code => Assert.Equal("fixed-code", code));
    }

    [Fact]
    public async Task SubmitWorkOrderAsync_AlwaysFails_RetriesThenGivesUp()
    {
        _inner
            .Setup(c => c.SubmitWorkOrderAsync(_workOrder, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Permanent ERP failure."));

        var client = CreateClient();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.SubmitWorkOrderAsync(_workOrder, "fixed-code", CancellationToken.None));

        // MaxRetryAttempts = 2 in the pipeline: the first try plus 2 retries = 3 calls.
        _inner.Verify(c => c.SubmitWorkOrderAsync(_workOrder, "fixed-code", It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
