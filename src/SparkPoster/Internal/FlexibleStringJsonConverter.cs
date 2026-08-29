using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SparkPoster.Internal;

/// <summary>
/// Читает строковое поле, которое SparkPost иногда отдаёт числом или булевым значением.
/// </summary>
/// <remarks>
/// Пример из документации: код ошибки транзакции приходит строкой (<c>"1400"</c>),
/// а код ошибки вебхука — числом (<c>400</c>). Без этого конвертера разбор тела ошибки
/// падал бы, и до вызывающего не доехало бы ни одного сообщения.
/// </remarks>
internal sealed class FlexibleStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var integer)
                ? integer.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            _ => throw new JsonException($"Строковое поле пришло как {reader.TokenType}."),
        };

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value);
    }
}
