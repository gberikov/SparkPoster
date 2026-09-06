using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
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
    /// so the event type picks the CLR type and the category is left empty.
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

    private static SparkPostEvent ReadByType(JsonObject body, string category = "")
    {
        var type = ReadType(body);

        return type switch
        {
            "click" or "open" or "initial_open"
                or "amp_click" or "amp_open" or "amp_initial_open"
                when category is "" or "track_event"
                => Deserialize(body, SparkPostJsonContext.Default.TrackEvent, category),
            "generation_failure" or "generation_rejection"
                when category is "" or "gen_event"
                => Deserialize(body, SparkPostJsonContext.Default.GenerationEvent, category),
            "list_unsubscribe" or "link_unsubscribe"
                when category is "" or "unsubscribe_event"
                => Deserialize(body, SparkPostJsonContext.Default.UnsubscribeEvent, category),
            "relay_injection" or "relay_rejection" or "relay_delivery"
                or "relay_tempfail" or "relay_permfail"
                when category is "" or "relay_event"
                => Deserialize(body, SparkPostJsonContext.Default.RelayEvent, category),
            "bounce" or "delivery" or "injection" or "delay" or "out_of_band"
                or "policy_rejection" or "spam_complaint" or "sms_status"
                when category is "" or "message_event"
                => Deserialize(body, SparkPostJsonContext.Default.MessageEvent, category),
            "ab_test_completed" or "ab_test_cancelled"
                when category is "" or "ab_test_event"
                => Deserialize(body, SparkPostJsonContext.Default.AbTestEvent, category),
            "success" or "error"
                when category is "" or "ingest_event"
                => Deserialize(body, SparkPostJsonContext.Default.IngestEvent, category),
            // An unfamiliar type is reported as unknown rather than forced into MessageEvent:
            // the common fields are still read, and everything is available through Raw.
            _ => Unknown(body, category, parseError: null),
        };
    }

    private static SparkPostEvent? ReadOne(JsonNode? item)
    {
        // Anything that is not {"msys": {...}} is not a SparkPost event at all. Surfacing that as a
        // JsonException (→ 400 from the endpoint) beats silently answering 200 to a body that would
        // never have carried an event.
        if (item is not JsonObject wrapper || wrapper[Envelope] is not JsonObject envelope)
        {
            throw new JsonException($"A batch element is not a SparkPost event: it has no '{Envelope}' wrapper.");
        }

        // The validation batch arrives as [{"msys":{}}] and carries no events.
        if (envelope.Count == 0)
        {
            return null;
        }

        var (category, payload) = (envelope.First().Key, envelope.First().Value);

        if (payload is not JsonObject body)
        {
            throw new JsonException($"The '{category}' event body is not an object.");
        }

        return ReadByType(body, category);
    }

    private static SparkPostEvent Deserialize<T>(JsonObject body, JsonTypeInfo<T> typeInfo, string category)
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
            return Unknown(body, category, exception.Message);
        }
    }

    /// <summary>
    /// The common fields are read through the base model so that EventId, Timestamp and the rest
    /// stay available on an unknown event. If a common field is malformed, isolate it from the
    /// other fields; the original value remains available in Raw.
    /// </summary>
    private static UnknownSparkPostEvent Unknown(JsonObject body, string category, string? parseError)
    {
        UnknownSparkPostEvent? common = null;

        try
        {
            common = body.Deserialize(SparkPostJsonContext.Default.UnknownSparkPostEvent);
        }
        catch (JsonException exception)
        {
            parseError ??= exception.Message;
            common = ReadValidCommonFields(body);
        }

        return (common ?? new UnknownSparkPostEvent { Type = ReadType(body) }) with
        {
            Category = category,
            Raw = body.DeepClone(),
            ParseError = parseError,
        };
    }

    private static UnknownSparkPostEvent ReadValidCommonFields(JsonObject body)
    {
        var validFields = new JsonObject();
        var singleField = new JsonObject();
        var typeInfo = SparkPostJsonContext.Default.UnknownSparkPostEvent;

        // This slower path is only needed when the common model itself failed. Reuse its
        // generated contract and property converters, including extension data, so new base
        // properties automatically participate without reflection or a second field mapping.
        foreach (var (name, value) in body)
        {
            singleField.Clear();
            singleField.Add(name, value?.DeepClone());

            try
            {
                _ = singleField.Deserialize(typeInfo);
            }
            catch (JsonException)
            {
                continue;
            }

            validFields.Add(name, value?.DeepClone());
        }

        return validFields.Deserialize(typeInfo)!;
    }

    /// <summary>
    /// The discriminator without an <see cref="InvalidOperationException"/>: a cast of a number
    /// or an object to string throws, and that exception is not the one the fallback catches.
    /// </summary>
    private static string? ReadType(JsonObject body) =>
        body["type"] is JsonValue value && value.TryGetValue<string>(out var type) ? type : null;
}
