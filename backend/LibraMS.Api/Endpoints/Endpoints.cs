using Carter;
using FluentValidation;
using LibraMS.Api.Data;
using LibraMS.Api.Models;
using LibraMS.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LibraMS.Api.Endpoints;

/// <summary>
/// Clamps caller-supplied paging. A bare <c>page > 0</c> check guards null and zero but not
/// negatives — <c>?page=-5</c> produced a negative OFFSET and a Postgres error rather than a
/// 400 — and an unbounded page size let one request ask for the entire table.
/// </summary>
internal static class Paging
{
    internal const int DefaultPageSize = 20;
    internal const int MaxPageSize = 100;

    internal static int Page(int? page) => page is > 0 ? page.Value : 1;

    internal static int PageSize(int? pageSize) =>
        pageSize is > 0 ? Math.Min(pageSize.Value, MaxPageSize) : DefaultPageSize;
}

// ── Books ─────────────────────────────────────────────────────────────────────
public class BookEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // The catalogue is library material, not public data, and the API connects to
        // Postgres as an owner role — RLS never applies to this traffic, so these route
        // attributes are the only access control there is. Reads therefore require a user
        // just as /stats always did.
        var group = app.MapGroup("/api/books").RequireAuthorization("AnyUser").WithOpenApi();

        group.MapGet("/", async (
            [FromQuery] string? query,
            [FromQuery] string? genre,
            [FromQuery] string? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            IBookRepository books) =>
        {
            BookStatus? bookStatus = status?.ToLower() switch
            {
                "available"  => BookStatus.Available,
                "checked_out" => BookStatus.CheckedOut,
                _ => null
            };
            var result = await books.SearchAsync(
                new BookSearchRequest(query, genre, bookStatus, Paging.Page(page), Paging.PageSize(pageSize)));
            return Results.Ok(result);
        });

        group.MapGet("/genres", async (IBookRepository books) =>
            Results.Ok(await books.GetGenresAsync()));

        group.MapGet("/stats", async (IBookRepository books) =>
            Results.Ok(await books.GetStatsAsync()));

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

        group.MapPut("/{id:guid}", async (Guid id, UpdateBookRequest req, IBookRepository books,
            IValidator<UpdateBookRequest> validator) =>
        {
            var validation = await validator.ValidateAsync(req);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());
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

            var result = await loans.CheckOutAsync(bookId, userId, email);
            return result.Outcome switch
            {
                CheckOutOutcome.NotFound    => Results.NotFound(),
                CheckOutOutcome.Unavailable => Results.Conflict(new { error = "Book is not available for checkout." }),
                _ => Results.Created($"/api/loans/{result.Loan!.Id}", result.Loan),
            };
        });

        // Check in a book. Members may only return their own loans; librarians may return
        // any loan. A loan belonging to someone else answers 404 exactly as an unknown loan
        // does, so other users' loan IDs are not disclosed.
        group.MapPost("/checkin/{loanId:guid}", async (
            Guid loanId, HttpContext ctx,
            ILoanRepository loans) =>
        {
            var userId = GetUserId(ctx);
            if (userId == Guid.Empty) return Results.Unauthorized();

            var loan = await loans.CheckInAsync(loanId, userId, IsLibrarian(ctx));
            return loan is null ? Results.NotFound() : Results.Ok(loan);
        });

        // My active loans. The Guid.Empty guard matches checkout/checkin: a validated JWT
        // always carries a parseable "sub" today, so this is defence in depth — without it a
        // token that somehow lacked one would query loans for the all-zero user id.
        group.MapGet("/my", async (HttpContext ctx, ILoanRepository loans) =>
        {
            var userId = GetUserId(ctx);
            if (userId == Guid.Empty) return Results.Unauthorized();
            return Results.Ok(await loans.GetActiveLoansByUserAsync(userId));
        });

        // My history
        group.MapGet("/my/history", async (HttpContext ctx, ILoanRepository loans) =>
        {
            var userId = GetUserId(ctx);
            if (userId == Guid.Empty) return Results.Unauthorized();
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

    // Matches the "LibrarianOnly" policy: RoleEnrichmentMiddleware injects "user_role".
    private static bool IsLibrarian(HttpContext ctx) =>
        ctx.User.FindFirst("user_role")?.Value == "librarian";
}

// ── AI ────────────────────────────────────────────────────────────────────────
public class AiEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai").RequireAuthorization("AnyUser").RequireRateLimiting("ai-limit").WithOpenApi();

        // Auto-describe a book. Librarian-only: it is a cataloguing aid, and only librarians
        // can create or edit a book. The group's "AnyUser" is already implied by this policy,
        // so it is not restated here. The UI hides the button for members to match.
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
            if (userId == Guid.Empty) return Results.Unauthorized();
            var history = await loans.GetLoanHistoryByUserAsync(userId, 10);
            var catalog = (await books.SearchAsync(new BookSearchRequest(null, null, null, 1, 50))).Items;
            return Results.Ok(await ai.RecommendBooksAsync(userId, history, catalog));
        });
    }
}

// ── Users ─────────────────────────────────────────────────────────────────────
public class UserEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization("AnyUser").WithOpenApi();

        group.MapGet("/me", async (HttpContext ctx, IUserRepository users) =>
        {
            var userId = Guid.TryParse(
                ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                out var id) ? id : Guid.Empty;

            if (userId == Guid.Empty) return Results.Unauthorized();

            var email = ctx.User.FindFirst("email")?.Value ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";

            // Supabase stores Google profile data in user_metadata as a nested JSON object.
            // ASP.NET Core's JWT handler exposes it as a single claim whose value is the JSON string.
            var name  = ctx.User.FindFirst("name")?.Value
                     ?? ctx.User.FindFirst("full_name")?.Value;

            if (name is null)
            {
                var rawMeta = ctx.User.FindFirst("user_metadata")?.Value;
                if (rawMeta is not null)
                {
                    try
                    {
                        var meta = System.Text.Json.JsonDocument.Parse(rawMeta).RootElement;
                        name = meta.TryGetProperty("full_name", out var fn) ? fn.GetString()
                             : meta.TryGetProperty("name",      out var n)  ? n.GetString()
                             : null;
                    }
                    catch { /* not valid JSON, ignore */ }
                }
            }

            await users.EnsureExistsAsync(userId, email, name);
            var user = await users.GetByIdAsync(userId);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });

    }
}

// ── Health ────────────────────────────────────────────────────────────────────
public class HealthEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
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

public class UpdateBookValidator : AbstractValidator<UpdateBookRequest>
{
    public UpdateBookValidator()
    {
        RuleFor(x => x.Title).MaximumLength(300).When(x => x.Title is not null);
        RuleFor(x => x.Author).MaximumLength(200).When(x => x.Author is not null);
        RuleFor(x => x.Isbn).MaximumLength(20).When(x => x.Isbn is not null);
        RuleFor(x => x.PublishedYear).InclusiveBetween(1000, DateTime.UtcNow.Year + 1).When(x => x.PublishedYear is not null);
    }
}
