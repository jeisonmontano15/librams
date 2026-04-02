using Npgsql;
using Xunit;

namespace LibraMS.Api.Tests.Fixtures;

public class TestDbFixture : IAsyncLifetime
{
    public NpgsqlConnection? Connection { get; private set; }

    public static bool IsAvailable =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING"));

    public async Task InitializeAsync()
    {
        var connStr = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connStr)) return;

        Connection = new NpgsqlConnection(connStr);
        await Connection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        if (Connection is not null)
            await Connection.DisposeAsync();
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<TestDbFixture> { }
