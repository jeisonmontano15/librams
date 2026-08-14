using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace LibraMS.Api;

/// <summary>
/// Caches Supabase's JWKS signing keys so token validation performs no network call in
/// the normal case.
/// </summary>
/// <remarks>
/// Before this existed, <c>IssuerSigningKeyResolver</c> built a fresh <c>HttpClient</c>
/// and blocked on a JWKS fetch for <em>every</em> validated token — a round trip per
/// authenticated request, plus socket exhaustion risk under load.
/// <para>
/// Defined here rather than inline in <c>Program.cs</c> so the caching behaviour is
/// reachable from tests, matching <see cref="AiRateLimitPolicy"/>.
/// </para>
/// </remarks>
public sealed class SupabaseSigningKeys
{
    /// <summary>How long a successfully fetched key set is served before a refresh.</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(1);

    private readonly Func<CancellationToken, Task<string>> _fetchJwks;
    private readonly Func<DateTimeOffset> _now;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IReadOnlyList<SecurityKey> _keys = [];
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public SupabaseSigningKeys(
        Func<CancellationToken, Task<string>> fetchJwks,
        Func<DateTimeOffset>? now = null)
    {
        _fetchJwks = fetchJwks;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Builds an instance that fetches from Supabase's well-known JWKS endpoint using a
    /// single long-lived <see cref="HttpClient"/>.
    /// </summary>
    public static SupabaseSigningKeys ForSupabase(string supabaseUrl, HttpClient client)
    {
        var jwksUrl = supabaseUrl + "/auth/v1/.well-known/jwks.json";
        return new SupabaseSigningKeys(ct => client.GetStringAsync(jwksUrl, ct));
    }

    /// <summary>
    /// The cached signing keys, refreshing them if the cache has expired. Returns the
    /// last known-good key set if a refresh fails, so a transient JWKS outage does not
    /// reject otherwise valid tokens.
    /// </summary>
    public IReadOnlyList<SecurityKey> Get()
    {
        if (_now() < _expiresAt) return _keys;

        _gate.Wait();
        try
        {
            // Another caller may have refreshed while this one waited on the gate.
            if (_now() < _expiresAt) return _keys;

            try
            {
                var json = _fetchJwks(CancellationToken.None).GetAwaiter().GetResult();
                _keys = ParseKeys(json);
                _expiresAt = _now() + RefreshInterval;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to fetch JWKS from Supabase");
                // Keep serving the previous key set rather than failing every request;
                // the next validation past the expiry retries the fetch.
            }

            return _keys;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Builds ECDsa keys by hand: <c>JsonWebKeySet.GetSigningKeys()</c> skips keys with
    /// <c>key_ops=["verify"]</c>, which is exactly how Supabase publishes its public keys.
    /// </summary>
    private static IReadOnlyList<SecurityKey> ParseKeys(string json)
    {
        var keySet = new JsonWebKeySet(json);
        var keys = new List<SecurityKey>();

        foreach (var jwk in keySet.Keys.Where(k => k.Kty == "EC"))
        {
            var ecParams = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = Base64UrlEncoder.DecodeBytes(jwk.X),
                    Y = Base64UrlEncoder.DecodeBytes(jwk.Y),
                }
            };
            keys.Add(new ECDsaSecurityKey(ECDsa.Create(ecParams)) { KeyId = jwk.Kid });
        }

        Serilog.Log.Information("JWKS resolved {Count} EC keys", keys.Count);
        return keys;
    }
}
