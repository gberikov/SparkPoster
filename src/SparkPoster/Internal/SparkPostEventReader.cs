using System.Text.Json;
using System.Text.Json.Nodes;
using SparkPoster.Webhooks;

namespace SparkPoster.Internal;

/// <summary>
/// Parses a webhook batch. Every element looks like
/// <c>{"msys": {"message_event": { ... }}}</c>: the discriminator is the name of the outer
/// property rather than a field inside the object, so System.Text.Json polymorphism does not
/// apply and the dispatch is written by hand.
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

        // A batch is an array; a single event is accepted too, which makes testing easier.
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

        // The validation batch arrives as [{"msys":{}}] and carries no events.
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
                ?? throw new JsonException("The event body turned out to be empty.");
        }
        catch (JsonException exception)
        {
            // One unparsable event must not take down the whole batch: SparkPost would resend
            // it in full, together with the events that were already handled.
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
