using Microsoft.EntityFrameworkCore;
using SecureLogin.Infrastructure.Persistence;

namespace SecureLogin.UnitTests;

[Collection(nameof(DesignTimeFactoryEnvironmentCollection))]
public sealed class SecureLoginDbContextFactoryTests
{
    private const string AllowVariable = "SECURELOGIN_ALLOW_DISPOSABLE_DB";
    private const string ConnectionVariable = "ConnectionStrings__Postgres";

    [Fact]
    public void Rejects_missing_safety_flag()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateWithEnvironment(null, ValidIpv4Connection));

        Assert.Contains(AllowVariable, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData(" true")]
    [InlineData("false")]
    public void Rejects_safety_flag_unless_exactly_lowercase_true(string flag)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateWithEnvironment(flag, ValidIpv4Connection));

        Assert.Contains("exactly 'true'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_missing_connection_string()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateWithEnvironment("true", null));

        Assert.Contains(ConnectionVariable, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not a connection string")]
    [InlineData("Host=127.0.0.1;Port=abc;Database=securelogin_test_bad")]
    public void Rejects_malformed_connection_string_without_echoing_it(string connectionString)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateWithEnvironment("true", connectionString));

        Assert.Contains(ConnectionVariable, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(connectionString, exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("securelogin_dev")]
    [InlineData("SecureLogin_test_uppercase")]
    [InlineData("securelogin_test_")]
    public void Rejects_database_names_outside_test_prefix(string database)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateWithEnvironment("true", Ipv4Connection(database: database)));

        Assert.Contains("database name must start", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("192.0.2.10")]
    [InlineData("0.0.0.0")]
    [InlineData("127.0.0.2")]
    public void Rejects_non_allowed_loopback_addresses(string host)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateWithEnvironment("true", Ipv4Connection(host: host)));

        Assert.Contains("literal loopback IP address", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1,::1")]
    [InlineData("/var/run/postgresql")]
    public void Rejects_dns_host_lists_and_socket_paths(string host)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateWithEnvironment("true", Ipv4Connection(host: host)));

        Assert.Contains("literal loopback IP address", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rejects_port_5432()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateWithEnvironment("true", Ipv4Connection(port: 5432)));

        Assert.Contains("Port 5432", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Host=127.0.0.1;Server=127.0.0.1;Port=55432;Database=securelogin_test_duplicate")]
    [InlineData("Host=127.0.0.1;Host=127.0.0.1;Port=55432;Database=securelogin_test_duplicate")]
    [InlineData("Host=127.0.0.1;Port=55432;Database=securelogin_test_duplicate;Application Name=ambiguous")]
    public void Rejects_duplicate_alias_or_unsupported_connection_settings(string connectionString)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateWithEnvironment("true", connectionString));

        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Creates_context_for_valid_ipv4_loopback_without_connecting()
    {
        using var context = CreateWithEnvironment("true", ValidIpv4Connection);

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public void Creates_context_for_valid_ipv6_loopback_without_connecting()
    {
        using var context = CreateWithEnvironment("true", Ipv4Connection(host: "::1", port: 55434));

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.Equal(System.Data.ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    private const string ValidIpv4Connection =
        "Host=127.0.0.1;Port=55432;Database=securelogin_test_factory;Username=test_user;Password=test_password";

    private static SecureLoginDbContext CreateWithEnvironment(string? flag, string? connectionString)
    {
        var previousFlag = Environment.GetEnvironmentVariable(AllowVariable);
        var previousConnection = Environment.GetEnvironmentVariable(ConnectionVariable);

        try
        {
            Environment.SetEnvironmentVariable(AllowVariable, flag);
            Environment.SetEnvironmentVariable(ConnectionVariable, connectionString);
            return new SecureLoginDbContextFactory().CreateDbContext([]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AllowVariable, previousFlag);
            Environment.SetEnvironmentVariable(ConnectionVariable, previousConnection);
        }
    }

    private static string Ipv4Connection(
        string host = "127.0.0.1",
        int port = 55432,
        string database = "securelogin_test_factory") =>
        $"Host={host};Port={port};Database={database};Username=test_user;Password=test_password";
}

[CollectionDefinition(nameof(DesignTimeFactoryEnvironmentCollection), DisableParallelization = true)]
public sealed class DesignTimeFactoryEnvironmentCollection
{
}
