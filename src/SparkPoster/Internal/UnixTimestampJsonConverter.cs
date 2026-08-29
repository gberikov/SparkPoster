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
                return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64());

            case JsonTokenType.String:
                var text = reader.GetString();

                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(seconds);
                }

                // A JsonException — not the FormatException DateTimeOffset.Parse would throw — so that
                // the reader's fallback catches it and reports the event as unknown instead of taking
                // down the whole batch.
                return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var moment)
                    ? moment
                    : throw new JsonException($"An event timestamp arrived as '{text}', which is neither Unix seconds nor ISO 8601.");

            default:
                throw new JsonException($"An event timestamp arrived as {reader.TokenType}.");
        }
    }

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
