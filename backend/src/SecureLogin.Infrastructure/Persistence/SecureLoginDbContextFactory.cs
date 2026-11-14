using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace SecureLogin.Infrastructure.Persistence;

public sealed class SecureLoginDbContextFactory : IDesignTimeDbContextFactory<SecureLoginDbContext>
{
    private const string AllowDisposableDatabaseVariable = "SECURELOGIN_ALLOW_DISPOSABLE_DB";
    private const string ConnectionStringVariable = "ConnectionStrings__Postgres";
    private const string RequiredDatabasePrefix = "securelogin_test_";

    private static readonly HashSet<string> AllowedConnectionStringKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Port",
        "Database",
        "Username",
        "Password"
    };

    public SecureLoginDbContext CreateDbContext(string[] args)
    {
        var allowDisposableDatabase = Environment.GetEnvironmentVariable(AllowDisposableDatabaseVariable);
        if (!string.Equals(allowDisposableDatabase, "true", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Design-time database access is disabled. Set {AllowDisposableDatabaseVariable} to exactly 'true' for a disposable test database.");
        }

        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Design-time database access requires a non-empty {ConnectionStringVariable} process environment variable.");
        }

        NpgsqlConnectionStringBuilder parsed;
        try
        {
            parsed = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException(
                $"{ConnectionStringVariable} must be a valid PostgreSQL connection string.");
        }

        var keys = GetConnectionStringKeys(connectionString);
        if (keys.Any(key => !AllowedConnectionStringKeys.Contains(key)))
        {
            throw new InvalidOperationException(
                $"{ConnectionStringVariable} contains an unsupported or ambiguous setting. Use only canonical Host, Port, Database, Username, and Password keys.");
        }

        if (keys.Count != keys.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new InvalidOperationException(
                $"{ConnectionStringVariable} contains duplicate or ambiguous settings.");
        }

        foreach (var requiredKey in new[] { "Host", "Port", "Database" })
        {
            if (!keys.Contains(requiredKey, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{ConnectionStringVariable} must explicitly specify {requiredKey}.");
            }
        }

        var database = parsed.Database;
        if (string.IsNullOrEmpty(database)
            || !database.StartsWith(RequiredDatabasePrefix, StringComparison.Ordinal)
            || database.Length == RequiredDatabasePrefix.Length)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL database name must start with '{RequiredDatabasePrefix}' and include a test-specific suffix.");
        }

        if (!IPAddress.TryParse(parsed.Host, out var address)
            || !IPAddress.IsLoopback(address)
            || !(address.Equals(IPAddress.Loopback) || address.Equals(IPAddress.IPv6Loopback)))
        {
            throw new InvalidOperationException(
                "The PostgreSQL host must be the literal loopback IP address 127.0.0.1 or ::1; DNS names, host lists, sockets, and non-loopback addresses are not allowed.");
        }

        if (parsed.Port == 5432)
        {
            throw new InvalidOperationException(
                "Port 5432 is reserved for the existing development PostgreSQL service. Use the disposable test container's dynamically assigned loopback port.");
        }

        var options = new DbContextOptionsBuilder<SecureLoginDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new SecureLoginDbContext(options);
    }

    private static List<string> GetConnectionStringKeys(string connectionString)
    {
        var keys = new List<string>();
        var segmentStart = 0;
        var quote = '\0';

        for (var index = 0; index <= connectionString.Length; index++)
        {
            if (index < connectionString.Length)
            {
                var character = connectionString[index];
                if (quote != '\0')
                {
                    if (character == quote)
                    {
                        if (index + 1 < connectionString.Length && connectionString[index + 1] == quote)
                        {
                            index++;
                            continue;
                        }

                        quote = '\0';
                    }

                    continue;
                }

                if (character is '\'' or '"')
                {
                    quote = character;
                    continue;
                }

                if (character != ';')
                {
                    continue;
                }
            }

            var segment = connectionString[segmentStart..index].Trim();
            segmentStart = index + 1;
            if (segment.Length == 0)
            {
                continue;
            }

            var separator = segment.IndexOf('=');
            if (separator <= 0)
            {
                throw new InvalidOperationException(
                    $"{ConnectionStringVariable} contains a malformed or ambiguous setting.");
            }

            keys.Add(segment[..separator].Trim());
        }

        if (quote != '\0')
        {
            throw new InvalidOperationException(
                $"{ConnectionStringVariable} contains malformed quoting.");
        }

        return keys;
    }
}
