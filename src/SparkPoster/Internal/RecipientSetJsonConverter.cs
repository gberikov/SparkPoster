using System.Text.Json;
using System.Text.Json.Serialization;

namespace SparkPoster.Internal;

/// <summary>
/// Reads and writes the <c>recipients</c> field, which has two shapes: an array of
/// recipients or an object of the form <c>{ "list_id": "..." }</c>.
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
                    ? throw new JsonException("The recipients object carries no list_id.")
                    : RecipientSet.StoredList(listId);

            default:
                throw new JsonException($"The recipients field was expected to be an array or an object but was {reader.TokenType}.");
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
