using System.Text.Json;
using LibraMS.Api.Models;
using LibraMS.Api.Serialization;
using Xunit;

namespace LibraMS.Api.Tests.Serialization;

/// <summary>
/// The status enums shipped as JSON numbers because no converter was registered, so the
/// frontend's <c>status === 'available'</c> checks were always false and every book rendered
/// as checked out. These assert the wire text directly — the defect type-checked cleanly on
/// both sides, so only the serialized output can catch a regression.
/// </summary>
public class StatusSerializationTests
{
    private static readonly JsonSerializerOptions Options = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        options.Converters.Add(new BookStatusJsonConverter());
        options.Converters.Add(new LoanStatusJsonConverter());
        return options;
    }

    [Theory]
    [InlineData(BookStatus.Available, "available")]
    [InlineData(BookStatus.CheckedOut, "checked_out")]
    public void BookStatus_SerializesAsDatabaseText(BookStatus status, string expected)
    {
        var json = JsonSerializer.Serialize(new Book { Title = "T", Author = "A", Status = status }, Options);

        using var doc = JsonDocument.Parse(json);
        var property = doc.RootElement.GetProperty("status");

        Assert.Equal(JsonValueKind.String, property.ValueKind);
        Assert.Equal(expected, property.GetString());
    }

    [Theory]
    [InlineData(LoanStatus.Active, "active")]
    [InlineData(LoanStatus.Returned, "returned")]
    [InlineData(LoanStatus.Overdue, "overdue")]
    public void LoanStatus_SerializesAsDatabaseText(LoanStatus status, string expected)
    {
        var json = JsonSerializer.Serialize(new Loan { Status = status }, Options);

        using var doc = JsonDocument.Parse(json);
        var property = doc.RootElement.GetProperty("status");

        Assert.Equal(JsonValueKind.String, property.ValueKind);
        Assert.Equal(expected, property.GetString());
    }

    // AiSearchRequest/AiSearchResponse carry BookStatus?, which routes through the same
    // converter rather than falling back to the numeric default.
    [Fact]
    public void NullableBookStatus_RoundTripsAsText()
    {
        var json = JsonSerializer.Serialize(new AiSearchResponse(null, null, BookStatus.CheckedOut, ""), Options);
        Assert.Contains("\"checked_out\"", json);

        var restored = JsonSerializer.Deserialize<AiSearchResponse>(json, Options);
        Assert.Equal(BookStatus.CheckedOut, restored!.Status);
    }

    [Fact]
    public void NullBookStatus_StaysNull()
    {
        var json = JsonSerializer.Serialize(new AiSearchResponse(null, null, null, ""), Options);

        var restored = JsonSerializer.Deserialize<AiSearchResponse>(json, Options);
        Assert.Null(restored!.Status);
    }

    [Theory]
    [InlineData("\"available\"", BookStatus.Available)]
    [InlineData("\"checked_out\"", BookStatus.CheckedOut)]
    public void BookStatus_DeserializesFromDatabaseText(string json, BookStatus expected)
        => Assert.Equal(expected, JsonSerializer.Deserialize<BookStatus>(json, Options));
}
