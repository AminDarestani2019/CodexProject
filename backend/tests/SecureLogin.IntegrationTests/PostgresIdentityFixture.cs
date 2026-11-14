using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SecureLogin.Infrastructure.DependencyInjection;
using SecureLogin.Infrastructure.Persistence;

namespace SecureLogin.IntegrationTests;

public sealed class PostgresIdentityFixture : IAsyncLifetime
{
    private const string AllowDisposableDatabaseVariable = "SECURELOGIN_ALLOW_DISPOSABLE_DB";
    private const string ConnectionStringVariable = "ConnectionStrings__Postgres";
    private const string InitialIdentityMigration = "20261114130532_InitialIdentity";

    private readonly ServiceProvider services;

    public PostgresIdentityFixture()
    {
        // Reuse the design-time factory as a fail-closed validator before any test DB access.
        using var validatedContext = new SecureLoginDbContextFactory().CreateDbContext([]);
        ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable)!;

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddInfrastructure(ConnectionString);
        services = serviceCollection.BuildServiceProvider(validateScopes: true);
    }

    public string ConnectionString { get; }

    public async Task InitializeAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecureLoginDbContext>();

        if (!await dbContext.Database.CanConnectAsync())
        {
            throw new InvalidOperationException(
                $"The guarded disposable PostgreSQL database from {ConnectionStringVariable} is not reachable.");
        }

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

        if (!appliedMigrations.Contains(InitialIdentityMigration, StringComparer.Ordinal)
            || pendingMigrations.Any())
        {
            throw new InvalidOperationException(
                "The disposable PostgreSQL database must already have InitialIdentity applied and no pending migrations. Tests never apply migrations.");
        }
    }

    public AsyncServiceScope CreateScope() => services.CreateAsyncScope();

    public async Task DeleteUserAndRoleAsync(Guid userId, string? roleName = null)
    {
        await using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<SecureLogin.Infrastructure.Identity.ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>>();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is not null)
        {
            await userManager.DeleteAsync(user);
        }

        if (roleName is not null)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is not null)
            {
                await roleManager.DeleteAsync(role);
            }
        }
    }

    public async Task DisposeAsync() => await services.DisposeAsync();
}
