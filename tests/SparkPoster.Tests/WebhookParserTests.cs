using SparkPoster.Webhooks;

namespace SparkPoster.Tests;

public sealed class WebhookParserTests
{
    /// <summary>A bounce: the SparkPost documentation example, trimmed to the fields that matter.</summary>
    private const string BounceBatch = """
        [{"msys":{"message_event":{
          "type":"bounce","event_id":"92356927693813856","timestamp":"1460989507",
          "bounce_class":"1","error_code":"554","message_id":"000443ee14578172be22",
          "raw_reason":"MAIL REFUSED - IP (17.99.99.99) is in black list",
          "reason":"MAIL REFUSED - IP (a.b.c.d) is in black list",
          "rcpt_to":"recipient@example.com","rcpt_type":"cc","rcpt_tags":["male","US"],
          "rcpt_meta":{"customKey":"customValue"},"num_retries":"2","msg_size":"1337",
          "campaign_id":"Example Campaign Name","transmission_id":"65832150921904138",
          "subaccount_id":"101","sending_ip":"18.236.253.72","transactional":"1",
          "mailbox_provider":"Gsuite"}}}]
        """;

    [Fact]
    public void Bounce_is_parsed_into_MessageEvent()
    {
        var events = SparkPostWebhookParser.Parse(BounceBatch);

        var bounce = Assert.IsType<MessageEvent>(events.Single());

        Assert.Equal(SparkPostEventTypes.Bounce, bounce.Type);
        Assert.Equal("92356927693813856", bounce.EventId);
        Assert.Equal("1", bounce.BounceClass);
        Assert.Equal("554", bounce.ErrorCode);
        Assert.Equal("recipient@example.com", bounce.RcptTo);
        Assert.Equal(["male", "US"], bounce.RcptTags!);
        Assert.Equal("customValue", (string?)bounce.RcptMeta!["customKey"]);
        Assert.Equal("Gsuite", bounce.MailboxProvider);
    }

    [Fact]
    public void Event_timestamp_is_parsed_from_unix_seconds()
    {
        var bounce = (MessageEvent)SparkPostWebhookParser.Parse(BounceBatch).Single();

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1460989507), bounce.Timestamp);
    }

    [Fact]
    public void Unparsable_timestamp_does_not_break_the_batch()
    {
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"message_event":{"type":"bounce","timestamp":"yesterday"}}}]""");

        var unknown = Assert.IsType<UnknownSparkPostEvent>(events.Single());

        Assert.Equal(SparkPostEventTypes.Bounce, unknown.Type);
        Assert.NotNull(unknown.Raw);
        Assert.Contains("sparkposter_parse_error", unknown.Extra!);
    }

    [Fact]
    public void Unparsable_timestamp_leaves_the_other_events_intact()
    {
        var events = SparkPostWebhookParser.Parse(
            """
            [
              {"msys":{"message_event":{"type":"bounce","timestamp":"yesterday"}}},
              {"msys":{"message_event":{"type":"delivery","timestamp":"1460989507"}}}
            ]
            """);

        Assert.Collection(
            events,
            e => Assert.IsType<UnknownSparkPostEvent>(e),
            e =>
            {
                var delivery = Assert.IsType<MessageEvent>(e);

                Assert.Equal(SparkPostEventTypes.Delivery, delivery.Type);
                Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1460989507), delivery.Timestamp);
            });
    }

    [Fact]
    public void Out_of_range_unix_timestamp_does_not_break_the_batch()
    {
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"message_event":{"type":"bounce","timestamp":"99999999999999"}}}]""");

        var unknown = Assert.IsType<UnknownSparkPostEvent>(events.Single());

        Assert.Contains("sparkposter_parse_error", unknown.Extra!);
    }

    [Fact]
    public void Click_is_parsed_into_TrackEvent()
    {
        var events = SparkPostWebhookParser.Parse(
            """
            [{"msys":{"track_event":{
              "type":"click","target_link_url":"http://example.com/deals",
              "target_link_name":"deals","user_agent":"Mozilla/5.0","ip_address":"127.0.0.1",
              "geo_ip":{"country":"US","city":"Columbia"}}}}]
            """);

        var click = Assert.IsType<TrackEvent>(events.Single());

        Assert.Equal("http://example.com/deals", click.TargetLinkUrl);
        Assert.Equal("US", (string?)click.GeoIp!["country"]);
    }

    [Fact]
    public void Each_event_category_maps_to_its_own_type()
    {
        var events = SparkPostWebhookParser.Parse(
            """
            [
              {"msys":{"gen_event":{"type":"generation_failure","error_code":"554"}}},
              {"msys":{"unsubscribe_event":{"type":"link_unsubscribe","user_agent":"curl"}}},
              {"msys":{"relay_event":{"type":"relay_delivery","protocol":"smtp"}}}
            ]
            """);

        Assert.Collection(
            events,
            e => Assert.Equal("554", Assert.IsType<GenerationEvent>(e).ErrorCode),
            e => Assert.Equal("curl", Assert.IsType<UnsubscribeEvent>(e).UserAgent),
            e => Assert.Equal("smtp", Assert.IsType<RelayEvent>(e).Protocol));
    }

    [Fact]
    public void Unknown_category_does_not_break_parsing()
    {
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"quantum_event":{"type":"teleported","payload":42}}}]""");

        var unknown = Assert.IsType<UnknownSparkPostEvent>(events.Single());

        Assert.Equal("quantum_event", unknown.Category);
        Assert.Equal("teleported", unknown.Type);
        Assert.Equal(42, (int?)unknown.Raw!["payload"]);
    }

    [Fact]
    public void Unknown_fields_of_a_known_event_are_preserved()
    {
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"message_event":{"type":"delivery","quantum_flux":"7"}}}]""");

        var delivery = Assert.IsType<MessageEvent>(events.Single());

        Assert.Equal("7", delivery.Extra!["quantum_flux"].GetString());
    }

    [Fact]
    public void Webhook_validation_batch_contains_no_events()
    {
        // This is exactly the batch SparkPost posts when ValidateAsync is called.
        Assert.Empty(SparkPostWebhookParser.Parse("""[{"msys":{}}]"""));
    }

    [Fact]
    public async Task Parsing_from_a_stream_yields_the_same_result()
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(BounceBatch));

        var events = await SparkPostWebhookParser.ParseAsync(stream, TestContext.Current.CancellationToken);

        Assert.Equal(SparkPostEventTypes.Bounce, events.Single().Type);
    }

    [Fact]
    public void Empty_batch_parses_to_an_empty_list()
    {
        Assert.Empty(SparkPostWebhookParser.Parse("[]"));
    }
}
