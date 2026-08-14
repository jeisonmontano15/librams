using System.Net;
using System.Text.Json;
using LibraMS.Api.Data;
using LibraMS.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LibraMS.Api.Tests.Endpoints;

// Returns one available and one checked-out book so the response exercises both spellings.
file sealed class TwoBookRepository : IBookRepository
{
    public static readonly Guid CheckedOutId = Guid.NewGuid();

    private static readonly Book[] Books =
    [
        new() { Title = "Dune", Author = "Frank Herbert", Status = BookStatus.Available },
        new() { Id = CheckedOutId, Title = "Sapiens", Author = "Harari", Status = BookStatus.CheckedOut }
    ];

    public Task<PagedResult<Book>> SearchAsync(BookSearchRequest req) =>
        Task.FromResult(new PagedResult<Book>(Books, Books.Length, req.Page, req.PageSize));
    public Task<Book?> GetByIdAsync(Guid id) => Task.FromResult(Books.FirstOrDefault(b => b.Id == id));
    public Task<Book> CreateAsync(CreateBookRequest req) => throw new NotImplementedException();
    public Task<Book?> UpdateAsync(Guid id, UpdateBookRequest req) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
    public Task<bool> SetStatusAsync(Guid id, BookStatus status) => throw new NotImplementedException();
    public Task<IEnumerable<string>> GetGenresAsync() => Task.FromResult(Enumerable.Empty<string>());
    public Task<DashboardStats> GetStatsAsync() => throw new NotImplementedException();
}

/// <summary>
/// Asserts the JSON that actually leaves the HTTP pipeline. The unit-level converter tests
/// build JsonSerializerOptions directly, so they cannot prove the options registered via
/// ConfigureHttpJsonOptions are the ones the endpoints serialize with — which is the exact
/// gap that let the numeric-enum defect reach production.
/// </summary>
public class BookStatusWireFormatTests(BookStatusWireFormatTests.Factory factory)
    : IClassFixture<BookStatusWireFormatTests.Factory>
{
    public class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Supabase:Url"] = "https://placeholder.supabase.co",
                    ["ConnectionStrings:Supabase"] = "Host=localhost;Database=test;Username=test;Password=test",
                    ["Frontend:Url"] = "http://localhost:5173"
                });
            });

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBookRepository));
                if (descriptor is not null) services.Remove(descriptor);
                services.AddScoped<IBookRepository, TwoBookRepository>();

                // The endpoints require an authenticated user; this test is about the
                // serialized payload, not the auth policy, so allow the request through.
                services.AddAuthorizationBuilder().SetFallbackPolicy(null);
                services.PostConfigure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(o =>
                {
                    o.AddPolicy("AnyUser", p => p.RequireAssertion(_ => true));
                    o.AddPolicy("LibrarianOnly", p => p.RequireAssertion(_ => true));
                });
            });
        }
    }

    [Fact]
    public async Task GetBooks_WritesStatusAsStringNotNumber()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/books");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("items");

        foreach (var book in items.EnumerateArray())
        {
            var status = book.GetProperty("status");
            Assert.Equal(JsonValueKind.String, status.ValueKind);
        }

        Assert.Contains("\"available\"", body);
        Assert.Contains("\"checked_out\"", body);
        Assert.DoesNotContain("\"status\":0", body);
        Assert.DoesNotContain("\"status\":1", body);
    }

    [Fact]
    public async Task GetBookById_WritesStatusAsString()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/books/{TwoBookRepository.CheckedOutId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status");

        Assert.Equal(JsonValueKind.String, status.ValueKind);
        Assert.Equal("checked_out", status.GetString());
    }
}
