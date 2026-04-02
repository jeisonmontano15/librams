using System.Text.Json;
using LibraMS.Api.Models;

namespace LibraMS.Api.Services;

public interface IOpenLibraryService
{
    Task<OpenLibraryBook?> LookupByIsbnAsync(string isbn);
}

public record OpenLibraryBook(string Title, string Author, int? Year, string? CoverUrl, string? Description);

public class OpenLibraryService(HttpClient http, ILogger<OpenLibraryService> logger) : IOpenLibraryService
{
    public async Task<OpenLibraryBook?> LookupByIsbnAsync(string isbn)
    {
        try
        {
            var url = $"https://openlibrary.org/api/books?bibkeys=ISBN:{isbn}&jscmd=details&format=json";
            var response = await http.GetStringAsync(url);
            var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;
            var key = $"ISBN:{isbn}";
            if (!root.TryGetProperty(key, out var entry)) return null;

            var details = entry.GetProperty("details");
            var title = details.TryGetProperty("title", out var t) ? t.GetString() : null;
            var authors = details.TryGetProperty("authors", out var a)
                ? string.Join(", ", a.EnumerateArray().Select(x => x.TryGetProperty("name", out var n) ? n.GetString() : ""))
                : null;
            var year = details.TryGetProperty("publish_date", out var y) ? ParseYear(y.GetString()) : null;
            var coverId = details.TryGetProperty("covers", out var c) ? c[0].GetInt32() : (int?)null;
            var coverUrl = coverId.HasValue ? $"https://covers.openlibrary.org/b/id/{coverId}-M.jpg" : null;
            var description = details.TryGetProperty("description", out var d)
                ? (d.ValueKind == JsonValueKind.Object ? d.GetProperty("value").GetString() : d.GetString())
                : null;

            if (title is null) return null;
            return new OpenLibraryBook(title, authors ?? "Unknown", year, coverUrl, description);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OpenLibrary lookup failed for ISBN {Isbn}", isbn);
            return null;
        }
    }

    private static int? ParseYear(string? dateStr)
    {
        if (dateStr is null) return null;
        var parts = dateStr.Split(' ', ',');
        foreach (var part in parts)
            if (int.TryParse(part.Trim(), out var year) && year > 1000 && year < 2100)
                return year;
        return null;
    }
}
