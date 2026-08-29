using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SparkPoster.Internal;

/// <summary>
/// Reads the moment of an event. SparkPost reports it as Unix epoch seconds — usually as a
/// string (<c>"1460989507"</c>), sometimes as a number; a few fields arrive as ISO 8601.
/// All three shapes are accepted.
/// </summary>
internal sealed class UnixTimestampJsonConverter : JsonConverter<DateTimeOffset?>
{
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.Number:
                return FromUnixSeconds(reader.GetInt64());

            case JsonTokenType.String:
                var text = reader.GetString();

                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
                {
                    return FromUnixSeconds(seconds);
                }

                return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var moment)
                    ? moment
                    : throw Unreadable(text);

            default:
                throw new JsonException($"An event timestamp arrived as {reader.TokenType}.");
        }
    }

    /// <summary>
    /// Both branches go through here: <see cref="DateTimeOffset.FromUnixTimeSeconds"/> throws
    /// <see cref="ArgumentOutOfRangeException"/> outside its range, and a value that far out still
    /// parses as a <see cref="long"/>.
    /// </summary>
    private static DateTimeOffset FromUnixSeconds(long seconds)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw Unreadable(seconds.ToString(CultureInfo.InvariantCulture), exception);
        }
    }

    /// <summary>
    /// A <see cref="JsonException"/> — never a <see cref="FormatException"/> or an
    /// <see cref="ArgumentOutOfRangeException"/> — because only that one is caught by the fallback in
    /// <see cref="SparkPostEventReader"/>. Anything else takes down the whole batch instead of the
    /// single event that is unreadable.
    /// </summary>
    private static JsonException Unreadable(string raw, Exception? inner = null) =>
        new($"An event timestamp arrived as '{raw}', which is neither Unix seconds nor ISO 8601.", inner);

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
    }
}
