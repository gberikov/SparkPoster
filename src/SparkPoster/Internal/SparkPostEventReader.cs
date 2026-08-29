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

    /// <summary>
    /// Reads the flat array returned by the Events API. There is no <c>msys</c> wrapper there,
    /// so the category is derived from the event type instead.
    /// </summary>
    public static IReadOnlyList<SparkPostEvent> ReadFlat(JsonNode? results)
    {
        if (results is not JsonArray items)
        {
            return [];
        }

        var events = new List<SparkPostEvent>(items.Count);

        foreach (var item in items)
        {
            if (item is not JsonObject body)
            {
                continue;
            }

            events.Add(ReadByType(body));
        }

        return events;
    }

    private static SparkPostEvent ReadByType(JsonObject body)
    {
        var type = (string?)body["type"];

        return type switch
        {
            "click" or "open" or "initial_open"
                or "amp_click" or "amp_open" or "amp_initial_open"
                => Deserialize(body, SparkPostJsonContext.Default.TrackEvent, string.Empty),
            "generation_failure" or "generation_rejection"
                => Deserialize(body, SparkPostJsonContext.Default.GenerationEvent, string.Empty),
            "list_unsubscribe" or "link_unsubscribe"
                => Deserialize(body, SparkPostJsonContext.Default.UnsubscribeEvent, string.Empty),
            "relay_injection" or "relay_rejection" or "relay_delivery"
                or "relay_tempfail" or "relay_permfail"
                => Deserialize(body, SparkPostJsonContext.Default.RelayEvent, string.Empty),
            "bounce" or "delivery" or "injection" or "delay" or "out_of_band"
                or "policy_rejection" or "spam_complaint"
                => Deserialize(body, SparkPostJsonContext.Default.MessageEvent, string.Empty),
            // An unfamiliar type is reported as unknown rather than forced into MessageEvent:
            // the caller can still read everything through Raw and Extra.
            _ => new UnknownSparkPostEvent
            {
                Category = string.Empty,
                Type = type,
                Raw = body.DeepClone(),
            },
        };
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
            "message_event" => Deserialize(body, SparkPostJsonContext.Default.MessageEvent, category),
            "track_event" => Deserialize(body, SparkPostJsonContext.Default.TrackEvent, category),
            "gen_event" => Deserialize(body, SparkPostJsonContext.Default.GenerationEvent, category),
            "unsubscribe_event" => Deserialize(body, SparkPostJsonContext.Default.UnsubscribeEvent, category),
            "relay_event" => Deserialize(body, SparkPostJsonContext.Default.RelayEvent, category),
            _ => new UnknownSparkPostEvent
            {
                Category = category,
                Type = (string?)body["type"],
                Raw = body.DeepClone(),
            },
        };
    }

    private static SparkPostEvent Deserialize<T>(
        JsonObject body,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        string category)
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
                Category = category,
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
