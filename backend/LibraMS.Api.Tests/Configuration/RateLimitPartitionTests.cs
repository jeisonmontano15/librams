using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using LibraMS.Api;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LibraMS.Api.Tests.Configuration;

/// <summary>
/// BUG-6 regression: the "ai-limit" policy must partition per caller, so one caller
/// exhausting their quota does not affect another. Before the fix the policy was an
/// unpartitioned <c>AddFixedWindowLimiter</c> — a single bucket shared by everyone.
/// </summary>
public class RateLimitPartitionTests
{
    private static HttpContext ContextFor(string? userId, string? ip)
    {
        var ctx = new DefaultHttpContext();
        if (userId is not null)
            ctx.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("sub", userId)], "test"));
        if (ip is not null)
            ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return ctx;
    }

    // ── Partition key ─────────────────────────────────────────────────────────

    [Fact]
    public void PartitionKey_DistinctUsers_AreDistinct()
    {
        var a = RateLimitPartitionKey.For(ContextFor("11111111-1111-1111-1111-111111111111", "10.0.0.1"));
        var b = RateLimitPartitionKey.For(ContextFor("22222222-2222-2222-2222-222222222222", "10.0.0.1"));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void PartitionKey_SameUserOnDifferentIps_IsStable()
    {
        var a = RateLimitPartitionKey.For(ContextFor("11111111-1111-1111-1111-111111111111", "10.0.0.1"));
        var b = RateLimitPartitionKey.For(ContextFor("11111111-1111-1111-1111-111111111111", "10.0.0.2"));

        Assert.Equal(a, b);
    }

    [Fact]
    public void PartitionKey_Unauthenticated_FallsBackToRemoteIp()
    {
        var a = RateLimitPartitionKey.For(ContextFor(null, "10.0.0.1"));
        var b = RateLimitPartitionKey.For(ContextFor(null, "10.0.0.2"));

        Assert.NotEqual(a, b);
        Assert.Contains("10.0.0.1", a);
    }

    [Fact]
    public void PartitionKey_NoUserAndNoIp_FallsBackToAnonymous()
    {
        Assert.Equal("anonymous", RateLimitPartitionKey.For(ContextFor(null, null)));
    }

    // ── Policy behaviour ──────────────────────────────────────────────────────

    /// <summary>
    /// Drives the real "ai-limit" partitioner: one caller spends the full budget, then a
    /// second caller must still be admitted. Against the pre-fix unpartitioned limiter
    /// the second caller was rejected.
    /// </summary>
    [Fact]
    public async Task AiLimit_OneCallerExhaustingQuota_DoesNotAffectAnother()
    {
        using var limiter = BuildLimiter();

        var callerA = ContextFor("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "10.0.0.1");
        // Same IP, different user — proves the partition follows identity, not just address.
        var callerB = ContextFor("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "10.0.0.1");

        for (var i = 0; i < AiRateLimitPolicy.PermitLimit; i++)
        {
            using var lease = await limiter.AcquireAsync(callerA, 1);
            Assert.True(lease.IsAcquired, $"caller A request {i + 1} should be admitted");
        }

        using (var exhausted = await limiter.AcquireAsync(callerA, 1))
            Assert.False(exhausted.IsAcquired, "caller A should be limited once the budget is spent");

        using var otherLease = await limiter.AcquireAsync(callerB, 1);
        Assert.True(otherLease.IsAcquired, "caller B must not be affected by caller A's usage");
    }

    [Fact]
    public async Task AiLimit_SingleCaller_PermitsExactlyTheConfiguredBudget()
    {
        using var limiter = BuildLimiter();
        var caller = ContextFor("cccccccc-cccc-cccc-cccc-cccccccccccc", "10.0.0.5");

        var admitted = 0;
        for (var i = 0; i < AiRateLimitPolicy.PermitLimit + 5; i++)
        {
            using var lease = await limiter.AcquireAsync(caller, 1);
            if (lease.IsAcquired) admitted++;
        }

        Assert.Equal(AiRateLimitPolicy.PermitLimit, admitted);
    }

    [Fact]
    public void AiLimit_PreservesTheDocumentedBudget()
    {
        Assert.Equal(10, AiRateLimitPolicy.PermitLimit);
        Assert.Equal(TimeSpan.FromSeconds(60), AiRateLimitPolicy.Window);
    }

    private static PartitionedRateLimiter<HttpContext> BuildLimiter() =>
        PartitionedRateLimiter.Create<HttpContext, string>(AiRateLimitPolicy.GetPartition);
}
