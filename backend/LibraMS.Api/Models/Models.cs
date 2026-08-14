namespace LibraMS.Api.Models;

public record Book
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "";
    public string Author { get; init; } = "";
    public string? Isbn { get; init; }
    public string? Genre { get; init; }
    public int? PublishedYear { get; init; }
    public string? Description { get; init; }
    public string? CoverUrl { get; init; }

    /// <summary>
    /// The raw <c>books.status</c> text, which is snake_case (<c>available</c>,
    /// <c>checked_out</c>). Dapper materializes enum-typed properties with
    /// <see cref="Enum.Parse(Type, string, bool)"/> before any registered
    /// <see cref="Dapper.SqlMapper.TypeHandler{T}"/> is consulted, so a <c>BookStatus</c>
    /// property here threw on every checked-out row. Mapping the column as text and
    /// projecting the enum in <see cref="Status"/> keeps the conversion in one place and
    /// off Dapper's materialization path.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string StatusRaw { get; init; } = "available";

    public BookStatus Status
    {
        get => BookStatusText.Parse(StatusRaw);
        init => StatusRaw = BookStatusText.ToText(value);
    }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

public enum BookStatus { Available, CheckedOut }

/// <summary>
/// The single definition of how <see cref="BookStatus"/> is spelled in the database. The
/// stored values are snake_case and do not round-trip through <see cref="Enum.Parse"/>.
/// </summary>
public static class BookStatusText
{
    public const string Available = "available";
    public const string CheckedOut = "checked_out";

    public static string ToText(BookStatus status) => status switch
    {
        BookStatus.Available  => Available,
        BookStatus.CheckedOut => CheckedOut,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown book status.")
    };

    public static BookStatus Parse(string? value) => value switch
    {
        Available   => BookStatus.Available,
        CheckedOut  => BookStatus.CheckedOut,
        null or ""  => BookStatus.Available,
        _ => throw new System.Data.DataException($"Unrecognised book status '{value}' in the database.")
    };
}

public record Loan
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BookId { get; init; }
    public Guid UserId { get; init; }
    public string UserEmail { get; init; } = "";
    public DateTime CheckedOutAt { get; init; } = DateTime.UtcNow;
    public DateTime DueDate { get; init; } = DateTime.UtcNow.AddDays(14);
    public DateTime? ReturnedAt { get; init; }

    /// <summary>
    /// The loan's status as text. These values (<c>active</c>, <c>returned</c>,
    /// <c>overdue</c>) are single words that Dapper's enum materializer does parse, unlike
    /// <c>books.status</c> — but the column is mapped the same way here so that both models
    /// convert in one place and a future multi-word status cannot reintroduce the defect.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string StatusRaw { get; init; } = LoanStatusText.Active;

    public LoanStatus Status
    {
        get => LoanStatusText.Parse(StatusRaw);
        init => StatusRaw = LoanStatusText.ToText(value);
    }

    public Book? Book { get; init; }
}

public enum LoanStatus { Active, Returned, Overdue }

/// <summary>The single definition of how <see cref="LoanStatus"/> is spelled in the database.</summary>
public static class LoanStatusText
{
    public const string Active = "active";
    public const string Returned = "returned";
    public const string Overdue = "overdue";

    public static string ToText(LoanStatus status) => status switch
    {
        LoanStatus.Active   => Active,
        LoanStatus.Returned => Returned,
        LoanStatus.Overdue  => Overdue,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown loan status.")
    };

    public static LoanStatus Parse(string? value) => value switch
    {
        Active     => LoanStatus.Active,
        Returned   => LoanStatus.Returned,
        Overdue    => LoanStatus.Overdue,
        null or "" => LoanStatus.Active,
        _ => throw new System.Data.DataException($"Unrecognised loan status '{value}' in the database.")
    };
}

/// <summary>
/// Why a checkout did or did not produce a loan. A missing book and an already borrowed one
/// were previously both signalled by a null loan, so the endpoint could only answer 409.
/// </summary>
public enum CheckOutOutcome { Success, NotFound, Unavailable }

public record CheckOutResult(CheckOutOutcome Outcome, Loan? Loan)
{
    public static CheckOutResult Success(Loan loan) => new(CheckOutOutcome.Success, loan);
    public static readonly CheckOutResult NotFound = new(CheckOutOutcome.NotFound, null);
    public static readonly CheckOutResult Unavailable = new(CheckOutOutcome.Unavailable, null);
}

public record LibraryUser
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
    public string? DisplayName { get; init; }
    public string Role { get; init; } = "member"; // "librarian" | "member"
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

// ── Request / Response DTOs ───────────────────────────────────────────────────

public record CreateBookRequest(
    string Title,
    string Author,
    string? Isbn,
    string? Genre,
    int? PublishedYear,
    string? Description,
    string? CoverUrl);

public record UpdateBookRequest(
    string? Title,
    string? Author,
    string? Isbn,
    string? Genre,
    int? PublishedYear,
    string? Description,
    string? CoverUrl);

public record BookSearchRequest(
    string? Query,
    string? Genre,
    BookStatus? Status,
    int Page = 1,
    int PageSize = 20);

public record PagedResult<T>(IEnumerable<T> Items, int Total, int Page, int PageSize);

// ── AI DTOs ───────────────────────────────────────────────────────────────────

public record AiDescribeRequest(string Title, string Author, string? Isbn);
public record AiDescribeResponse(string Description, string[] SuggestedGenres);

public record AiSearchRequest(string NaturalQuery);
public record AiSearchResponse(string? Query, string? Genre, BookStatus? Status, string Explanation);

public record AiRecommendRequest(Guid UserId);
public record AiRecommendResponse(IEnumerable<BookRecommendation> Recommendations);
public record BookRecommendation(string Title, string Author, string Reason, Guid? MatchedBookId);
