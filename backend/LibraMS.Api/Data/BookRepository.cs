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
    /// <summary>
    /// The book column list. <c>status</c> is aliased to <c>status_raw</c> so it lands on
    /// <see cref="Book.StatusRaw"/> (via MatchNamesWithUnderscores) rather than being parsed
    /// straight onto the <see cref="BookStatus"/> enum, which Dapper cannot do for the
    /// stored snake_case text. Every query that materializes a Book must use this list —
    /// a bare <c>SELECT *</c> reintroduces the defect.
    /// </summary>
    internal const string BookColumns = """
        id, title, author, isbn, genre, published_year, description, cover_url,
        status AS status_raw, created_at, updated_at
        """;

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
            // ILIKE without wildcards: a full, case-insensitive match. The genre dropdown is
            // populated from GetGenresAsync(), so partial matching only ever mixed in siblings
            // like "Non-Fiction" and "Science Fiction" under a "Fiction" filter.
            conditions.Add("genre ILIKE @genre");
            parameters.Add("genre", req.Genre);
        }
        if (req.Status.HasValue)
        {
            conditions.Add("status = @status");
            parameters.Add("status", BookStatusText.ToText(req.Status.Value));
        }

        // Endpoints clamp these, but the repository is called directly too (AI search, the
        // recommendation catalogue, tests). A negative page here would reach Postgres as a
        // negative OFFSET and error, so normalise rather than trust the caller.
        var page = Math.Max(req.Page, 1);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        var where = string.Join(" AND ", conditions);
        var offset = (page - 1) * pageSize;
        parameters.Add("limit", pageSize);
        parameters.Add("offset", offset);

        var countSql = $"SELECT COUNT(*) FROM public.books WHERE {where}";
        var dataSql  = $"SELECT {BookColumns} FROM public.books WHERE {where} ORDER BY created_at DESC LIMIT @limit OFFSET @offset";

        var total = await conn.ExecuteScalarAsync<int>(countSql, parameters);
        var items = await conn.QueryAsync<Book>(dataSql, parameters);

        return new PagedResult<Book>(items, total, page, pageSize);
    }

    public async Task<Book?> GetByIdAsync(Guid id)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Book>(
            $"SELECT {BookColumns} FROM public.books WHERE id = @id", new { id });
    }

    public async Task<Book> CreateAsync(CreateBookRequest req)
    {
        using var conn = db.Create();
        const string sql = $"""
            INSERT INTO public.books (title, author, isbn, genre, published_year, description, cover_url)
            VALUES (@Title, @Author, @Isbn, @Genre, @PublishedYear, @Description, @CoverUrl)
            RETURNING {BookColumns}
            """;
        return await conn.QuerySingleAsync<Book>(sql, req);
    }

    public async Task<Book?> UpdateAsync(Guid id, UpdateBookRequest req)
    {
        using var conn = db.Create();
        // Omitted (null) means unchanged throughout. For the three optional descriptive
        // fields an empty string additionally means "clear it" — plain COALESCE cannot
        // express that, so a librarian could never remove a description once set.
        // Title and author are required, and isbn/published_year are not clearable in the UI.
        const string sql = $"""
            UPDATE public.books SET
                title          = COALESCE(@Title, title),
                author         = COALESCE(@Author, author),
                isbn           = COALESCE(@Isbn, isbn),
                genre          = CASE WHEN @Genre IS NULL THEN genre
                                      WHEN @Genre = ''    THEN NULL
                                      ELSE @Genre END,
                published_year = COALESCE(@PublishedYear, published_year),
                description    = CASE WHEN @Description IS NULL THEN description
                                      WHEN @Description = ''    THEN NULL
                                      ELSE @Description END,
                cover_url      = CASE WHEN @CoverUrl IS NULL THEN cover_url
                                      WHEN @CoverUrl = ''    THEN NULL
                                      ELSE @CoverUrl END,
                updated_at     = NOW()
            WHERE id = @Id
            RETURNING {BookColumns}
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
            new { status = BookStatusText.ToText(status), id });
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
                -- Overdue is derived here exactly as LoanRepository derives it, so the
                -- dashboard tile cannot disagree with the admin list.
                (SELECT COUNT(*) FROM public.loans
                  WHERE status <> 'returned' AND (due_date < NOW() OR status = 'overdue')) AS Overdue,
                (SELECT COUNT(*) FROM public.loans) AS TotalLoans
            """;
        return await conn.QuerySingleAsync<DashboardStats>(sql);
    }
}
