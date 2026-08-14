using LibraMS.Api;
using Xunit;

namespace LibraMS.Api.Tests.Configuration;

/// <summary>
/// BUG-8 regression: signing keys must be fetched once and cached, not refetched on
/// every token validation. Before the fix the resolver built a fresh
/// <c>HttpClient</c> and blocked on a JWKS fetch per validated token.
/// </summary>
public class SigningKeyCacheTests
{
    // A minimal Supabase-shaped JWKS: one EC key published with key_ops ["verify"],
    // which is precisely the shape GetSigningKeys() skips and the manual ECDsa
    // construction exists to handle.
    private const string Jwks = """
        {"keys":[{
          "kty":"EC","crv":"P-256","kid":"test-key-1","alg":"ES256","key_ops":["verify"],
          "x":"f83OJ3D2xF1Bg8vub9tLe1gHMzV76e8Tus9uPHvRVEU",
          "y":"x_FEzRu9m36HLN_tue659LNpXW6pCyStikYjKIWI5a0"
        }]}
        """;

    private static (SupabaseSigningKeys Keys, Func<int> FetchCount) Build(
        string json = Jwks, Func<DateTimeOffset>? now = null)
    {
        var fetches = 0;
        var keys = new SupabaseSigningKeys(
            _ =>
            {
                Interlocked.Increment(ref fetches);
                return Task.FromResult(json);
            },
            now);
        return (keys, () => fetches);
    }

    /// <summary>
    /// The regression assertion: many validations, exactly one network fetch.
    /// </summary>
    [Fact]
    public void RepeatedValidations_TriggerASingleFetch()
    {
        var (keys, fetchCount) = Build();

        for (var i = 0; i < 50; i++)
            Assert.NotEmpty(keys.Get());

        Assert.Equal(1, fetchCount());
    }

    [Fact]
    public void Get_ParsesSupabaseVerifyOnlyEcKeys()
    {
        var (keys, _) = Build();

        var resolved = keys.Get();

        var key = Assert.Single(resolved);
        Assert.Equal("test-key-1", key.KeyId);
        Assert.IsType<Microsoft.IdentityModel.Tokens.ECDsaSecurityKey>(key);
    }

    [Fact]
    public void Get_PastTheRefreshInterval_FetchesAgain()
    {
        var clock = DateTimeOffset.UnixEpoch;
        var (keys, fetchCount) = Build(now: () => clock);

        keys.Get();
        keys.Get();
        Assert.Equal(1, fetchCount());

        clock += SupabaseSigningKeys.RefreshInterval + TimeSpan.FromMinutes(1);
        keys.Get();

        Assert.Equal(2, fetchCount());
    }

    /// <summary>
    /// A JWKS outage must not reject otherwise valid tokens: the last known-good key set
    /// keeps being served until a later refresh succeeds.
    /// </summary>
    [Fact]
    public void Get_WhenARefreshFails_KeepsServingTheLastKnownGoodKeys()
    {
        var clock = DateTimeOffset.UnixEpoch;
        var fail = false;
        var keys = new SupabaseSigningKeys(
            _ => fail
                ? Task.FromException<string>(new HttpRequestException("jwks down"))
                : Task.FromResult(Jwks),
            () => clock);

        Assert.Single(keys.Get());

        fail = true;
        clock += SupabaseSigningKeys.RefreshInterval + TimeSpan.FromMinutes(1);

        Assert.Single(keys.Get());
    }

    [Fact]
    public void Get_ConcurrentCallers_StillFetchOnce()
    {
        var (keys, fetchCount) = Build();

        Parallel.For(0, 32, _ => Assert.NotEmpty(keys.Get()));

        Assert.Equal(1, fetchCount());
    }
}
