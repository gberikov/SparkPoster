using System.Text.Json;
using System.Text.Json.Nodes;
using SparkPoster.Webhooks;

namespace SparkPoster.Internal;

/// <summary>
/// Разбирает батч вебхука. Каждый элемент выглядит как
/// <c>{"msys": {"message_event": { ... }}}</c>: дискриминатор здесь — имя внешнего
/// свойства, а не поле внутри объекта, поэтому полиморфизм System.Text.Json не годится
/// и разбор написан руками.
/// </summary>
internal static class SparkPostEventReader
{
    private const string Envelope = "msys";

    public static IReadOnlyList<SparkPostEvent> Read(JsonNode? batch)
    {
        if (batch is null)
        {
            return [];
        }

        // Батч — массив; одиночное событие тоже принимаем, так его удобнее тестировать.
        var items = batch as JsonArray ?? [batch.DeepClone()];
        var events = new List<SparkPostEvent>(items.Count);

        foreach (var item in items)
        {
            var parsed = ReadOne(item);

            if (parsed is not null)
            {
                events.Add(parsed);
            }
        }

        return events;
    }

    private static SparkPostEvent? ReadOne(JsonNode? item)
    {
        if (item is not JsonObject wrapper)
        {
            return null;
        }

        // Батч валидации приходит как [{"msys":{}}] — событий в нём нет.
        if (wrapper[Envelope] is not JsonObject envelope || envelope.Count == 0)
        {
            return null;
        }

        var (category, payload) = (envelope.First().Key, envelope.First().Value);

        if (payload is not JsonObject body)
        {
            return null;
        }

        return category switch
        {
            "message_event" => Deserialize(body, SparkPostJsonContext.Default.MessageEvent),
            "track_event" => Deserialize(body, SparkPostJsonContext.Default.TrackEvent),
            "gen_event" => Deserialize(body, SparkPostJsonContext.Default.GenerationEvent),
            "unsubscribe_event" => Deserialize(body, SparkPostJsonContext.Default.UnsubscribeEvent),
            "relay_event" => Deserialize(body, SparkPostJsonContext.Default.RelayEvent),
            _ => new UnknownSparkPostEvent
            {
                Category = category,
                Type = (string?)body["type"],
                Raw = body.DeepClone(),
            },
        };
    }

    private static SparkPostEvent Deserialize<T>(JsonObject body, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : SparkPostEvent
    {
        try
        {
            return body.Deserialize(typeInfo)
                ?? throw new JsonException("Тело события оказалось пустым.");
        }
        catch (JsonException exception)
        {
            // Одно неразобранное событие не должно ронять весь батч: SparkPost повторил бы
            // его целиком, вместе с уже обработанными событиями.
            return new UnknownSparkPostEvent
            {
                Category = typeof(T).Name,
                Type = (string?)body["type"],
                Raw = body.DeepClone(),
                Extra = new Dictionary<string, JsonElement>
                {
                    ["sparkposter_parse_error"] = JsonSerializer.SerializeToElement(exception.Message, SparkPostJsonContext.Default.String),
                },
            };
        }
    }
}
