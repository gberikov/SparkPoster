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
        Assert.Equal("message_event", unknown.Category);
        Assert.NotNull(unknown.Raw);
        Assert.Contains(
            "not Unix seconds within the range of a date nor ISO 8601",
            unknown.ParseError,
            StringComparison.Ordinal);
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

        Assert.Contains(
            "not Unix seconds within the range of a date nor ISO 8601",
            unknown.ParseError,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Numeric_timestamp_is_parsed_from_unix_seconds()
    {
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"message_event":{"type":"bounce","timestamp":1460989507}}}]""");

        var bounce = Assert.IsType<MessageEvent>(events.Single());

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1460989507), bounce.Timestamp);
    }

    [Fact]
    public void Fractional_numeric_timestamp_does_not_break_the_batch()
    {
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"message_event":{"type":"bounce","timestamp":1460989507.5}}}]""");

        var unknown = Assert.IsType<UnknownSparkPostEvent>(events.Single());

        Assert.NotNull(unknown.ParseError);
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
        Assert.Equal("US", click.GeoIp!.Country);
    }

    [Fact]
    public void Boolean_initial_pixel_does_not_turn_an_open_into_unknown()
    {
        // The official samples carry initial_pixel as a boolean. A string property here used to
        // fail the whole event, so every open and click arrived as UnknownSparkPostEvent.
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"track_event":{"type":"open","event_id":"event1","timestamp":"1460989507","initial_pixel":true}}}]""");

        var open = Assert.IsType<TrackEvent>(events.Single());

        Assert.True(open.InitialPixel);
        Assert.Equal("event1", open.EventId);
    }

    [Fact]
    public void Geo_ip_accepts_coordinates_as_numbers_and_as_strings()
    {
        // Webhooks send numbers, the Events API sends strings — for the same field.
        var events = SparkPostWebhookParser.Parse(
            """
            [
              {"msys":{"track_event":{"type":"click","geo_ip":{"latitude":39.1749,"longitude":-76.8375,"zip":21046}}}},
              {"msys":{"track_event":{"type":"click","geo_ip":{"latitude":"39.1749","longitude":"-76.8375","zip":"21046"}}}}
            ]
            """);

        Assert.All(events, e =>
        {
            var geo = Assert.IsType<TrackEvent>(e).GeoIp!;
            Assert.Equal(39.1749, geo.Latitude);
            Assert.Equal(-76.8375, geo.Longitude);
            Assert.Equal("21046", geo.Zip);
        });
    }

    [Fact]
    public void Unsubscribe_mailfrom_is_read_from_its_real_wire_name()
    {
        // SparkPost spells it "mailfrom", not the snake_case "mail_from".
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"unsubscribe_event":{"type":"list_unsubscribe","mailfrom":"recipient@example.com"}}}]""");

        var unsubscribe = Assert.IsType<UnsubscribeEvent>(events.Single());

        Assert.Equal("recipient@example.com", unsubscribe.MailFrom);
        Assert.Null(unsubscribe.Extra);
    }

    [Fact]
    public void Ab_test_and_ingest_categories_have_their_own_types()
    {
        var events = SparkPostWebhookParser.Parse(
            """
            [
              {"msys":{"ab_test_event":{"type":"ab_test_completed","event_id":"a1","ab_test":{"id":"password-reset","version":1,"winning_template_id":"templ-1234","variants":[{"template_id":"templ-5678","engagement_rate":0.2}]}}}},
              {"msys":{"ingest_event":{"type":"error","event_id":"i1","batch_id":"b1","number_failed":50,"retryable":false}}}
            ]
            """);

        Assert.Collection(
            events,
            e =>
            {
                var abTest = Assert.IsType<AbTestEvent>(e);
                Assert.Equal(SparkPostEventTypes.AbTestCompleted, abTest.Type);
                Assert.Equal("templ-1234", abTest.AbTest!.WinningTemplateId);
                Assert.Equal(0.2, abTest.AbTest.Variants!.Single().EngagementRate);
            },
            e =>
            {
                var ingest = Assert.IsType<IngestEvent>(e);
                Assert.Equal(SparkPostEventTypes.IngestError, ingest.Type);
                Assert.Equal("b1", ingest.BatchId);
                Assert.Equal(50, ingest.NumberFailed);
                Assert.False(ingest.Retryable);
            });
    }

    [Fact]
    public void Unknown_event_keeps_the_common_fields()
    {
        // Deduplication on EventId has to work for an event nobody has typed yet.
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"quantum_event":{"type":"teleported","event_id":"q1","timestamp":"1460989507","message_id":"m1","rcpt_to":"a@example.com","payload":42}}}]""");

        var unknown = Assert.IsType<UnknownSparkPostEvent>(events.Single());

        Assert.Equal("q1", unknown.EventId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1460989507), unknown.Timestamp);
        Assert.Equal("m1", unknown.MessageId);
        Assert.Equal("a@example.com", unknown.RcptTo);
        Assert.Equal(42, unknown.Extra!["payload"].GetInt32());
        Assert.Null(unknown.ParseError);
    }

    [Fact]
    public void Bad_specialized_field_keeps_the_common_fields_and_reports_why()
    {
        // ab_test is an object; a number there fails the typed model but not the base one.
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"ab_test_event":{"type":"ab_test_completed","event_id":"a1","timestamp":"1460989507","ab_test":7}}}]""");

        var unknown = Assert.IsType<UnknownSparkPostEvent>(events.Single());

        Assert.Equal("ab_test_event", unknown.Category);
        Assert.Equal("a1", unknown.EventId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1460989507), unknown.Timestamp);
        Assert.NotNull(unknown.ParseError);
        Assert.Equal(7, (int?)unknown.Raw!["ab_test"]);
    }

    [Fact]
    public void Non_string_type_does_not_break_the_batch()
    {
        // A cast of a number to string on a JsonNode throws InvalidOperationException, which is
        // not the exception the per-event fallback catches — the neighbour was lost with it.
        var events = SparkPostWebhookParser.Parse(
            """[{"msys":{"message_event":{"type":42}}},{"msys":{"message_event":{"type":"delivery","event_id":"good"}}}]""");

        Assert.Collection(
            events,
            e => Assert.Equal("42", e.Type),
            e => Assert.Equal("good", Assert.IsType<MessageEvent>(e).EventId));
    }

    [Theory]
    [InlineData("""{"unexpected":true}""")]
    [InlineData("""[{"unexpected":true}]""")]
    [InlineData("""[42]""")]
    [InlineData("""[{"msys":{"message_event":"not an object"}}]""")]
    public void Body_that_is_not_a_sparkpost_batch_is_rejected(string json)
    {
        // Silently answering "no events" to a body that never could have carried one hides a
        // misrouted request. A JsonException becomes a 400 at the endpoint.
        Assert.Throws<System.Text.Json.JsonException>(() => SparkPostWebhookParser.Parse(json));
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
