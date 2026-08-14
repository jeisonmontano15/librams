using System.Text.Json;
using System.Text.Json.Serialization;
using LibraMS.Api.Models;

namespace LibraMS.Api.Serialization;

/// <summary>
/// Serializes <see cref="BookStatus"/> as the same snake_case text the database and the
/// frontend use. Without a converter System.Text.Json writes the enum's numeric value, so
/// every client-side <c>status === 'available'</c> comparison silently failed and the whole
/// catalogue rendered as checked out.
/// </summary>
/// <remarks>
/// The default <see cref="JsonStringEnumConverter"/> would emit the member name
/// (<c>CheckedOut</c>), not <c>checked_out</c>. Routing through
/// <see cref="BookStatusText"/> keeps the wire spelling and the stored spelling defined in
/// exactly one place.
/// </remarks>
public sealed class BookStatusJsonConverter : JsonConverter<BookStatus>
{
    public override BookStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => BookStatusText.Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, BookStatus value, JsonSerializerOptions options)
        => writer.WriteStringValue(BookStatusText.ToText(value));
}

/// <summary>
/// Serializes <see cref="LoanStatus"/> as its database text. See
/// <see cref="BookStatusJsonConverter"/> for why the built-in string converter is not used.
/// </summary>
public sealed class LoanStatusJsonConverter : JsonConverter<LoanStatus>
{
    public override LoanStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => LoanStatusText.Parse(reader.GetString());

    public override void Write(Utf8JsonWriter writer, LoanStatus value, JsonSerializerOptions options)
        => writer.WriteStringValue(LoanStatusText.ToText(value));
}
