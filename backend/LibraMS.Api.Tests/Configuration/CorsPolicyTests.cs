using LibraMS.Api.Tests.Endpoints;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace LibraMS.Api.Tests.Configuration;

/// <summary>
/// BUG-7 regression: the default CORS policy lists wildcard origins
/// ("https://*.vercel.app"), which are compared as literal strings unless
/// <c>SetIsOriginAllowedToAllowWildcardSubdomains()</c> is set. Before the fix every
/// wildcard-matching origin was refused.
/// </summary>
public class CorsPolicyTests(LibramsWebApplicationFactory factory)
    : IClassFixture<LibramsWebApplicationFactory>
{
    private CorsPolicy DefaultPolicy()
    {
        var options = factory.Services.GetRequiredService<IOptions<CorsOptions>>().Value;
        var policy = options.GetPolicy(options.DefaultPolicyName);
        Assert.NotNull(policy);
        return policy!;
    }

    [Theory]
    [InlineData("https://libra-ms.vercel.app")]
    [InlineData("https://libra-ms-git-master.vercel.app")]
    [InlineData("https://libra-ms.azurestaticapps.net")]
    public void WildcardMatchingOrigin_IsAllowed(string origin)
    {
        Assert.True(DefaultPolicy().IsOriginAllowed(origin),
            $"{origin} should match a configured wildcard entry");
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("https://vercel.app.evil.com")]
    [InlineData("http://libra-ms.vercel.app")] // wrong scheme
    public void UnrelatedOrigin_IsRefused(string origin)
    {
        Assert.False(DefaultPolicy().IsOriginAllowed(origin),
            $"{origin} must not be permitted by the CORS policy");
    }

    [Fact]
    public void ConfiguredFrontendOrigin_IsStillAllowed()
    {
        // The explicit Frontend:Url entry must keep working alongside the wildcards.
        // The factory pins Frontend:Url to this origin.
        Assert.True(DefaultPolicy().IsOriginAllowed("http://localhost:5173"));
    }
}
