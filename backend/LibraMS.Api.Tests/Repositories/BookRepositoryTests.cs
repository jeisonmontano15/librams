using Dapper;
using LibraMS.Api.Data;
using LibraMS.Api.Models;
using LibraMS.Api.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Xunit;

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

    [SkippableFact]
    public async Task SearchAsync_NoFilters_ReturnsPaged()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var result = await repo.SearchAsync(new BookSearchRequest(null, null, null));
        Assert.NotNull(result);
        Assert.True(result.Total >= 0);
    }

    [SkippableFact]
    public async Task SearchAsync_WithQuery_FiltersResults()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var book = await repo.CreateAsync(new CreateBookRequest("UniqueTestXYZ987", "Author", null, null, null, null, null));
        _createdIds.Add(book.Id);
        var result = await repo.SearchAsync(new BookSearchRequest("UniqueTestXYZ987", null, null));
        Assert.Contains(result.Items, b => b.Id == book.Id);
    }

    [SkippableFact]
    public async Task SearchAsync_WithStatusAvailable_ReturnsOnlyAvailable()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var result = await repo.SearchAsync(new BookSearchRequest(null, null, BookStatus.Available));
        Assert.All(result.Items, b => Assert.Equal(BookStatus.Available, b.Status));
    }

    [SkippableFact]
    public async Task CreateAsync_InsertAndReturns()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var book = await repo.CreateAsync(new CreateBookRequest("Test Book", "Test Author", null, null, null, null, null));
        _createdIds.Add(book.Id);
        Assert.Equal("Test Book", book.Title);
        Assert.Equal("Test Author", book.Author);
        Assert.Equal(BookStatus.Available, book.Status);
    }

    // ── BUG-4: genre filter matched substrings ────────────────────────────────

    [SkippableFact]
    public async Task SearchAsync_GenreFilter_ExcludesSubstringMatches()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var fiction = await CreateWithGenre(repo, "GenreTestFiction", "Fiction");
        var nonFiction = await CreateWithGenre(repo, "GenreTestNonFiction", "Non-Fiction");
        var sciFi = await CreateWithGenre(repo, "GenreTestSciFi", "Science Fiction");

        var result = await repo.SearchAsync(new BookSearchRequest(null, "Fiction", null, 1, 200));

        Assert.Contains(result.Items, b => b.Id == fiction.Id);
        Assert.DoesNotContain(result.Items, b => b.Id == nonFiction.Id);
        Assert.DoesNotContain(result.Items, b => b.Id == sciFi.Id);
    }

    [SkippableFact]
    public async Task SearchAsync_CompoundGenreFilter_ReturnsThatGenre()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var sciFi = await CreateWithGenre(repo, "GenreTestSciFi2", "Science Fiction");
        var fiction = await CreateWithGenre(repo, "GenreTestFiction2", "Fiction");

        var result = await repo.SearchAsync(new BookSearchRequest(null, "Science Fiction", null, 1, 200));

        Assert.Contains(result.Items, b => b.Id == sciFi.Id);
        Assert.DoesNotContain(result.Items, b => b.Id == fiction.Id);
    }

    [SkippableFact]
    public async Task SearchAsync_GenreFilter_IsCaseInsensitive()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var fiction = await CreateWithGenre(repo, "GenreTestCase", "Fiction");

        var result = await repo.SearchAsync(new BookSearchRequest(null, "fiction", null, 1, 200));

        Assert.Contains(result.Items, b => b.Id == fiction.Id);
    }

    // ── BUG-5: optional fields could not be cleared ───────────────────────────

    [SkippableFact]
    public async Task UpdateAsync_EmptyDescription_ClearsIt()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var book = await repo.CreateAsync(new CreateBookRequest(
            "ClearDesc", "Author", null, "Fiction", null, "An existing description", "https://example.com/c.jpg"));
        _createdIds.Add(book.Id);

        var updated = await repo.UpdateAsync(book.Id, new UpdateBookRequest(
            null, null, null, null, null, "", null));

        Assert.NotNull(updated);
        Assert.Null(updated.Description);
        Assert.Equal("Fiction", updated.Genre);
        Assert.Equal("https://example.com/c.jpg", updated.CoverUrl);
    }

    [SkippableFact]
    public async Task UpdateAsync_OmittedDescription_PreservesIt()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var book = await repo.CreateAsync(new CreateBookRequest(
            "KeepDesc", "Author", null, "Fiction", null, "An existing description", null));
        _createdIds.Add(book.Id);

        var updated = await repo.UpdateAsync(book.Id, new UpdateBookRequest(
            null, null, null, "Mystery", null, null, null));

        Assert.NotNull(updated);
        Assert.Equal("An existing description", updated.Description);
        Assert.Equal("Mystery", updated.Genre);
    }

    [SkippableFact]
    public async Task UpdateAsync_EmptyGenreAndCoverUrl_ClearsThem()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var book = await repo.CreateAsync(new CreateBookRequest(
            "ClearGenre", "Author", null, "Fiction", null, "Desc", "https://example.com/c.jpg"));
        _createdIds.Add(book.Id);

        var updated = await repo.UpdateAsync(book.Id, new UpdateBookRequest(
            null, null, null, "", null, null, ""));

        Assert.NotNull(updated);
        Assert.Null(updated.Genre);
        Assert.Null(updated.CoverUrl);
        Assert.Equal("Desc", updated.Description);
    }

    [SkippableFact]
    public async Task UpdateAsync_RequiredFields_AreNotClearedByEmptyValues()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var book = await repo.CreateAsync(new CreateBookRequest(
            "KeepTitle", "Original Author", "1234567890", null, 1999, null, null));
        _createdIds.Add(book.Id);

        var updated = await repo.UpdateAsync(book.Id, new UpdateBookRequest(
            null, null, null, null, null, null, null));

        Assert.NotNull(updated);
        Assert.Equal("KeepTitle", updated.Title);
        Assert.Equal("Original Author", updated.Author);
        Assert.Equal("1234567890", updated.Isbn);
        Assert.Equal(1999, updated.PublishedYear);
    }

    private async Task<Book> CreateWithGenre(BookRepository repo, string title, string genre)
    {
        var book = await repo.CreateAsync(new CreateBookRequest(title, "Author", null, genre, null, null, null));
        _createdIds.Add(book.Id);
        return book;
    }

    [SkippableFact]
    public async Task DeleteAsync_RemovesBook()
    {
        TestDbFixture.SkipIfUnavailable();
        var repo = BuildRepo();
        var book = await repo.CreateAsync(new CreateBookRequest("ToDelete", "Author", null, null, null, null, null));
        var deleted = await repo.DeleteAsync(book.Id);
        Assert.True(deleted);
        var found = await repo.GetByIdAsync(book.Id);
        Assert.Null(found);
    }
}
