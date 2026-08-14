using Npgsql;
using Xunit;

namespace LibraMS.Api.Tests.Fixtures;

public class TestDbFixture : IAsyncLifetime
{
    public NpgsqlConnection? Connection { get; private set; }

    public static bool IsAvailable =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING"));

    /// <summary>
    /// Skips the calling test — visibly, in the runner's output — when no test database
    /// is configured.
    /// </summary>
    /// <remarks>
    /// BUG-9: these tests previously opened with <c>if (!IsAvailable) return;</c>, which
    /// reported them as <em>passed</em> while they asserted nothing. That is how BUG-1
    /// shipped: a test genuinely exercising the broken INSERT never ran, and the suite
    /// stayed green. Callers must be <c>[SkippableFact]</c>/<c>[SkippableTheory]</c> for
    /// the raised exception to be reported as a skip.
    /// </remarks>
    public static void SkipIfUnavailable() =>
        Skip.IfNot(IsAvailable, "TEST_DB_CONNECTION_STRING is not set — no test database configured.");

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
