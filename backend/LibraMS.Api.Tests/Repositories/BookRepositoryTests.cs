using Dapper;
using LibraMS.Api.Data;
using LibraMS.Api.Models;
using LibraMS.Api.Tests.Fixtures;
using Microsoft.Extensions.Configuration;

namespace LibraMS.Api.Tests.Repositories;

[Collection("Database")]
public class BookRepositoryTests(TestDbFixture fixture) : IAsyncLifetime
{
    private readonly List<Guid> _createdIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (fixture.Connection is null) return;
        foreach (var id in _createdIds)
            await fixture.Connection.ExecuteAsync("DELETE FROM public.books WHERE id = @id", new { id });
    }

    private BookRepository BuildRepo()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Supabase"] = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION_STRING")
            })
            .Build();
        return new BookRepository(new DbConnectionFactory(config));
    }

    [Fact]
    public async Task SearchAsync_NoFilters_ReturnsPaged()
    {
        if (!TestDbFixture.IsAvailable) return;
        var repo = BuildRepo();
        var result = await repo.SearchAsync(new BookSearchRequest(null, null, null));
        Assert.NotNull(result);
        Assert.True(result.Total >= 0);
    }

    [Fact]
    public async Task SearchAsync_WithQuery_FiltersResults()
    {
        if (!TestDbFixture.IsAvailable) return;
        var repo = BuildRepo();
        var book = await repo.CreateAsync(new CreateBookRequest("UniqueTestXYZ987", "Author", null, null, null, null, null));
        _createdIds.Add(book.Id);
        var result = await repo.SearchAsync(new BookSearchRequest("UniqueTestXYZ987", null, null));
        Assert.Contains(result.Items, b => b.Id == book.Id);
    }

    [Fact]
    public async Task SearchAsync_WithStatusAvailable_ReturnsOnlyAvailable()
    {
        if (!TestDbFixture.IsAvailable) return;
        var repo = BuildRepo();
        var result = await repo.SearchAsync(new BookSearchRequest(null, null, BookStatus.Available));
        Assert.All(result.Items, b => Assert.Equal(BookStatus.Available, b.Status));
    }

    [Fact]
    public async Task CreateAsync_InsertAndReturns()
    {
        if (!TestDbFixture.IsAvailable) return;
        var repo = BuildRepo();
        var book = await repo.CreateAsync(new CreateBookRequest("Test Book", "Test Author", null, null, null, null, null));
        _createdIds.Add(book.Id);
        Assert.Equal("Test Book", book.Title);
        Assert.Equal("Test Author", book.Author);
        Assert.Equal(BookStatus.Available, book.Status);
    }

    [Fact]
    public async Task DeleteAsync_RemovesBook()
    {
        if (!TestDbFixture.IsAvailable) return;
        var repo = BuildRepo();
        var book = await repo.CreateAsync(new CreateBookRequest("ToDelete", "Author", null, null, null, null, null));
        var deleted = await repo.DeleteAsync(book.Id);
        Assert.True(deleted);
        var found = await repo.GetByIdAsync(book.Id);
        Assert.Null(found);
    }
}
