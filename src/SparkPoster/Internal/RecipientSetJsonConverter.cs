using System.Text.Json;
using System.Text.Json.Serialization;

namespace SparkPoster.Internal;

/// <summary>
/// Пишет и читает поле <c>recipients</c>, у которого две формы: массив получателей
/// либо объект <c>{ "list_id": "..." }</c>.
/// </summary>
internal sealed class RecipientSetJsonConverter : JsonConverter<RecipientSet>
{
    private const string ListIdProperty = "list_id";

    public override RecipientSet? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.StartArray:
                var items = JsonSerializer.Deserialize(ref reader, SparkPostJsonContext.Default.ListRecipient);
                return items is null ? null : RecipientSet.Inline(items);

            case JsonTokenType.StartObject:
                var listId = ReadListId(ref reader);
                return listId is null
                    ? throw new JsonException("Объект recipients не содержит list_id.")
                    : RecipientSet.StoredList(listId);

            default:
                throw new JsonException($"Поле recipients ожидалось массивом или объектом, встретилось {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, RecipientSet value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        if (value.ListId is { } listId)
        {
            writer.WriteStartObject();
            writer.WriteString(ListIdProperty, listId);
            writer.WriteEndObject();
            return;
        }

        JsonSerializer.Serialize(writer, value.Items ?? [], SparkPostJsonContext.Default.IReadOnlyListRecipient);
    }

    private static string? ReadListId(ref Utf8JsonReader reader)
    {
        string? listId = null;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var isListId = reader.ValueTextEquals(ListIdProperty);
            reader.Read();

            if (isListId)
            {
                listId = reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        return listId;
    }
}
