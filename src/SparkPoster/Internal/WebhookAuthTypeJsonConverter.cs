using System.Text.Json;
using System.Text.Json.Serialization;

namespace SparkPoster.Internal;

/// <summary>
/// The <c>auth_type</c> values are lower case. A dedicated converter is needed because on
/// net8.0 an enum naming policy cannot be set through an attribute.
/// </summary>
/// <remarks>
/// An unfamiliar value does not break parsing but turns into
/// <see cref="WebhookAuthType.Unknown"/>: SparkPost may add authentication schemes.
/// </remarks>
internal sealed class WebhookAuthTypeJsonConverter : JsonConverter<WebhookAuthType>
{
    public override WebhookAuthType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "none" => WebhookAuthType.None,
            "basic" => WebhookAuthType.Basic,
            "oauth2" => WebhookAuthType.OAuth2,
            _ => WebhookAuthType.Unknown,
        };

    public override void Write(Utf8JsonWriter writer, WebhookAuthType value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var text = value switch
        {
            WebhookAuthType.None => "none",
            WebhookAuthType.Basic => "basic",
            WebhookAuthType.OAuth2 => "oauth2",
            _ => throw new JsonException(
                $"The {value} authentication scheme cannot be sent to SparkPost: the value came from the server and this library does not know it."),
        };

        writer.WriteStringValue(text);
    }
}
