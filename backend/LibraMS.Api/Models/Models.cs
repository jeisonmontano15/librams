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
    public BookStatus Status { get; init; } = BookStatus.Available;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
}

public enum BookStatus { Available, CheckedOut }

public record Loan
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid BookId { get; init; }
    public Guid UserId { get; init; }
    public string UserEmail { get; init; } = "";
    public DateTime CheckedOutAt { get; init; } = DateTime.UtcNow;
    public DateTime DueDate { get; init; } = DateTime.UtcNow.AddDays(14);
    public DateTime? ReturnedAt { get; init; }
    public LoanStatus Status { get; init; } = LoanStatus.Active;
    public Book? Book { get; init; }
}

public enum LoanStatus { Active, Returned, Overdue }

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
