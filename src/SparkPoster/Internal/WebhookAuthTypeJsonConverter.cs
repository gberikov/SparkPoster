using System.Text.Json;
using System.Text.Json.Serialization;

namespace SparkPoster.Internal;

/// <summary>
/// Значения <c>auth_type</c> пишутся в нижнем регистре. Отдельный конвертер нужен потому,
/// что на net8.0 политику именования для перечисления нельзя задать атрибутом.
/// </summary>
/// <remarks>
/// Незнакомое значение не роняет разбор, а превращается в
/// <see cref="WebhookAuthType.Unknown"/>: список способов авторизации на стороне
/// SparkPost может пополниться.
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
                $"Способ авторизации {value} нельзя отправить в SparkPost: это значение получено от сервера и библиотеке неизвестно."),
        };

        writer.WriteStringValue(text);
    }
}
