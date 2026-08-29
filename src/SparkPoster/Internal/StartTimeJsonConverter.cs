using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SparkPoster.Internal;

/// <summary>
/// Writes <c>start_time</c> the way SparkPost documents it — <c>YYYY-MM-DDTHH:MM:SS+-HH:MM</c>, whole
/// seconds, the caller's offset. The default converter appends fractional seconds whenever they are
/// present, and <see cref="DateTimeOffset.UtcNow"/> always has them.
/// </summary>
internal sealed class StartTimeJsonConverter : JsonConverter<DateTimeOffset?>
{
    private const string Format = "yyyy-MM-ddTHH:mm:sszzz";

    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTimeOffset();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString(Format, CultureInfo.InvariantCulture));
    }
}
