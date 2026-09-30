using AssetSync.Api;
using AssetSync.Domain;
using AssetSync.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AssetSync.Tests.Api;

public class PaginationTests
{
    private static async Task<AssetSyncDbContext> CreateContextWithAssets(int count)
    {
        var db = new AssetSyncDbContext(new DbContextOptionsBuilder<AssetSyncDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        for (var i = 1; i <= count; i++)
        {
            db.Assets.Add(new Asset { Code = $"A-{i:000}", Name = $"Asset {i}" });
        }
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    [Fact]
    public async Task ToPagedResultAsync_NoParameters_ReturnsFirstPageWithDefaultSize()
    {
        await using var db = await CreateContextWithAssets(45);

        var result = await db.Assets.OrderBy(a => a.Id).ToPagedResultAsync(null, null, CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(Pagination.DefaultPageSize, result.PageSize);
        Assert.Equal(Pagination.DefaultPageSize, result.Items.Count);
        Assert.Equal(45, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal("A-001", result.Items[0].Code);
    }

    [Fact]
    public async Task ToPagedResultAsync_LastPage_ReturnsOnlyRemainingItems()
    {
        await using var db = await CreateContextWithAssets(45);

        var result = await db.Assets.OrderBy(a => a.Id).ToPagedResultAsync(3, 20, CancellationToken.None);

        Assert.Equal(5, result.Items.Count);
        Assert.Equal("A-041", result.Items[0].Code);
        Assert.Equal("A-045", result.Items[^1].Code);
    }

    [Fact]
    public async Task ToPagedResultAsync_PageBeyondTheEnd_ReturnsEmptyItemsAndRealTotal()
    {
        await using var db = await CreateContextWithAssets(5);

        var result = await db.Assets.OrderBy(a => a.Id).ToPagedResultAsync(9, 20, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(5, result.TotalCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    public async Task ToPagedResultAsync_PageBelowOne_IsClampedToFirstPage(int requested, int expected)
    {
        await using var db = await CreateContextWithAssets(5);

        var result = await db.Assets.OrderBy(a => a.Id).ToPagedResultAsync(requested, 2, CancellationToken.None);

        Assert.Equal(expected, result.Page);
        Assert.Equal("A-001", result.Items[0].Code);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5000, Pagination.MaxPageSize)]
    public async Task ToPagedResultAsync_PageSizeOutOfRange_IsClamped(int requested, int expected)
    {
        await using var db = await CreateContextWithAssets(150);

        var result = await db.Assets.OrderBy(a => a.Id).ToPagedResultAsync(1, requested, CancellationToken.None);

        Assert.Equal(expected, result.PageSize);
        Assert.Equal(expected, result.Items.Count);
    }

    [Fact]
    public async Task ToPagedResultAsync_DoesNotTrackTheReturnedEntities()
    {
        await using var db = await CreateContextWithAssets(3);

        await db.Assets.OrderBy(a => a.Id).ToPagedResultAsync(1, 10, CancellationToken.None);

        Assert.Empty(db.ChangeTracker.Entries());
    }
}
