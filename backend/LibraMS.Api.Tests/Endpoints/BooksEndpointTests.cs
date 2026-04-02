using System.Net;
using System.Net.Http.Json;
using LibraMS.Api.Data;
using LibraMS.Api.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LibraMS.Api.Tests.Endpoints;

// Minimal in-memory stub — no real DB needed for endpoint-level tests
file sealed class StubBookRepository : IBookRepository
{
    public Task<PagedResult<Book>> SearchAsync(BookSearchRequest req) =>
        Task.FromResult(new PagedResult<Book>([], 0, req.Page, req.PageSize));
    public Task<Book?> GetByIdAsync(Guid id) => Task.FromResult<Book?>(null);
    public Task<Book> CreateAsync(CreateBookRequest req) => throw new NotImplementedException();
    public Task<Book?> UpdateAsync(Guid id, UpdateBookRequest req) => throw new NotImplementedException();
    public Task<bool> DeleteAsync(Guid id) => throw new NotImplementedException();
    public Task<bool> SetStatusAsync(Guid id, BookStatus status) => throw new NotImplementedException();
    public Task<IEnumerable<string>> GetGenresAsync() => Task.FromResult(Enumerable.Empty<string>());
    public Task<DashboardStats> GetStatsAsync() => throw new NotImplementedException();
}

public class LibramsWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"] = "https://placeholder.supabase.co",
                ["ConnectionStrings:Supabase"] = "Host=localhost;Database=test;Username=test;Password=test"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace real DB-backed repository with the stub
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IBookRepository));
            if (descriptor is not null) services.Remove(descriptor);
            services.AddScoped<IBookRepository, StubBookRepository>();
        });
    }
}

public class BooksEndpointTests(LibramsWebApplicationFactory factory)
    : IClassFixture<LibramsWebApplicationFactory>
{
    [Fact]
    public async Task GetBooks_ReturnsOk()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/books");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostBook_Unauthenticated_Returns401()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/books", new
        {
            Title = "Test",
            Author = "Author"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_ReturnsOk()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
