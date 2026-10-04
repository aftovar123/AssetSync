namespace AssetSync.Infrastructure;

/// <summary>
/// The database engine the app runs on, chosen with "Database:Provider".
/// Production runs on SQL Server (Azure SQL); PostgreSQL is supported end to
/// end, with its own migrations in the AssetSync.Migrations.PostgreSql
/// project, since EF Core migrations are specific to one provider.
/// </summary>
public enum DatabaseProvider
{
    SqlServer,
    PostgreSql,
}
