using Dapper;
using LibraMS.Api.Data;
using LibraMS.Api.Models;
using LibraMS.Api.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LibraMS.Api.Tests.Repositories;

[Collection("Database")]
public class LoanRepositoryTests(TestDbFixture fixture) : IAsyncLifetime
{
    private readonly List<Guid> _bookIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (fixture.Connection is null) return;
        foreach (var id in _bookIds)
        {
            await fixture.Connection.ExecuteAsync(
                "DELETE FROM public.loans WHERE book_id = @id", new { id });
            await fixture.Connection.ExecuteAsync(
                "DELETE FROM public.books WHERE id = @id", new { id });
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

    [SkippableFact]
    public async Task CheckOutAsync_AvailableBook_CreatesLoan()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("LoanTest1", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);

        var userId = Guid.NewGuid();
        var result = await loans.CheckOutAsync(book.Id, userId, "test@example.com");

        Assert.Equal(CheckOutOutcome.Success, result.Outcome);
        Assert.NotNull(result.Loan);
        Assert.Equal(book.Id, result.Loan.BookId);
        Assert.Equal(userId, result.Loan.UserId);
    }

    [SkippableFact]
    public async Task CheckOutAsync_CheckedOutBook_ReturnsUnavailable()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("LoanTest2", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);

        var userId = Guid.NewGuid();
        await loans.CheckOutAsync(book.Id, userId, "test@example.com");

        var second = await loans.CheckOutAsync(book.Id, Guid.NewGuid(), "other@example.com");
        Assert.Equal(CheckOutOutcome.Unavailable, second.Outcome);
        Assert.Null(second.Loan);
    }

    // ── BUG-3 regression: an unknown book id must be distinguishable from a borrowed one ──
    // Before the fix CheckOutAsync answered null for both, so the endpoint reported 409
    // Conflict for a book that does not exist.

    [SkippableFact]
    public async Task CheckOutAsync_UnknownBookId_ReturnsNotFound()
    {
        TestDbFixture.SkipIfUnavailable();
        var (_, loans) = BuildRepos();

        var result = await loans.CheckOutAsync(Guid.NewGuid(), Guid.NewGuid(), "test@example.com");

        Assert.Equal(CheckOutOutcome.NotFound, result.Outcome);
        Assert.Null(result.Loan);
    }

    [SkippableFact]
    public async Task CheckInAsync_ActiveLoan_ReturnsLoanAndFreesBook()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("LoanTest3", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);

        var userId = Guid.NewGuid();
        var loan = (await loans.CheckOutAsync(book.Id, userId, "test@example.com")).Loan;
        Assert.NotNull(loan);

        var returned = await loans.CheckInAsync(loan.Id, userId, isLibrarian: false);
        Assert.NotNull(returned);
        Assert.Equal(loan.Id, returned.Id);

        var updatedBook = await books.GetByIdAsync(book.Id);
        Assert.Equal(BookStatus.Available, updatedBook!.Status);
    }

    // ── BUG-2 regression: check-in must be scoped to the loan's owner ─────────────
    // Before the fix CheckInAsync filtered on loan ID alone, so any authenticated user
    // could return any loan by ID.

    [SkippableFact]
    public async Task CheckInAsync_LoanBelongingToAnotherMember_ReturnsNullAndLeavesLoanOutstanding()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("LoanTest4", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);

        var owner = Guid.NewGuid();
        var loan = (await loans.CheckOutAsync(book.Id, owner, "owner@example.com")).Loan;
        Assert.NotNull(loan);

        // A different member attempts the return.
        var returned = await loans.CheckInAsync(loan.Id, Guid.NewGuid(), isLibrarian: false);

        Assert.Null(returned);

        // The loan is still outstanding and the book is still checked out.
        var stillActive = await loans.GetActiveLoanForBookAsync(book.Id);
        Assert.NotNull(stillActive);
        Assert.Equal(loan.Id, stillActive.Id);
        Assert.Null(stillActive.ReturnedAt);

        var stillOut = await books.GetByIdAsync(book.Id);
        Assert.Equal(BookStatus.CheckedOut, stillOut!.Status);
    }

    [SkippableFact]
    public async Task CheckInAsync_LibrarianReturningAnotherMembersLoan_Succeeds()
    {
        TestDbFixture.SkipIfUnavailable();
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("LoanTest5", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);

        var owner = Guid.NewGuid();
        var loan = (await loans.CheckOutAsync(book.Id, owner, "owner@example.com")).Loan;
        Assert.NotNull(loan);

        // Returns are processed at the desk, so a librarian returns on the member's behalf.
        var returned = await loans.CheckInAsync(loan.Id, Guid.NewGuid(), isLibrarian: true);

        Assert.NotNull(returned);
        Assert.Equal(loan.Id, returned.Id);
        Assert.NotNull(returned.ReturnedAt);

        var updatedBook = await books.GetByIdAsync(book.Id);
        Assert.Equal(BookStatus.Available, updatedBook!.Status);
    }
}
