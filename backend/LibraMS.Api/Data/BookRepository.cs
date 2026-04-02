using Dapper;
using LibraMS.Api.Models;

namespace LibraMS.Api.Data;

public interface IBookRepository
{
    Task<PagedResult<Book>> SearchAsync(BookSearchRequest req);
    Task<Book?> GetByIdAsync(Guid id);
    Task<Book> CreateAsync(CreateBookRequest req);
    Task<Book?> UpdateAsync(Guid id, UpdateBookRequest req);
    Task<bool> DeleteAsync(Guid id);
    Task<bool> SetStatusAsync(Guid id, BookStatus status);
    Task<IEnumerable<string>> GetGenresAsync();
    Task<DashboardStats> GetStatsAsync();
}

public record DashboardStats
{
    public int TotalBooks { get; init; }
    public int Available { get; init; }
    public int CheckedOut { get; init; }
    public int Overdue { get; init; }
    public int TotalLoans { get; init; }
}

public class BookRepository(DbConnectionFactory db) : IBookRepository
{
    public async Task<PagedResult<Book>> SearchAsync(BookSearchRequest req)
    {
        using var conn = db.Create();
        var conditions = new List<string> { "1=1" };
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(req.Query))
        {
            conditions.Add("to_tsvector('english', title || ' ' || author || ' ' || COALESCE(description,'')) @@ plainto_tsquery('english', @query)");
            parameters.Add("query", req.Query);
        }
        if (!string.IsNullOrWhiteSpace(req.Genre))
        {
            conditions.Add("genre ILIKE @genre");
            parameters.Add("genre", $"%{req.Genre}%");
        }
        if (req.Status.HasValue)
        {
            conditions.Add("status = @status");
            parameters.Add("status", req.Status.Value switch
            {
                BookStatus.Available   => "available",
                BookStatus.CheckedOut  => "checked_out",
                _                      => req.Status.Value.ToString().ToLower()
            });
        }

        var where = string.Join(" AND ", conditions);
        var offset = (req.Page - 1) * req.PageSize;
        parameters.Add("limit", req.PageSize);
        parameters.Add("offset", offset);

        var countSql = $"SELECT COUNT(*) FROM public.books WHERE {where}";
        var dataSql  = $"SELECT * FROM public.books WHERE {where} ORDER BY created_at DESC LIMIT @limit OFFSET @offset";

        var total = await conn.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await conn.QueryAsync<Book>(dataSql, parameters);

        return new PagedResult<Book>(items, total, req.Page, req.PageSize);
    }

    public async Task<Book?> GetByIdAsync(Guid id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Book>(
            "SELECT * FROM public.books WHERE id = @id", new { id });
    }

    public async Task<Book> CreateAsync(CreateBookRequest req)
    {
        using var conn = db.Create();
        const string sql = """
            INSERT INTO public.books (title, author, isbn, genre, published_year, description, cover_url)
            VALUES (@Title, @Author, @Isbn, @Genre, @PublishedYear, @Description, @CoverUrl)
            RETURNING *
            """;
        return await conn.QuerySingleAsync<Book>(sql, req);
    }

    public async Task<Book?> UpdateAsync(Guid id, UpdateBookRequest req)
    {
        using var conn = db.Create();
        const string sql = """
            UPDATE public.books SET
                title          = COALESCE(@Title, title),
                author         = COALESCE(@Author, author),
                isbn           = COALESCE(@Isbn, isbn),
                genre          = COALESCE(@Genre, genre),
                published_year = COALESCE(@PublishedYear, published_year),
                description    = COALESCE(@Description, description),
                cover_url      = COALESCE(@CoverUrl, cover_url),
                updated_at     = NOW()
            WHERE id = @Id
            RETURNING *
            """;
        return await conn.QuerySingleOrDefaultAsync<Book>(sql, new { req.Title, req.Author, req.Isbn, req.Genre, req.PublishedYear, req.Description, req.CoverUrl, Id = id });
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM public.books WHERE id = @id", new { id });
        return rows > 0;
    }

    public async Task<bool> SetStatusAsync(Guid id, BookStatus status)
    {
        using var conn = db.Create();
        var rows = await conn.ExecuteAsync(
            "UPDATE public.books SET status = @status WHERE id = @id",
            new { status = status switch { BookStatus.Available => "available", BookStatus.CheckedOut => "checked_out", _ => status.ToString().ToLower() }, id });
        return rows > 0;
    }

    public async Task<IEnumerable<string>> GetGenresAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<string>(
            "SELECT DISTINCT genre FROM public.books WHERE genre IS NOT NULL ORDER BY genre");
    }

    public async Task<DashboardStats> GetStatsAsync()
    {
        using var conn = db.Create();
        const string sql = """
            SELECT
                (SELECT COUNT(*) FROM public.books) AS TotalBooks,
                (SELECT COUNT(*) FROM public.books WHERE status = 'available') AS Available,
                (SELECT COUNT(*) FROM public.books WHERE status = 'checked_out') AS CheckedOut,
                (SELECT COUNT(*) FROM public.loans WHERE status = 'overdue') AS Overdue,
                (SELECT COUNT(*) FROM public.loans) AS TotalLoans
            """;
        return await conn.QuerySingleAsync<DashboardStats>(sql);
    }
}
