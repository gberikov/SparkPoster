using System.Net;
using System.Text.Json.Nodes;
using SparkPoster.Webhooks;

namespace SparkPoster.Tests;

/// <summary>
/// Every official sample event has to land in its typed model. The hand-trimmed samples in the
/// other test classes once left out <c>initial_pixel</c>, and with it the boolean that turned
/// every open and click into an <see cref="UnknownSparkPostEvent"/> — so these run on the full
/// payloads.
/// </summary>
/// <remarks>
/// Source, captured 2026-09-06 from the SparkPost developer site's static query data:
/// <c>Fixtures/webhook-events.json</c> is <c>data.allWebhookEvent.edges[].node.sample</c> of
/// <c>https://developers.sparkpost.com/page-data/sq/d/3859448388.json</c>, and
/// <c>Fixtures/message-events.json</c> wraps <c>data.allMessageEvent.edges[].node.sample</c> of
/// <c>https://developers.sparkpost.com/page-data/sq/d/1319884646.json</c> in an Events API page.
/// The query hashes change with the site; the current ones are listed under
/// <c>staticQueryHashes</c> in <c>page-data/api/webhooks/page-data.json</c> and
/// <c>page-data/api/events/page-data.json</c>.
/// </remarks>
public sealed class OfficialSamplesTests
{
    private static readonly string FixturesDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");

    private static readonly Dictionary<string, Type> ExpectedWebhookTypes = new(StringComparer.Ordinal)
    {
        ["message_event"] = typeof(MessageEvent),
        ["track_event"] = typeof(TrackEvent),
        ["gen_event"] = typeof(GenerationEvent),
        ["unsubscribe_event"] = typeof(UnsubscribeEvent),
        ["relay_event"] = typeof(RelayEvent),
        ["ab_test_event"] = typeof(AbTestEvent),
        ["ingest_event"] = typeof(IngestEvent),
    };

    [Fact]
    public void Every_official_webhook_sample_is_read_into_its_typed_model()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDirectory, "webhook-events.json"));
        var samples = JsonNode.Parse(json)!.AsArray();

        var events = SparkPostWebhookParser.Parse(json);

        Assert.Equal(27, samples.Count);
        Assert.Equal(samples.Count, events.Count);

        for (var i = 0; i < samples.Count; i++)
        {
            var category = samples[i]!["msys"]!.AsObject().First().Key;
            var expected = ExpectedWebhookTypes[category];

            Assert.True(
                expected.IsInstanceOfType(events[i]),
                $"Sample {i} ({category}/{events[i].Type}) was read as {events[i].GetType().Name}"
                + (events[i] is UnknownSparkPostEvent { ParseError: { } error } ? $": {error}" : "."));
            // The sms_status sample carries no event_id; every other one does.
            Assert.NotNull(events[i].Timestamp);
        }
    }

    [Fact]
    public async Task Every_official_events_api_sample_is_read_into_its_typed_model()
    {
        var json = await File.ReadAllTextAsync(
            Path.Combine(FixturesDirectory, "message-events.json"),
            TestContext.Current.CancellationToken);
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, json);
        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });

        var page = await client.Events.GetPageAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(18, page.Events.Count);
        Assert.All(page.Events, e =>
        {
            Assert.IsNotType<UnknownSparkPostEvent>(e);
            Assert.NotNull(e.Timestamp);
        });
    }

    [Fact]
    public void Documented_fields_do_not_leak_into_extra()
    {
        // Everything the samples send is typed, except the SMS-specific fields of sms_status:
        // SMPP is an Enterprise feature and those fields stay in Extra on purpose.
        var json = File.ReadAllText(Path.Combine(FixturesDirectory, "webhook-events.json"));

        var events = SparkPostWebhookParser.Parse(json);

        var leaked = events
            .Where(e => e.Extra is not null)
            .SelectMany(e => e.Extra!.Keys.Select(key => (e.Type, key)))
            .Where(pair => !pair.key.StartsWith("sms_", StringComparison.Ordinal)
                && pair.key is not ("stat_type" or "stat_state" or "dr_latency"))
            .Distinct()
            .ToList();

        Assert.Empty(leaked);
    }

    [Fact]
    public async Task Sms_status_is_a_message_event_both_from_webhooks_and_from_the_events_api()
    {
        var webhook = SparkPostWebhookParser.Parse(
            """[{"msys":{"message_event":{"type":"sms_status","event_id":"1","stat_state":"Delivered"}}}]""");
        var handler = FakeHttpMessageHandler.Returning(
            HttpStatusCode.OK,
            """{"results":[{"type":"sms_status","event_id":"1"}],"total_count":1,"links":{}}""");
        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });

        var page = await client.Events.GetPageAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.IsType<MessageEvent>(webhook.Single());
        Assert.IsType<MessageEvent>(page.Events.Single());
    }
}
