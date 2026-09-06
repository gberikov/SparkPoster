using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using SparkPoster.Webhooks;

namespace SparkPoster.Tests;

public sealed class EventFieldPreservationTests
{
    [Theory]
    [InlineData(false, "delivery")]
    [InlineData(true, "delivery")]
    [InlineData(false, "future_event")]
    [InlineData(true, "future_event")]
    public async Task Malformed_common_fields_preserve_valid_fields_and_original_payload(bool flat, string type)
    {
        var body = JsonNode.Parse("""
            {"event_id":"e1","message_id":"m1","rcpt_to":"a@example.com",
             "timestamp":"bad","rcpt_tags":["valid",{}],"open_tracking":"bad",
             "customer_id":123,"scheduled_time":"1460989507","click_tracking":true,
             "rcpt_meta":{"key":"value"},"future_field":{"items":[1,null,true]}}
            """)!.AsObject();
        body["type"] = type;

        var events = await ReadAsync([body, new JsonObject { ["type"] = "delivery", ["event_id"] = "good" }], flat);

        var unknown = Assert.IsType<UnknownSparkPostEvent>(events[0]);
        Assert.Equal(type, unknown.Type);
        Assert.Equal("e1", unknown.EventId);
        Assert.Equal("m1", unknown.MessageId);
        Assert.Equal("a@example.com", unknown.RcptTo);
        Assert.Equal("123", unknown.CustomerId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1460989507), unknown.ScheduledTime);
        Assert.True(unknown.ClickTracking);
        Assert.Equal("value", (string?)unknown.RcptMeta!["key"]);
        Assert.Null(unknown.Timestamp);
        Assert.Null(unknown.RcptTags);
        Assert.Null(unknown.OpenTracking);
        Assert.NotNull(unknown.ParseError);
        Assert.True(JsonNode.DeepEquals(body, unknown.Raw));
        Assert.Equal(3, unknown.Extra!["future_field"].GetProperty("items").GetArrayLength());
        Assert.Equal("good", Assert.IsType<MessageEvent>(events[1]).EventId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Malformed_identifier_and_discriminator_preserve_valid_timestamp(bool flat)
    {
        var body = JsonNode.Parse("""
            {"type":{},"event_id":[],"timestamp":"1460989507","message_id":"m1"}
            """)!.AsObject();

        var unknown = Assert.IsType<UnknownSparkPostEvent>((await ReadAsync([body], flat)).Single());

        Assert.Null(unknown.Type);
        Assert.Null(unknown.EventId);
        Assert.Equal("m1", unknown.MessageId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1460989507), unknown.Timestamp);
        Assert.NotNull(unknown.ParseError);
        Assert.True(JsonNode.DeepEquals(body, unknown.Raw));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Nested_event_models_preserve_unknown_fields(bool flat)
    {
        var trackBody = JsonNode.Parse("""
            {"type":"open","geo_ip":{"country":"US","future_geo":{"code":42}},
             "user_agent_parsed":{"agent_family":"Chrome","future_agent":[true,null]}}
            """)!.AsObject();
        var abBody = JsonNode.Parse("""
            {"type":"ab_test_completed","ab_test":{"id":"test","future_test":null,
             "default_template":{"template_id":"default","future_result":"kept"},
             "variants":[{"template_id":"variant","future_result":{"score":1}}]}}
            """)!.AsObject();

        var events = await ReadAsync([trackBody, abBody], flat);

        var track = Assert.IsType<TrackEvent>(events[0]);
        Assert.Equal("US", track.GeoIp!.Country);
        Assert.Equal(42, Assert.Single(track.GeoIp.Extra!).Value.GetProperty("code").GetInt32());
        Assert.Equal("Chrome", track.UserAgentParsed!.AgentFamily);
        Assert.Equal(2, Assert.Single(track.UserAgentParsed.Extra!).Value.GetArrayLength());
        Assert.Null(track.Extra);
        var ab = Assert.IsType<AbTestEvent>(events[1]).AbTest!;
        Assert.Equal("test", ab.Id);
        Assert.Equal(JsonValueKind.Null, Assert.Single(ab.Extra!).Value.ValueKind);
        Assert.Equal("kept", ab.DefaultTemplate!.Extra!["future_result"].GetString());
        Assert.Equal(1, ab.Variants!.Single().Extra!["future_result"].GetProperty("score").GetInt32());
    }

    private static async Task<IReadOnlyList<SparkPostEvent>> ReadAsync(JsonObject[] bodies, bool flat)
    {
        var items = new JsonArray();
        foreach (var body in bodies)
        {
            var type = body["type"] is JsonValue value && value.TryGetValue<string>(out var name) ? name : null;
            var category = type switch
            {
                "open" => "track_event",
                "ab_test_completed" => "ab_test_event",
                "future_event" => "future_category",
                _ => "message_event",
            };
            items.Add(flat ? body.DeepClone() : new JsonObject
            {
                ["msys"] = new JsonObject { [category] = body.DeepClone() },
            });
        }

        if (!flat)
        {
            return SparkPostWebhookParser.Parse(items.ToJsonString());
        }

        var response = new JsonObject { ["results"] = items, ["total_count"] = bodies.Length, ["links"] = new JsonObject() };
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, response.ToJsonString());
        using var http = handler.CreateClient();
        var client = new SparkPostClient(http, new SparkPostOptions { ApiKey = "test-key" });
        return (await client.Events.GetPageAsync(cancellationToken: TestContext.Current.CancellationToken)).Events;
    }
}
