using Dapper;
using LibraMS.Api.Data;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace LibraMS.Api.Middleware;

/// <summary>
/// After JWT validation, looks up the user's role from the database
/// and injects it as a "user_role" claim so authorization policies work.
/// </summary>
public class RoleEnrichmentMiddleware(RequestDelegate next)
{
    /// <summary>
    /// How long a looked-up role is reused. This ran a query on every authenticated request
    /// — with the dashboard polling stats every 30s that is a round trip per poll per user.
    /// Roles change rarely (a librarian is promoted by hand), so a short TTL removes nearly
    /// all of that while bounding how long a revoked librarian keeps the claim.
    /// </summary>
    private static readonly TimeSpan RoleCacheTtl = TimeSpan.FromSeconds(60);

    public async Task InvokeAsync(HttpContext ctx, DbConnectionFactory db, IMemoryCache cache)
    {
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? ctx.User.FindFirstValue("sub");

            if (Guid.TryParse(userId, out var id))
            {
                var cacheKey = $"user_role:{id}";
                if (!cache.TryGetValue(cacheKey, out string? role))
                {
                    using var conn = db.Create();
                    role = await conn.QuerySingleOrDefaultAsync<string>(
                        "SELECT role FROM public.library_users WHERE id = @id", new { id });

                    // Only a hit is cached. A miss means the profile row has not been created
                    // yet (first login, before /api/users/me runs), and caching that would
                    // leave a new user role-less for the whole TTL.
                    if (!string.IsNullOrEmpty(role))
                        cache.Set(cacheKey, role, RoleCacheTtl);
                }

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
