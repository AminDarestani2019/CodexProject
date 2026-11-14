using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SecureLogin.Infrastructure.Identity;

namespace SecureLogin.Infrastructure.Persistence;

public sealed class SecureLoginDbContext(DbContextOptions<SecureLoginDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.Email).IsRequired();
            entity.Property(user => user.NormalizedEmail).IsRequired();
            entity.Property(user => user.UserName).IsRequired();
            entity.Property(user => user.NormalizedUserName).IsRequired();

            entity.HasIndex(user => user.NormalizedEmail)
                .IsUnique()
                .HasDatabaseName("EmailIndex");

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_AspNetUsers_Email_NotBlank",
                    "\"Email\" ~ '[^[:space:]]'");
                table.HasCheckConstraint(
                    "CK_AspNetUsers_NormalizedEmail_NotBlank",
                    "\"NormalizedEmail\" ~ '[^[:space:]]'");
                table.HasCheckConstraint(
                    "CK_AspNetUsers_UserName_NotBlank",
                    "\"UserName\" ~ '[^[:space:]]'");
            });
        });
    }
}
