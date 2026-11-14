using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SecureLogin.IntegrationTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")))
            Skip = "Set ConnectionStrings__Postgres to a running PostgreSQL database to run this database test.";
    }
}

[Collection(PostgresIntegrationCollection.Name)]
public sealed class HealthEndpointTests(PostgresIdentityFixture fixture) : IClassFixture<PostgresIdentityFixture>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Postgres";
    private const string UnavailableConnectionString =
        "Host=127.0.0.1;Port=1;Database=securelogin_test_unavailable;Username=phase23_unavailable";

    [PostgresFact]
    public async Task Liveness_succeeds_and_readiness_fails_when_database_is_unavailable()
    {
        var previousConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        try
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, UnavailableConnectionString);
            using var factory = new WebApplicationFactory<Program>();
            using var client = factory.CreateClient();

            var resolvedConnectionString = factory.Services
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("Postgres");
            Assert.Equal(UnavailableConnectionString, resolvedConnectionString);

            using var liveResponse = await client.GetAsync("/health/live");
            using var readyResponse = await client.GetAsync("/health/ready");

            Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ConnectionStringVariable, previousConnectionString);
        }
    }

    [PostgresFact]
    public async Task Readiness_succeeds_with_the_disposable_postgres_database()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var resolvedConnectionString = factory.Services
            .GetRequiredService<IConfiguration>()
            .GetConnectionString("Postgres");
        Assert.Equal(fixture.ConnectionString, resolvedConnectionString);

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
