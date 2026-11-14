using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SecureLogin.Infrastructure.DependencyInjection;
using SecureLogin.Infrastructure.Identity;
using SecureLogin.Infrastructure.Persistence;

namespace SecureLogin.UnitTests;

public sealed class IdentityFoundationTests
{
    private const string AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-";

    [Fact]
    public void Identity_uses_guid_keys_and_required_unique_normalized_email()
    {
        var userIdType = typeof(ApplicationUser).BaseType!.GetGenericArguments()[0];
        var roleIdType = typeof(IdentityRole<Guid>).GetProperty(nameof(IdentityRole<Guid>.Id))!.PropertyType;
        var context = CreateContext();
        var user = context.Model.FindEntityType(typeof(ApplicationUser))!;
        var role = context.Model.FindEntityType(typeof(IdentityRole<Guid>))!;
        var createScript = context.Database.GenerateCreateScript();

        Assert.Equal(typeof(Guid), userIdType);
        Assert.Equal(typeof(Guid), roleIdType);
        Assert.Equal(typeof(Guid), user.FindProperty(nameof(ApplicationUser.Id))!.ClrType);
        Assert.Equal(typeof(Guid), role.FindProperty(nameof(IdentityRole<Guid>.Id))!.ClrType);
        Assert.False(user.FindProperty(nameof(ApplicationUser.Email))!.IsNullable);
        Assert.False(user.FindProperty(nameof(ApplicationUser.NormalizedEmail))!.IsNullable);
        Assert.False(user.FindProperty(nameof(ApplicationUser.UserName))!.IsNullable);
        Assert.False(user.FindProperty(nameof(ApplicationUser.NormalizedUserName))!.IsNullable);
        Assert.Contains(user.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(ApplicationUser.NormalizedEmail)]));
        Assert.Contains(user.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(ApplicationUser.NormalizedUserName)]));
        Assert.Contains("CK_AspNetUsers_Email_NotBlank", createScript, StringComparison.Ordinal);
        Assert.Contains("CK_AspNetUsers_NormalizedEmail_NotBlank", createScript, StringComparison.Ordinal);
        Assert.Contains("CK_AspNetUsers_UserName_NotBlank", createScript, StringComparison.Ordinal);
    }

    [Fact]
    public void Identity_options_match_the_approved_policy()
    {
        var options = GetIdentityOptions();

        Assert.True(options.User.RequireUniqueEmail);
        Assert.Equal(AllowedUserNameCharacters, options.User.AllowedUserNameCharacters);
        Assert.False(options.SignIn.RequireConfirmedEmail);
        Assert.Equal(12, options.Password.RequiredLength);
        Assert.False(options.Password.RequireDigit);
        Assert.False(options.Password.RequireLowercase);
        Assert.False(options.Password.RequireUppercase);
        Assert.False(options.Password.RequireNonAlphanumeric);
        Assert.True(options.Lockout.AllowedForNewUsers);
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task Identity_rejects_missing_or_whitespace_email(string? email)
    {
        var options = GetIdentityOptions();
        var manager = new ValidationUserManager(options, new UpperInvariantLookupNormalizer(), []);
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "valid.user", Email = email };

        var result = await new UserValidator<ApplicationUser>().ValidateAsync(manager, user);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "InvalidEmail");
    }

    [Fact]
    public async Task Identity_rejects_duplicate_email_after_normalization()
    {
        var options = GetIdentityOptions();
        var existing = new ApplicationUser { Id = Guid.NewGuid(), UserName = "existing", Email = "person@example.test" };
        var manager = new ValidationUserManager(options, new UpperInvariantLookupNormalizer(), [existing]);
        var candidate = new ApplicationUser { Id = Guid.NewGuid(), UserName = "candidate", Email = "PERSON@example.test" };

        var result = await new UserValidator<ApplicationUser>().ValidateAsync(manager, candidate);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "DuplicateEmail");
    }

    [Theory]
    [InlineData("user name")]
    [InlineData("user+name")]
    [InlineData("name/name")]
    [InlineData("naïve")]
    public async Task Identity_rejects_spaces_and_non_ascii_or_unsupported_username_characters(string userName)
    {
        var options = GetIdentityOptions();
        var manager = new ValidationUserManager(options, new UpperInvariantLookupNormalizer(), []);
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = userName, Email = "valid@example.test" };

        var result = await new UserValidator<ApplicationUser>().ValidateAsync(manager, user);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "InvalidUserName");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Identity_rejects_missing_or_whitespace_username(string? userName)
    {
        var options = GetIdentityOptions();
        var manager = new ValidationUserManager(options, new UpperInvariantLookupNormalizer(), []);
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = userName, Email = "valid@example.test" };

        var result = await new UserValidator<ApplicationUser>().ValidateAsync(manager, user);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "InvalidUserName");
    }

    [Theory]
    [InlineData("Alice", "alice")]
    [InlineData("alice_1", "ALICE_1")]
    public async Task Identity_username_uniqueness_uses_case_insensitive_normalization(string existingName, string newName)
    {
        var options = GetIdentityOptions();
        var normalizer = new UpperInvariantLookupNormalizer();
        var existing = new ApplicationUser { Id = Guid.NewGuid(), UserName = existingName, Email = "existing@example.test" };
        var manager = new ValidationUserManager(options, normalizer, [existing]);
        var candidate = new ApplicationUser { Id = Guid.NewGuid(), UserName = newName, Email = "new@example.test" };

        var result = await new UserValidator<ApplicationUser>().ValidateAsync(manager, candidate);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "DuplicateUserName");
    }

    [Fact]
    public void Domain_assembly_does_not_reference_identity_or_infrastructure()
    {
        var references = typeof(SecureLogin.Domain.DomainMarker).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name?.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(references, reference => reference.Name?.StartsWith("SecureLogin.Infrastructure", StringComparison.Ordinal) == true);
    }

    private static SecureLoginDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SecureLoginDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=identity_model_only;Username=unused;Password=unused")
            .Options;

        return new SecureLoginDbContext(options);
    }

    private static IdentityOptions GetIdentityOptions()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(connectionString: null);
        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<IdentityOptions>>().Value;
    }

    private sealed class ValidationUserManager(
        IdentityOptions options,
        ILookupNormalizer normalizer,
        IReadOnlyCollection<ApplicationUser> users)
        : UserManager<ApplicationUser>(
            new ValidationUserStore(),
            Microsoft.Extensions.Options.Options.Create(options),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            normalizer,
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        public override Task<string?> GetUserNameAsync(ApplicationUser user) => Task.FromResult(user.UserName);

        public override Task<string?> GetEmailAsync(ApplicationUser user) => Task.FromResult(user.Email);

        public override Task<string> GetUserIdAsync(ApplicationUser user) => Task.FromResult(user.Id.ToString());

        public override Task<ApplicationUser?> FindByNameAsync(string userName)
        {
            var normalized = NormalizeName(userName);
            return Task.FromResult(users.FirstOrDefault(user => NormalizeName(user.UserName) == normalized));
        }

        public override Task<ApplicationUser?> FindByEmailAsync(string email)
        {
            var normalized = NormalizeEmail(email);
            return Task.FromResult(users.FirstOrDefault(user => NormalizeEmail(user.Email) == normalized));
        }
    }

    private sealed class ValidationUserStore : IUserStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id.ToString());

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
