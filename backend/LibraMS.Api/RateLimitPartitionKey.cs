namespace LibraMS.Api;

/// <summary>
/// Resolves the rate-limit partition key for a request.
/// </summary>
public static class RateLimitPartitionKey
{
    /// <summary>
    /// The authenticated user id ("sub" claim) where present, falling back to the
    /// remote IP address, then to a shared "anonymous" bucket. The AI endpoints all
    /// require authentication, so the user id is the normal case — IP alone would
    /// collide for users behind a shared NAT.
    /// </summary>
    public static string For(HttpContext ctx)
    {
        var sub = ctx.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(sub)) return "user:" + sub;

        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrWhiteSpace(ip)) return "ip:" + ip;

        return "anonymous";
    }
}
