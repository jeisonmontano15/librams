using Dapper;
using LibraMS.Api.Data;
using System.Security.Claims;

namespace LibraMS.Api.Middleware;

/// <summary>
/// After JWT validation, looks up the user's role from the database
/// and injects it as a "user_role" claim so authorization policies work.
/// </summary>
public class RoleEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, DbConnectionFactory db)
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? ctx.User.FindFirstValue("sub");

            if (Guid.TryParse(userId, out var id))
            {
                using var conn = db.Create();
                var role = await conn.QuerySingleOrDefaultAsync<string>(
                    "SELECT role FROM public.library_users WHERE id = @id", new { id });

                if (!string.IsNullOrEmpty(role))
                {
                    var identity = (ClaimsIdentity)ctx.User.Identity;
                    identity.TryRemoveClaim(identity.FindFirst("user_role"));
                    identity.AddClaim(new Claim("user_role", role));
                }
            }
        }
        await next(ctx);
    }
}
