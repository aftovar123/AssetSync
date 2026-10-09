using System.Threading.RateLimiting;
using AssetSync.Application.Integration;
using AssetSync.Domain;
using AssetSync.Infrastructure;
using AssetSync.Infrastructure.Integration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using DotNet.Testcontainers.Containers;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace AssetSync.IntegrationTests;

/// <summary>
/// Runs the real API — the same Program, middleware, auth policies and EF
/// Core mappings as production — against a throwaway database container, so
/// provider-specific behaviour (the atomic outbox claim, the retrying
/// execution strategy, the migrations) is exercised for real. SQL Server by
/// default, as in production; set ASSETSYNC_TEST_DATABASE=PostgreSql to run
/// the same suite against PostgreSQL. Only three things differ from
/// production, each on purpose:
/// - the ERP client always succeeds, so no test fails at random;
/// - the outbox timer loop is off, and tests drain the outbox explicitly;
/// - the per-IP rate limit is lifted, since every test request shares one IP.
/// </summary>
public sealed class AssetSyncApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ErpClientId = "erp-integration";
    public const string ErpSecret = "erp-integration-test-secret";
    public const string AdminClientId = "asset-admin";
    public const string AdminSecret = "asset-admin-test-secret";
    public const string PanelOrigin = "https://panel.example.test";

    public static readonly DatabaseProvider Provider = Enum.Parse<DatabaseProvider>(
        Environment.GetEnvironmentVariable("ASSETSYNC_TEST_DATABASE") ?? nameof(DatabaseProvider.SqlServer),
        ignoreCase: true);

    private readonly IDatabaseContainer _sql = Provider switch
    {
        DatabaseProvider.PostgreSql => new PostgreSqlBuilder("postgres:17-alpine").Build(),
        _ => new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build(),
    };

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AssetSyncDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");
        builder.UseSetting("Database:Provider", Provider.ToString());
        builder.UseSetting("Cors:AllowedOrigins:0", PanelOrigin);
        builder.UseSetting("ConnectionStrings:AssetSyncDb", _sql.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-at-least-32-bytes");
        builder.UseSetting("Auth:Clients:0:ClientId", ErpClientId);
        builder.UseSetting("Auth:Clients:0:ClientSecret", ErpSecret);
        builder.UseSetting("Auth:Clients:0:Scopes", "workorders.write integration.read");
        builder.UseSetting("Auth:Clients:1:ClientId", AdminClientId);
        builder.UseSetting("Auth:Clients:1:ClientSecret", AdminSecret);
        builder.UseSetting("Auth:Clients:1:Scopes", "assets.write");

        builder.ConfigureTestServices(services =>
        {
            services.Remove(services.Single(d => d.ImplementationType == typeof(OutboxProcessor)));

            services.RemoveAll<IExternalErpClient>();
            services.AddScoped<IExternalErpClient, AlwaysSucceedsErpClient>();

            services.PostConfigure<RateLimiterOptions>(options =>
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    _ => RateLimitPartition.GetNoLimiter("integration-tests")));
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }

    private sealed class AlwaysSucceedsErpClient : IExternalErpClient
    {
        public Task SubmitWorkOrderAsync(WorkOrder workOrder, string submissionCode, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<AssetSyncApiFactory>
{
    public const string Name = "api";
}
