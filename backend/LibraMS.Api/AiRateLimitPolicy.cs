using System.Threading.RateLimiting;

namespace LibraMS.Api;

/// <summary>
/// The "ai-limit" rate limit policy: 10 requests per 60 seconds, partitioned per caller.
/// </summary>
/// <remarks>
/// Defined here rather than inline in <c>Program.cs</c> so the partitioning behaviour is
/// reachable from tests — <see cref="RateLimiterOptions"/> does not expose registered
/// policies for inspection.
/// </remarks>
public static class AiRateLimitPolicy
{
    public const string Name = "ai-limit";

    public const int PermitLimit = 10;

    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Builds the partition for a request. Keyed per caller so that one caller
    /// exhausting their quota leaves every other caller's budget intact.
    /// </summary>
    public static RateLimitPartition<string> GetPartition(HttpContext ctx) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: RateLimitPartitionKey.For(ctx),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitLimit,
                Window = Window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            });
}
