using System.Threading.RateLimiting;
using Carter;
using Dapper;
using LibraMS.Api;
using FluentValidation;
using LibraMS.Api.Data;
using LibraMS.Api.Middleware;
using LibraMS.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

// ── Auth ──────────────────────────────────────────────────────────────────────
var supabaseUrl = builder.Configuration["Supabase:Url"]
    ?? throw new InvalidOperationException("Supabase:Url not configured");

// One HttpClient and one cached key set for the process — the resolver below runs on
// every authenticated request, so a per-validation client and fetch would be a network
// round trip per request plus a socket exhaustion risk.
var jwksHttpClient = new System.Net.Http.HttpClient();
var signingKeys = SupabaseSigningKeys.ForSupabase(supabaseUrl, jwksHttpClient);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = null;
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = supabaseUrl + "/auth/v1",
            ValidateAudience = true,
            ValidAudience = "authenticated",
            ValidateLifetime = true,
            ValidAlgorithms = new[] { "RS256", "ES256" },
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
                signingKeys.Get(),
        };
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Log.Error("JWT auth failed: {Error}", ctx.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("LibrarianOnly", policy =>
        policy.RequireClaim("user_role", "librarian"));
    options.AddPolicy("AnyUser", policy =>
        policy.RequireAuthenticatedUser());
});

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<ILoanRepository, LoanRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAiService, GroqAiService>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddHttpClient<IOpenLibraryService, OpenLibraryService>();
builder.Services.AddCarter();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddRateLimiter(options =>
{
    // Partitioned per caller: authenticated user id, falling back to remote IP.
    // An unpartitioned limiter would let one caller exhaust the quota for everyone.
    options.AddPolicy(AiRateLimitPolicy.Name, AiRateLimitPolicy.GetPartition);
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await ctx.HttpContext.Response.WriteAsJsonAsync(new { error = "Too many requests. Please try again later." });
    };
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(
                builder.Configuration["Frontend:Url"] ?? "http://localhost:5173",
                "https://*.vercel.app",
                "https://*.azurestaticapps.net")
              // Without this, the "*.vercel.app" entries above are compared as literal
              // strings and never match a real origin.
              .SetIsOriginAllowedToAllowWildcardSubdomains()
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseExceptionHandler(exApp =>
    exApp.Run(async ctx =>
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred." });
    }));

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<RoleEnrichmentMiddleware>();
app.UseAuthorization();

app.UseSwagger(options =>
{
    options.RouteTemplate = "/openapi/{documentName}.json";
});
app.MapScalarApiReference("/docs");
app.MapCarter();

app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
