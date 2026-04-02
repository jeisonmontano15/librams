using Dapper;
using LibraMS.Api.Data;
using LibraMS.Api.Models;
using LibraMS.Api.Tests.Fixtures;
using Microsoft.Extensions.Configuration;

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

    [Fact]
    public async Task CheckOutAsync_AvailableBook_CreatesLoan()
    {
        if (!TestDbFixture.IsAvailable) return;
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("LoanTest1", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);

        var userId = Guid.NewGuid();
        var loan = await loans.CheckOutAsync(book.Id, userId, "test@example.com");

        Assert.NotNull(loan);
        Assert.Equal(book.Id, loan.BookId);
        Assert.Equal(userId, loan.UserId);
    }

    [Fact]
    public async Task CheckOutAsync_CheckedOutBook_ReturnsNull()
    {
        if (!TestDbFixture.IsAvailable) return;
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("LoanTest2", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);

        var userId = Guid.NewGuid();
        await loans.CheckOutAsync(book.Id, userId, "test@example.com");

        var secondLoan = await loans.CheckOutAsync(book.Id, Guid.NewGuid(), "other@example.com");
        Assert.Null(secondLoan);
    }

    [Fact]
    public async Task CheckInAsync_ActiveLoan_ReturnsLoanAndFreesBook()
    {
        if (!TestDbFixture.IsAvailable) return;
        var (books, loans) = BuildRepos();
        var book = await books.CreateAsync(new CreateBookRequest("LoanTest3", "Author", null, null, null, null, null));
        _bookIds.Add(book.Id);

        var userId = Guid.NewGuid();
        var loan = await loans.CheckOutAsync(book.Id, userId, "test@example.com");
        Assert.NotNull(loan);

        var returned = await loans.CheckInAsync(loan.Id);
        Assert.NotNull(returned);
        Assert.Equal(loan.Id, returned.Id);

        var updatedBook = await books.GetByIdAsync(book.Id);
        Assert.Equal(BookStatus.Available, updatedBook!.Status);
    }
}
