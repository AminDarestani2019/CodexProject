using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SecureLogin.Infrastructure.Identity;

namespace SecureLogin.IntegrationTests;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class IdentityPersistenceTests(PostgresIdentityFixture fixture) : IClassFixture<PostgresIdentityFixture>
{
    private const string ValidPassword = "phase2.3-test-password";

    [PostgresFact]
    public async Task User_creation_persists_identity_fields_and_password_behavior()
    {
        var user = NewUser();
        var userId = Guid.Empty;

        try
        {
            await using (var scope = fixture.CreateScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var result = await manager.CreateAsync(user, ValidPassword);

                Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
                userId = user.Id;
                Assert.NotEqual(Guid.Empty, userId);
                Assert.True(await manager.CheckPasswordAsync(user, ValidPassword));
                Assert.False(await manager.CheckPasswordAsync(user, "incorrect-password"));
            }

            await using (var scope = fixture.CreateScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var persisted = await manager.FindByIdAsync(userId.ToString());

                Assert.NotNull(persisted);
                Assert.Equal(user.UserName, persisted.UserName);
                Assert.Equal(user.Email, persisted.Email);
                Assert.Equal(user.UserName!.ToUpperInvariant(), persisted.NormalizedUserName);
                Assert.Equal(user.Email!.ToUpperInvariant(), persisted.NormalizedEmail);
                Assert.Equal(userId, (await manager.FindByNameAsync(user.UserName))?.Id);
                Assert.Equal(userId, (await manager.FindByEmailAsync(user.Email))?.Id);
            }
        }
        finally
        {
            if (userId != Guid.Empty)
            {
                await fixture.DeleteUserAndRoleAsync(userId);
            }
        }
    }

    [PostgresFact]
    public async Task Duplicate_normalized_email_is_rejected_case_insensitively()
    {
        var first = NewUser();
        var second = NewUser(email: first.Email!.ToUpperInvariant());

        try
        {
            await using var scope = fixture.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var firstResult = await manager.CreateAsync(first, ValidPassword);
            Assert.True(firstResult.Succeeded, string.Join(", ", firstResult.Errors.Select(error => error.Code)));

            var duplicateResult = await manager.CreateAsync(second, ValidPassword);

            Assert.False(duplicateResult.Succeeded);
            Assert.Contains(duplicateResult.Errors, error => error.Code == "DuplicateEmail");
        }
        finally
        {
            if (first.Id != Guid.Empty)
            {
                await fixture.DeleteUserAndRoleAsync(first.Id);
            }
        }
    }

    [PostgresFact]
    public async Task Duplicate_normalized_username_is_rejected_case_insensitively()
    {
        var first = NewUser();
        var second = NewUser(userName: first.UserName!.ToUpperInvariant());

        try
        {
            await using var scope = fixture.CreateScope();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var firstResult = await manager.CreateAsync(first, ValidPassword);
            Assert.True(firstResult.Succeeded, string.Join(", ", firstResult.Errors.Select(error => error.Code)));

            var duplicateResult = await manager.CreateAsync(second, ValidPassword);

            Assert.False(duplicateResult.Succeeded);
            Assert.Contains(duplicateResult.Errors, error => error.Code == "DuplicateUserName");
        }
        finally
        {
            if (first.Id != Guid.Empty)
            {
                await fixture.DeleteUserAndRoleAsync(first.Id);
            }
        }
    }

    [PostgresFact]
    public async Task Lockout_is_persisted_after_five_failed_access_attempts()
    {
        var user = NewUser();
        var userId = Guid.Empty;
        var lockoutOptions = GetLockoutOptions();

        Assert.Equal(5, lockoutOptions.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), lockoutOptions.Lockout.DefaultLockoutTimeSpan);
        Assert.True(lockoutOptions.Lockout.AllowedForNewUsers);

        try
        {
            await using (var scope = fixture.CreateScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var result = await manager.CreateAsync(user, ValidPassword);
                Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
                userId = user.Id;
                Assert.True(await manager.GetLockoutEnabledAsync(user));
            }

            DateTimeOffset fifthAttemptStarted = default;
            for (var attempt = 1; attempt <= 5; attempt++)
            {
                if (attempt == 5)
                {
                    fifthAttemptStarted = DateTimeOffset.UtcNow;
                }

                await using (var attemptScope = fixture.CreateScope())
                {
                    var manager = attemptScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var current = await manager.FindByIdAsync(userId.ToString());
                    Assert.NotNull(current);
                    var result = await manager.AccessFailedAsync(current);
                    Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
                }

                // Use a fresh scope/context so the assertion reads persisted state, not tracked state.
                await using var reloadScope = fixture.CreateScope();
                var reloadManager = reloadScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var persisted = await reloadManager.FindByIdAsync(userId.ToString());
                Assert.NotNull(persisted);

                if (attempt < 5)
                {
                    Assert.Equal(attempt, await reloadManager.GetAccessFailedCountAsync(persisted));
                    Assert.False(await reloadManager.IsLockedOutAsync(persisted));
                }
                else
                {
                    Assert.True(await reloadManager.IsLockedOutAsync(persisted));
                    var lockoutEnd = await reloadManager.GetLockoutEndDateAsync(persisted);
                    Assert.NotNull(lockoutEnd);

                    var expectedEnd = fifthAttemptStarted.AddMinutes(15);
                    var tolerance = TimeSpan.FromSeconds(10);
                    Assert.InRange(lockoutEnd.Value, expectedEnd - tolerance, expectedEnd + tolerance);
                }
            }
        }
        finally
        {
            if (userId != Guid.Empty)
            {
                await fixture.DeleteUserAndRoleAsync(userId);
            }
        }
    }

    [PostgresFact]
    public async Task Role_assignment_persists_across_scopes()
    {
        var user = NewUser();
        var roleName = $"test-role-{Guid.NewGuid():N}";
        var userId = Guid.Empty;

        try
        {
            await using (var scope = fixture.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName
                });
                Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(error => error.Code)));

                var userResult = await userManager.CreateAsync(user, ValidPassword);
                Assert.True(userResult.Succeeded, string.Join(", ", userResult.Errors.Select(error => error.Code)));
                userId = user.Id;

                var assignment = await userManager.AddToRoleAsync(user, roleName);
                Assert.True(assignment.Succeeded, string.Join(", ", assignment.Errors.Select(error => error.Code)));
            }

            await using (var scope = fixture.CreateScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var persisted = await userManager.FindByIdAsync(userId.ToString());
                Assert.NotNull(persisted);
                Assert.True(await userManager.IsInRoleAsync(persisted, roleName));
                Assert.Contains(roleName, await userManager.GetRolesAsync(persisted));
            }
        }
        finally
        {
            await fixture.DeleteUserAndRoleAsync(userId, roleName);
        }
    }

    private IdentityOptions GetLockoutOptions()
    {
        using var scope = fixture.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;
    }

    private static ApplicationUser NewUser(string? userName = null, string? email = null)
    {
        var id = Guid.NewGuid().ToString("N");
        return new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = userName ?? $"User{id}",
            Email = email ?? $"user-{id}@example.test"
        };
    }
}
