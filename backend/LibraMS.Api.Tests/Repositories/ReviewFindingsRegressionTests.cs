using Dapper;
using LibraMS.Api.Data;
using LibraMS.Api.Models;
using LibraMS.Api.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LibraMS.Api.Tests.Repositories;

/// <summary>
/// Regressions for the architecture-review findings fixed in this change. Each test fails
/// against the code as it stood before the fix.
/// </summary>
[Collection("Database")]
public class ReviewFindingsRegressionTests(TestDbFixture fixture) : IAsyncLifetime
{
    private readonly List<Guid> _bookIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (fixture.Connection is null) return;
        foreach (var id in _bookIds)
        {
            await fixture.Connection.ExecuteAsync("DELETE FROM public.loans WHERE book_id = @id", new { id });
            await fixture.Connection.ExecuteAsync("DELETE FROM public.books WHERE id = @id", new { id });
        }
    }

    private (BookRepository Books, LoanRepository Loans) BuildRepos()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Supabase"] = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING")
            })
            .Build();
        var factory = new DbConnectionFactory(config);
        return (new BookRepository(factory), new LoanRepository(factory));
    }

    // ── F1: reading a checked-out book ───────────────────────────────────────────
    // The stored text is 'checked_out'; Dapper's default enum materializer threw
    // DataException on it, so every read path 500'd once a single book was borrowed.

    [SkippableFact]
    public async Task GetByIdAsync_CheckedOutBook_MapsStatusInsteadOfThrowing()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("F1Get", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);
        await loans.CheckOutAsync(book.Id, Guid.NewGuid(), "reader@example.com");

        var reread = await books.GetByIdAsync(book.Id);

        Assert.NotNull(reread);
        Assert.Equal(BookStatus.CheckedOut, reread.Status);
    }

    [SkippableFact]
    public async Task SearchAsync_WithACheckedOutBookInResults_DoesNotThrow()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("F1SearchZZQ", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);
        await loans.CheckOutAsync(book.Id, Guid.NewGuid(), "reader@example.com");

        var result = await books.SearchAsync(new BookSearchRequest("F1SearchZZQ", null, null));

        var found = Assert.Single(result.Items, b => b.Id == book.Id);
        Assert.Equal(BookStatus.CheckedOut, found.Status);
    }

    [SkippableFact]
    public async Task GetActiveLoansByUserAsync_MultiMapWithCheckedOutBook_DoesNotThrow()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("F1MultiMap", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);
        var userId = Guid.NewGuid();
        await loans.CheckOutAsync(book.Id, userId, "reader@example.com");

        var active = await loans.GetActiveLoansByUserAsync(userId);

        var loan = Assert.Single(active);
        Assert.NotNull(loan.Book);
        Assert.Equal(BookStatus.CheckedOut, loan.Book.Status);
    }

    [SkippableFact]
    public async Task SearchAsync_FilteredByCheckedOut_MatchesTheStoredSnakeCaseValue()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("F1Filter", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);
        await loans.CheckOutAsync(book.Id, Guid.NewGuid(), "reader@example.com");

        var result = await books.SearchAsync(new BookSearchRequest(null, null, BookStatus.CheckedOut, 1, 100));

        Assert.All(result.Items, b => Assert.Equal(BookStatus.CheckedOut, b.Status));
    }

    // ── F7: paging guards ────────────────────────────────────────────────────────
    // `page > 0 ? page : 1` guarded null and zero but not negatives, so ?page=-5
    // reached Postgres as OFFSET -120 and errored.

    [SkippableFact]
    public async Task SearchAsync_NegativePage_IsClampedInsteadOfProducingANegativeOffset()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, _) = BuildRepos();

        var result = await books.SearchAsync(new BookSearchRequest(null, null, null, -5, 20));

        Assert.Equal(1, result.Page);
    }

    [SkippableFact]
    public async Task SearchAsync_OversizedPageSize_IsCapped()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, _) = BuildRepos();

        var result = await books.SearchAsync(new BookSearchRequest(null, null, null, 1, 100_000));

        Assert.Equal(100, result.PageSize);
        Assert.True(result.Items.Count() <= 100);
    }

    // ── F6: overdue derived, not written by a GET ────────────────────────────────
    // GetOverdueLoansAsync called mark_overdue_loans(), so a read permanently flipped
    // active→overdue and the dashboard read 0 until a librarian opened the admin tab.

    [SkippableFact]
    public async Task GetOverdueLoansAsync_DoesNotMutateStoredLoanStatus()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("F6Overdue", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);
        var loan = (await loans.CheckOutAsync(book.Id, Guid.NewGuid(), "late@example.com")).Loan;
        Assert.NotNull(loan);

        // Backdate the due date so the loan is genuinely overdue.
        await fixture.Connection!.ExecuteAsync(
            "UPDATE public.loans SET due_date = NOW() - INTERVAL '3 days' WHERE id = @id", new { id = loan.Id });

        var overdue = await loans.GetOverdueLoansAsync();
        Assert.Contains(overdue, l => l.Id == loan.Id);

        // The read reports it as overdue without having written that back.
        var storedStatus = await fixture.Connection!.QuerySingleAsync<string>(
            "SELECT status FROM public.loans WHERE id = @id", new { id = loan.Id });
        Assert.Equal("active", storedStatus);
    }

    [SkippableFact]
    public async Task GetActiveLoansByUserAsync_PastDueLoan_ReportsOverdueWithoutAnyoneOpeningTheAdminTab()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("F6Derived", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);
        var userId = Guid.NewGuid();
        var loan = (await loans.CheckOutAsync(book.Id, userId, "late@example.com")).Loan;
        Assert.NotNull(loan);

        await fixture.Connection!.ExecuteAsync(
            "UPDATE public.loans SET due_date = NOW() - INTERVAL '1 day' WHERE id = @id", new { id = loan.Id });

        var active = await loans.GetActiveLoansByUserAsync(userId);

        var reread = Assert.Single(active);
        Assert.Equal(LoanStatus.Overdue, reread.Status);
    }

    [SkippableFact]
    public async Task GetStatsAsync_CountsAPastDueLoanAsOverdueWithoutAPriorAdminRead()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();

        var before = (await books.GetStatsAsync()).Overdue;

        var book = await books.CreateAsync(new CreateBookRequest("F6Stats", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);
        var loan = (await loans.CheckOutAsync(book.Id, Guid.NewGuid(), "late@example.com")).Loan;
        Assert.NotNull(loan);
        await fixture.Connection!.ExecuteAsync(
            "UPDATE public.loans SET due_date = NOW() - INTERVAL '2 days' WHERE id = @id", new { id = loan.Id });

        Assert.Equal(before + 1, (await books.GetStatsAsync()).Overdue);
    }

    [SkippableFact]
    public async Task CheckInAsync_OverdueLoan_StillReturnsSuccessfully()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("F6CheckIn", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);
        var userId = Guid.NewGuid();
        var loan = (await loans.CheckOutAsync(book.Id, userId, "late@example.com")).Loan;
        Assert.NotNull(loan);
        await fixture.Connection!.ExecuteAsync(
            "UPDATE public.loans SET due_date = NOW() - INTERVAL '5 days' WHERE id = @id", new { id = loan.Id });

        var returned = await loans.CheckInAsync(loan.Id, userId, isLibrarian: false);

        Assert.NotNull(returned);
        var reread = await books.GetByIdAsync(book.Id);
        Assert.Equal(BookStatus.Available, reread!.Status);
    }
}
