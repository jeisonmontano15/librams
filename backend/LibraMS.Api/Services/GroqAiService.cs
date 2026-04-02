using LibraMS.Api.Models;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;

namespace LibraMS.Api.Services;

/// <summary>
/// Implements all three AI features using Groq's free OpenAI-compatible API.
/// Groq free tier: no credit card required, ~30 RPM, Llama 3.3 70B.
/// Sign up at https://console.groq.com — API keys are free.
/// </summary>
public class GroqAiService(IConfiguration config, ILogger<GroqAiService> logger) : IAiService
{
    // Groq is OpenAI-compatible: swap base URL + model name, keep same SDK
    private readonly ChatClient _chat = new ChatClient(
        model: "llama-3.3-70b-versatile",
        credential: new ApiKeyCredential(
            config["Groq:ApiKey"] ?? throw new InvalidOperationException("Groq:ApiKey not configured")),
        options: new OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }
    );

    // ── Feature 1: Auto-describe a book ──────────────────────────────────────
    public async Task<AiDescribeResponse> DescribeBookAsync(AiDescribeRequest req)
    {
        var prompt = $"""
            You are a library catalogue assistant. Given a book's metadata, write a compelling
            2-3 sentence description for a library catalogue and suggest 1-2 genre tags.

            Book: "{req.Title}" by {req.Author}{(req.Isbn != null ? $" (ISBN: {req.Isbn})" : "")}

            Respond ONLY with valid JSON — no markdown fences, no preamble, nothing else:
            {{"description":"...","suggestedGenres":["Genre1","Genre2"]}}
            """;

        var response = await CallGroqAsync(prompt);
        try
        {
            var result = JsonSerializer.Deserialize<AiDescribeResponse>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new AiDescribeResponse("No description available.", []);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse AI describe response: {Raw}", response);
            return new AiDescribeResponse(response, []);
        }
    }

    // ── Feature 2: Natural language search ───────────────────────────────────
    public async Task<AiSearchResponse> ParseNaturalSearchAsync(
        AiSearchRequest req, IEnumerable<string> availableGenres)
    {
        var genreList = string.Join(", ", availableGenres);
        var prompt = $"""
            You are a library search assistant. Parse the user's natural language query into
            structured search filters. Return only what the user explicitly asked for.

            Available genres in the library: {genreList}
            User query: "{req.NaturalQuery}"

            Respond ONLY with valid JSON — no markdown fences, no preamble:
            {{
              "query": "keyword to search in title/author/description, or null if not applicable",
              "genre": "exactly one genre from the list above, or null",
              "status": "available" | "checked_out" | null,
              "explanation": "one short sentence explaining what you understood"
            }}
            """;

        var response = await CallGroqAsync(prompt);
        try
        {
            var parsed = JsonSerializer.Deserialize<JsonElement>(response);
            var statusStr = parsed.TryGetProperty("status", out var s) ? s.GetString() : null;
            BookStatus? status = statusStr switch
            {
                "available"   => BookStatus.Available,
                "checked_out" => BookStatus.CheckedOut,
                _             => null
            };
            return new AiSearchResponse(
                parsed.TryGetProperty("query",       out var q) ? q.GetString() : null,
                parsed.TryGetProperty("genre",       out var g) ? g.GetString() : null,
                status,
                parsed.TryGetProperty("explanation", out var e) ? e.GetString() ?? "" : ""
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse AI search response: {Raw}", response);
            return new AiSearchResponse(req.NaturalQuery, null, null, "Searching by keyword.");
        }
    }

    // ── Feature 3: Personalised recommendations ───────────────────────────────
    public async Task<AiRecommendResponse> RecommendBooksAsync(
        Guid userId, IEnumerable<Loan> loanHistory, IEnumerable<Book> catalog)
    {
        var history  = loanHistory.Take(10).Select(l =>
            $"- \"{l.Book?.Title}\" by {l.Book?.Author} [{l.Book?.Genre}]");
        var available = catalog.Where(b => b.Status == BookStatus.Available).Take(30).Select(b =>
            $"- [{b.Id}] \"{b.Title}\" by {b.Author} [{b.Genre}]");

        var prompt = $"""
            You are a personalised library recommendation engine.

            User's recent reading history:
            {string.Join("\n", history.DefaultIfEmpty("(no history yet)"))}

            Available books in the library (with their IDs):
            {string.Join("\n", available)}

            Recommend exactly 3 books. Prefer books from the available list and include their
            exact ID when recommending them. For external suggestions not in the list, set
            matchedBookId to null.

            Respond ONLY with valid JSON — no markdown fences, no preamble:
            {{"recommendations":[{{"title":"...","author":"...","reason":"one sentence","matchedBookId":"uuid or null"}}]}}
            """;

        var response = await CallGroqAsync(prompt);
        try
        {
            var result = JsonSerializer.Deserialize<AiRecommendResponse>(response,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new AiRecommendResponse([]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse AI recommend response: {Raw}", response);
            return new AiRecommendResponse([]);
        }
    }

    // ── Shared Groq call ─────────────────────────────────────────────────────
    private async Task<string> CallGroqAsync(string prompt)
    {
        var messages = new List<ChatMessage> { ChatMessage.CreateUserMessage(prompt) };
        var completion = await _chat.CompleteChatAsync(messages);
        return completion.Value.Content[0].Text;
    }
}
