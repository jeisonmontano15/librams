using Dapper;
using LibraMS.Api.Models;

namespace LibraMS.Api.Data;

public interface ILoanRepository
{
    Task<Loan?> CheckOutAsync(Guid bookId, Guid userId, string userEmail);
    Task<Loan?> CheckInAsync(Guid loanId);
    Task<IEnumerable<Loan>> GetActiveLoansByUserAsync(Guid userId);
    Task<IEnumerable<Loan>> GetAllActiveLoansAsync();
    Task<IEnumerable<Loan>> GetLoanHistoryByUserAsync(Guid userId, int limit = 20);
    Task<IEnumerable<Loan>> GetOverdueLoansAsync();
    Task<Loan?> GetActiveLoanForBookAsync(Guid bookId);
}

public class LoanRepository(DbConnectionFactory db) : ILoanRepository
{
    private const string LoanWithBookSql = """
        SELECT l.*, b.*
        FROM public.loans l
        JOIN public.books b ON b.id = l.book_id
        """;

    public async Task<Loan?> CheckOutAsync(Guid bookId, Guid userId, string userEmail)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            // Check availability
            var status = await conn.QuerySingleOrDefaultAsync<string>(
                "SELECT status FROM public.books WHERE id = @bookId FOR UPDATE", new { bookId }, tx);
            if (status != "available") return null;

            // Create loan
            var loan = await conn.QuerySingleAsync<Loan>("""
                INSERT INTO public.loans (book_id, user_id, user_email)
                VALUES (@bookId, @userId, @userEmail)
                RETURNING *
                """, new { bookId, userId, userEmail }, tx);

            // Mark book checked out
            await conn.ExecuteAsync(
                "UPDATE public.books SET status = 'checked_out', updated_at = NOW() WHERE id = @bookId",
                new { bookId }, tx);

            tx.Commit();
            return loan;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<Loan?> CheckInAsync(Guid loanId)
    {
        using var conn = db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var loan = await conn.QuerySingleOrDefaultAsync<Loan>(
                "UPDATE public.loans SET status = 'returned', returned_at = NOW() WHERE id = @loanId AND status != 'returned' RETURNING *",
                new { loanId }, tx);
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

    public async Task<IEnumerable<Loan>> GetOverdueLoansAsync()
    {
        using var conn = db.Create();
        await conn.ExecuteAsync("SELECT public.mark_overdue_loans()");
        var sql = LoanWithBookSql + " WHERE l.status = 'overdue' ORDER BY l.due_date ASC";
        return await conn.QueryAsync<Loan, Book, Loan>(sql,
            (loan, book) => loan with { Book = book }, splitOn: "id");
    }

    public async Task<Loan?> GetActiveLoanForBookAsync(Guid bookId)
    {
        using var conn = db.Create();
        return await conn.QuerySingleOrDefaultAsync<Loan>(
            "SELECT * FROM public.loans WHERE book_id = @bookId AND status != 'returned' LIMIT 1",
            new { bookId });
    }
}
