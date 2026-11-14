using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SecureLogin.Infrastructure.Health;
using SecureLogin.Infrastructure.Identity;
using SecureLogin.Infrastructure.Persistence;

namespace SecureLogin.Infrastructure.DependencyInjection;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string? connectionString)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-";
                options.SignIn.RequireConfirmedEmail = false;

                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<SecureLoginDbContext>();

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddDbContext<SecureLoginDbContext>(options => options.UseNpgsql(connectionString));
            services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("postgresql");
        }
        else
        {
            services.AddHealthChecks().AddCheck("postgresql", () =>
                Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Database connection is not configured."));
        }

        return services;
    }
}
