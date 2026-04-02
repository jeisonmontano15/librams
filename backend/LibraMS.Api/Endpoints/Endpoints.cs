using Carter;
using FluentValidation;
using LibraMS.Api.Data;
using LibraMS.Api.Models;
using LibraMS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibraMS.Api.Endpoints;

// ── Books ─────────────────────────────────────────────────────────────────────
public class BookEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/books").WithOpenApi();

        group.MapGet("/", async (
            [FromQuery] string? query,
            [FromQuery] string? genre,
            [FromQuery] string? status,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            IBookRepository books) =>
        {
            BookStatus? bookStatus = status?.ToLower() switch
            {
                "available"  => BookStatus.Available,
                "checked_out" => BookStatus.CheckedOut,
                _ => null
            };
            var result = await books.SearchAsync(new BookSearchRequest(query, genre, bookStatus, page > 0 ? page : 1, pageSize > 0 ? pageSize : 20));
            return Results.Ok(result);
        });

        group.MapGet("/genres", async (IBookRepository books) =>
            Results.Ok(await books.GetGenresAsync()));

        group.MapGet("/stats", async (IBookRepository books) =>
            Results.Ok(await books.GetStatsAsync()))
            .RequireAuthorization("AnyUser");

        group.MapGet("/{id:guid}", async (Guid id, IBookRepository books) =>
        {
            var book = await books.GetByIdAsync(id);
            return book is null ? Results.NotFound() : Results.Ok(book);
        });

        group.MapPost("/", async (CreateBookRequest req, IBookRepository books,
            IValidator<CreateBookRequest> validator) =>
        {
            var validation = await validator.ValidateAsync(req);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
            var book = await books.CreateAsync(req);
            return Results.Created($"/api/books/{book.Id}", book);
        }).RequireAuthorization("LibrarianOnly");

        group.MapPut("/{id:guid}", async (Guid id, UpdateBookRequest req, IBookRepository books) =>
        {
            var book = await books.UpdateAsync(id, req);
            return book is null ? Results.NotFound() : Results.Ok(book);
        }).RequireAuthorization("LibrarianOnly");

        group.MapDelete("/{id:guid}", async (Guid id, IBookRepository books) =>
        {
            var deleted = await books.DeleteAsync(id);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization("LibrarianOnly");
    }
}

// ── Loans ─────────────────────────────────────────────────────────────────────
public class LoanEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/loans").RequireAuthorization("AnyUser").WithOpenApi();

        // Check out a book
        group.MapPost("/checkout/{bookId:guid}", async (
            Guid bookId, HttpContext ctx,
            ILoanRepository loans, IBookRepository books) =>
        {
            var userId = GetUserId(ctx);
            var email  = GetUserEmail(ctx);
            if (userId == Guid.Empty) return Results.Unauthorized();

            var loan = await loans.CheckOutAsync(bookId, userId, email);
            return loan is null
                ? Results.Conflict(new { error = "Book is not available for checkout." })
                : Results.Created($"/api/loans/{loan.Id}", loan);
        });

        // Check in a book
        group.MapPost("/checkin/{loanId:guid}", async (
            Guid loanId, HttpContext ctx,
            ILoanRepository loans) =>
        {
            var loan = await loans.CheckInAsync(loanId);
            return loan is null ? Results.NotFound() : Results.Ok(loan);
        });

        // My active loans
        group.MapGet("/my", async (HttpContext ctx, ILoanRepository loans) =>
        {
            var userId = GetUserId(ctx);
            return Results.Ok(await loans.GetActiveLoansByUserAsync(userId));
        });

        // My history
        group.MapGet("/my/history", async (HttpContext ctx, ILoanRepository loans) =>
        {
            var userId = GetUserId(ctx);
            return Results.Ok(await loans.GetLoanHistoryByUserAsync(userId));
        });

        // All active loans (librarian)
        group.MapGet("/active", async (ILoanRepository loans) =>
            Results.Ok(await loans.GetAllActiveLoansAsync()))
            .RequireAuthorization("LibrarianOnly");

        // Overdue loans (librarian)
        group.MapGet("/overdue", async (ILoanRepository loans) =>
            Results.Ok(await loans.GetOverdueLoansAsync()))
            .RequireAuthorization("LibrarianOnly");
    }

    private static Guid GetUserId(HttpContext ctx) =>
        Guid.TryParse(ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id : Guid.Empty;

    private static string GetUserEmail(HttpContext ctx) =>
        ctx.User.FindFirst("email")?.Value ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
}

// ── AI ────────────────────────────────────────────────────────────────────────
public class AiEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai").RequireAuthorization("AnyUser").WithOpenApi();

        // Auto-describe a book
        group.MapPost("/describe", async (AiDescribeRequest req, IAiService ai) =>
            Results.Ok(await ai.DescribeBookAsync(req)))
            .RequireAuthorization("LibrarianOnly");

        // Natural language search
        group.MapPost("/search", async (AiSearchRequest req, IAiService ai, IBookRepository books) =>
        {
            var genres = await books.GetGenresAsync();
            var parsed = await ai.ParseNaturalSearchAsync(req, genres);
            var results = await books.SearchAsync(new BookSearchRequest(parsed.Query, parsed.Genre, parsed.Status));
            return Results.Ok(new { parsed, results });
        });

        // Personalized recommendations
        group.MapGet("/recommend", async (HttpContext ctx, IAiService ai, ILoanRepository loans, IBookRepository books) =>
        {
            var userId = Guid.TryParse(ctx.User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;
            var history = await loans.GetLoanHistoryByUserAsync(userId, 10);
            var catalog = (await books.SearchAsync(new BookSearchRequest(null, null, null, 1, 50))).Items;
            return Results.Ok(await ai.RecommendBooksAsync(userId, history, catalog));
        });
    }
}

// ── Validators ────────────────────────────────────────────────────────────────
public class CreateBookValidator : AbstractValidator<CreateBookRequest>
{
    public CreateBookValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Author).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Isbn).MaximumLength(20).When(x => x.Isbn is not null);
        RuleFor(x => x.PublishedYear).InclusiveBetween(1000, DateTime.UtcNow.Year + 1).When(x => x.PublishedYear is not null);
    }
}
