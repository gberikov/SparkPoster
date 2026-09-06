using System.Text.Json;
using System.Text.Json.Serialization;

namespace SparkPoster.Internal;

/// <summary>
/// Reads the <c>from</c> of a template, which SparkPost documents as "string or object":
/// <c>"{{ friendly_from }} &lt;team@example.com&gt;"</c> is as legitimate as
/// <c>{"email": "...", "name": "..."}</c>.
/// </summary>
/// <remarks>
/// A string lands in <see cref="Address.Email"/> verbatim — no address parsing, because the
/// string may be a template expression. On the way out an <see cref="Address"/> with only
/// <see cref="Address.Email"/> set is written back as a string, so a template read with a
/// string <c>from</c> round-trips unchanged; anything with a name or a header-to is an object,
/// exactly as before.
/// </remarks>
internal sealed class TemplateFromJsonConverter : JsonConverter<Address?>
{
    public override Address? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => new Address { Email = reader.GetString()! },
            JsonTokenType.StartObject => JsonSerializer.Deserialize(ref reader, SparkPostJsonContext.Default.Address),
            _ => throw new JsonException($"A template 'from' arrived as {reader.TokenType}; a string or an object was expected."),
        };

    public override void Write(Utf8JsonWriter writer, Address? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
        }
        else if (value is { Name: null, HeaderTo: null })
        {
            writer.WriteStringValue(value.Email);
        }
        else
        {
            JsonSerializer.Serialize(writer, value, SparkPostJsonContext.Default.Address);
        }
    }
}
