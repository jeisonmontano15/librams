using Dapper;
using LibraMS.Api.Tests.Fixtures;
using Npgsql;
using Xunit;

namespace LibraMS.Api.Tests.Repositories;

/// <summary>
/// BUG-1 regression: checkout must succeed against a database provisioned only from the
/// committed <c>schema.sql</c>. The defect shipped because every existing test ran against a
/// live database that had <c>loans.user_email</c> added out of band, so the committed schema
/// was never exercised.
///
/// These tests provision into a throwaway schema rather than the shared public one, so they
/// do not disturb the other database tests.
/// </summary>
[Collection("Database")]
public class SchemaRegressionTests : IAsyncLifetime
{
    private readonly string _schema = "libra_schematest_" + Guid.NewGuid().ToString("N")[..12];
    private NpgsqlConnection? _conn;

    /// <summary>The provisioned connection, non-null once a test has passed the availability guard.</summary>
    private NpgsqlConnection Db => _conn ?? throw new InvalidOperationException("Test database not initialised.");

    public async Task InitializeAsync()
    {
        TestDbFixture.SkipIfUnavailable();

        _conn = new NpgsqlConnection(Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING"));
        await _conn.OpenAsync();
        await Db.ExecuteAsync($"CREATE SCHEMA \"{_schema}\"");
    }

    public async Task DisposeAsync()
    {
        if (_conn is null) return;
        await Db.ExecuteAsync($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");
        await _conn.DisposeAsync();
    }

    /// <summary>
    /// Applies the committed schema into the throwaway schema by redirecting
    /// <c>public.</c> references and the search path onto it.
    /// </summary>
    private async Task ApplySchemaAsync()
    {
        foreach (var stmt in SchemaProvisioner.PortableStatements())
        {
            var scoped = stmt.Replace("public.", $"\"{_schema}\".");
            // public stays on the search path so extension functions installed there
            // (uuid_generate_v4 from uuid-ossp) still resolve.
            await Db.ExecuteAsync($"SET search_path TO \"{_schema}\", public; {scoped}");
        }
    }

    [SkippableFact]
    public async Task Schema_AppliedTwice_IsIdempotent()
    {
        TestDbFixture.SkipIfUnavailable();

        await ApplySchemaAsync();
        await ApplySchemaAsync(); // must not throw — every statement is IF NOT EXISTS / OR REPLACE
    }

    [SkippableFact]
    public async Task Schema_CreatesLoansUserEmailColumn()
    {
        TestDbFixture.SkipIfUnavailable();
        await ApplySchemaAsync();

        var exists = await Db.QuerySingleAsync<bool>("""
            SELECT EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema = @schema AND table_name = 'loans' AND column_name = 'user_email')
            """, new { schema = _schema });

        Assert.True(exists, "schema.sql must create loans.user_email — LoanRepository.CheckOutAsync inserts into it.");
    }

    [SkippableFact]
    public async Task CheckOut_AgainstSchemaProvisionedDatabase_PersistsUserEmail()
    {
        TestDbFixture.SkipIfUnavailable();
        await ApplySchemaAsync();

        // Arrange: a user and an available book, using the exact columns schema.sql declares.
        var userId = Guid.NewGuid();
        await Db.ExecuteAsync(
            $"INSERT INTO \"{_schema}\".library_users (id, email, role) VALUES (@userId, @email, 'member')",
            new { userId, email = $"bug1-{userId:N}@example.com" });

        var bookId = await Db.QuerySingleAsync<Guid>(
            $"INSERT INTO \"{_schema}\".books (title, author) VALUES ('BUG-1 Regression', 'Author') RETURNING id");

        // Act: the same INSERT LoanRepository.CheckOutAsync issues. Before the fix this
        // failed with 42703: column "user_email" does not exist.
        const string email = "borrower@example.com";
        var loanId = await Db.QuerySingleAsync<Guid>($"""
            INSERT INTO "{_schema}".loans (book_id, user_id, user_email)
            VALUES (@bookId, @userId, @email)
            RETURNING id
            """, new { bookId, userId, email });

        // Assert: the borrower's email is persisted on the loan row.
        var persisted = await Db.QuerySingleAsync<string>(
            $"SELECT user_email FROM \"{_schema}\".loans WHERE id = @loanId", new { loanId });
        Assert.Equal(email, persisted);
    }

    [SkippableFact]
    public async Task Loan_WithNullUserEmail_MapsToEmptyString()
    {
        TestDbFixture.SkipIfUnavailable();
        await ApplySchemaAsync();

        // A loan row written before user_email existed leaves the column null.
        var userId = Guid.NewGuid();
        await Db.ExecuteAsync(
            $"INSERT INTO \"{_schema}\".library_users (id, email, role) VALUES (@userId, @email, 'member')",
            new { userId, email = $"bug1-null-{userId:N}@example.com" });

        var bookId = await Db.QuerySingleAsync<Guid>(
            $"INSERT INTO \"{_schema}\".books (title, author) VALUES ('BUG-1 Null Email', 'Author') RETURNING id");

        await Db.ExecuteAsync(
            $"INSERT INTO \"{_schema}\".loans (book_id, user_id) VALUES (@bookId, @userId)",
            new { bookId, userId });

        var loan = await Db.QuerySingleAsync<LibraMS.Api.Models.Loan>(
            $"SELECT * FROM \"{_schema}\".loans WHERE book_id = @bookId", new { bookId });

        Assert.Equal("", loan.UserEmail);
    }
}

