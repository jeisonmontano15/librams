using Dapper;
using LibraMS.Api.Models;

namespace LibraMS.Api.Data;

public interface ILoanRepository
{
    Task<CheckOutResult> CheckOutAsync(Guid bookId, Guid userId, string userEmail);
    Task<Loan?> CheckInAsync(Guid loanId, Guid userId, bool isLibrarian);
    Task<IEnumerable<Loan>> GetActiveLoansByUserAsync(Guid userId);
    Task<IEnumerable<Loan>> GetAllActiveLoansAsync();
    Task<IEnumerable<Loan>> GetLoanHistoryByUserAsync(Guid userId, int limit = 20);
    Task<IEnumerable<Loan>> GetOverdueLoansAsync();
    Task<Loan?> GetActiveLoanForBookAsync(Guid bookId);
}

public class LoanRepository(DbConnectionFactory db) : ILoanRepository
{
    /// <summary>
    /// Overdue is derived, never stored. An outstanding loan past its due date reads as
    /// 'overdue' the moment it becomes so, without any write — so the dashboard tile and the
    /// admin list agree with each other and with the client's own date arithmetic, and a GET
    /// stays idempotent. A row that was already flipped to 'overdue' by the previous
    /// mark_overdue_loans() behaviour still reads as overdue via the stored value.
    /// </summary>
    private const string DerivedStatusSql = """
        CASE WHEN l.status <> 'returned' AND l.due_date < NOW() THEN 'overdue' ELSE l.status END
        """;

    /// <summary>
    /// Loan columns. Like <see cref="BookRepository.BookColumns"/>, the status is aliased to
    /// <c>status_raw</c> so it maps as text rather than being parsed onto the enum directly.
    /// </summary>
    private const string LoanColumns = $"""
        l.id, l.book_id, l.user_id, l.user_email, l.checked_out_at, l.due_date,
        l.returned_at, {DerivedStatusSql} AS status_raw
        """;

    private const string LoanWithBookSql = $"""
        SELECT {LoanColumns},
               b.id, b.title, b.author, b.isbn, b.genre, b.published_year, b.description,
               b.cover_url, b.status AS status_raw, b.created_at, b.updated_at
        FROM public.loans l
        JOIN public.books b ON b.id = l.book_id
        """;

    /// <summary>
    /// Borrows a book. A book that does not exist is reported separately from one that is
    /// already borrowed, so the endpoint can answer 404 rather than 409 for an unknown id.
    /// </summary>
    public async Task<CheckOutResult> CheckOutAsync(Guid bookId, Guid userId, string userEmail)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            // Check availability, locking the row for the rest of the transaction. Selecting
            // the id as well distinguishes "no such book" from a row whose status is null —
            // a bare status select answers null for both.
            var book = await conn.QuerySingleOrDefaultAsync<BookLock>(
                "SELECT id, status FROM public.books WHERE id = @bookId FOR UPDATE", new { bookId }, tx);
            if (book is null) return CheckOutResult.NotFound;
            if (book.Status != BookStatusText.Available) return CheckOutResult.Unavailable;

            // Create loan
            var loan = await conn.QuerySingleAsync<Loan>($"""
                INSERT INTO public.loans (book_id, user_id, user_email)
                VALUES (@bookId, @userId, @userEmail)
                RETURNING id, book_id, user_id, user_email, checked_out_at, due_date,
                          returned_at, status AS status_raw
                """, new { bookId, userId, userEmail }, tx);

            // Mark book checked out
            await conn.ExecuteAsync(
                "UPDATE public.books SET status = 'checked_out', updated_at = NOW() WHERE id = @bookId",
                new { bookId }, tx);

            tx.Commit();
            return CheckOutResult.Success(loan);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>The locked book row read during checkout: the id distinguishes "no such book"
    /// from a row whose status is null, which a bare status select cannot. Status stays raw
    /// text — this is a availability check, not a materialized Book.</summary>
    private sealed record BookLock(Guid Id, string? Status);

    /// <summary>
    /// Returns a loan. Members may only return their own loans; librarians may return any
    /// loan, since returns are processed at the desk. A loan the caller may not return is
    /// indistinguishable from one that does not exist — both yield null — so that loan IDs
    /// belonging to other users are not disclosed.
    /// </summary>
    public async Task<Loan?> CheckInAsync(Guid loanId, Guid userId, bool isLibrarian)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var loan = await conn.QuerySingleOrDefaultAsync<Loan>("""
                UPDATE public.loans SET status = 'returned', returned_at = NOW()
                WHERE id = @loanId AND status != 'returned'
                  AND (@isLibrarian OR user_id = @userId)
                RETURNING id, book_id, user_id, user_email, checked_out_at, due_date,
                          returned_at, status AS status_raw
                """,
                new { loanId, userId, isLibrarian }, tx);
            if (loan is null) return null;

            await conn.ExecuteAsync(
                "UPDATE public.books SET status = 'available', updated_at = NOW() WHERE id = @bookId",
                new { loan.BookId }, tx);

            tx.Commit();
            return loan;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<IEnumerable<Loan>> GetActiveLoansByUserAsync(Guid userId)
    {
        using var conn = db.Create();
        var sql = LoanWithBookSql + " WHERE l.user_id = @userId AND l.status != 'returned' ORDER BY l.due_date ASC";
        return await conn.QueryAsync<Loan, Book, Loan>(sql,
            (loan, book) => loan with { Book = book }, new { userId }, splitOn: "id");
    }

    public async Task<IEnumerable<Loan>> GetAllActiveLoansAsync()
    {
        using var conn = db.Create();
        var sql = LoanWithBookSql + " WHERE l.status != 'returned' ORDER BY l.due_date ASC";
        return await conn.QueryAsync<Loan, Book, Loan>(sql,
            (loan, book) => loan with { Book = book }, splitOn: "id");
    }

    public async Task<IEnumerable<Loan>> GetLoanHistoryByUserAsync(Guid userId, int limit = 20)
    {
        using var conn = db.Create();
        var sql = LoanWithBookSql + " WHERE l.user_id = @userId ORDER BY l.checked_out_at DESC LIMIT @limit";
        return await conn.QueryAsync<Loan, Book, Loan>(sql,
            (loan, book) => loan with { Book = book }, new { userId, limit }, splitOn: "id");
    }

    /// <summary>
    /// Outstanding loans past their due date. Previously this called mark_overdue_loans(),
    /// so a plain GET permanently rewrote loan rows and the count stayed at zero until a
    /// librarian happened to open the admin tab. The predicate now mirrors
    /// <see cref="DerivedStatusSql"/>, and rows already flipped to 'overdue' by that former
    /// behaviour are still matched.
    /// </summary>
    public async Task<IEnumerable<Loan>> GetOverdueLoansAsync()
    {
        using var conn = db.Create();
        var sql = LoanWithBookSql +
            " WHERE l.status <> 'returned' AND (l.due_date < NOW() OR l.status = 'overdue')" +
            " ORDER BY l.due_date ASC";
        return await conn.QueryAsync<Loan, Book, Loan>(sql,
            (loan, book) => loan with { Book = book }, splitOn: "id");
    }

    public async Task<Loan?> GetActiveLoanForBookAsync(Guid bookId)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Loan>($"""
            SELECT {LoanColumns}
            FROM public.loans l
            WHERE l.book_id = @bookId AND l.status <> 'returned'
            LIMIT 1
            """, new { bookId });
    }
}
