using Npgsql;
using SecureLogin.Infrastructure.Persistence;

namespace SecureLogin.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class DatabaseConnectivityTests
{
    [PostgresFact]
    public async Task PostgreSQL_accepts_a_connection()
    {
        using var validatedContext = new SecureLoginDbContextFactory().CreateDbContext([]);
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }
}
