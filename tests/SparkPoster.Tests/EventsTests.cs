using System.Net;
using SparkPoster.Webhooks;

namespace SparkPoster.Tests;

public sealed class EventsTests
{
    private const string SinglePage = """
        {"results":[
          {"type":"bounce","event_id":"1","bounce_class":"1","timestamp":"2019-06-16T19:02:09.373Z"},
          {"type":"click","event_id":"2","target_link_url":"http://example.com"}
        ],"total_count":2,"links":{}}
        """;

    [Fact]
    public async Task Page_parses_results_and_total_count()
    {
        var (client, _) = CreateClient(SinglePage);

        var page = await client.Events.GetPageAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.IsType<MessageEvent>(page.Events[0]);
        Assert.IsType<TrackEvent>(page.Events[1]);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Total_count_is_read_when_it_arrives_as_a_string()
    {
        // SparkPost returns the same numeric fields quoted in places; the context is configured
        // for that, and reading total_count has to go through it like everything else.
        var (client, _) = CreateClient("""{"results":[],"total_count":"17","links":{}}""");

        var page = await client.Events.GetPageAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(17, page.TotalCount);
    }

    [Fact]
    public async Task Events_api_timestamp_is_parsed_from_iso8601()
    {
        // Webhooks report unix seconds while the Events API reports ISO 8601;
        // both have to land on the same DateTimeOffset.
        var (client, _) = CreateClient(SinglePage);

        var page = await client.Events.GetPageAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            new DateTimeOffset(2019, 6, 16, 19, 2, 9, 373, TimeSpan.Zero),
            page.Events[0].Timestamp);
    }

    [Fact]
    public async Task Unfamiliar_event_type_is_reported_as_unknown()
    {
        var (client, _) = CreateClient("""{"results":[{"type":"teleported","payload":1}],"total_count":1}""");

        var page = await client.Events.GetPageAsync(cancellationToken: TestContext.Current.CancellationToken);

        var unknown = Assert.IsType<UnknownSparkPostEvent>(page.Events.Single());
        Assert.Equal("teleported", unknown.Type);
        Assert.Equal(1, (int?)unknown.Raw!["payload"]);
    }

    [Fact]
    public async Task First_request_starts_the_walk_with_the_initial_cursor()
    {
        var (client, handler) = CreateClient(SinglePage);

        await client.Events.GetPageAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("cursor=initial", handler.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Filters_are_serialized_into_the_query_string()
    {
        var (client, handler) = CreateClient(SinglePage);

        await client.Events.GetPageAsync(
            new EventQuery
            {
                From = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.FromHours(6)),
                Events = [SparkPostEventTypes.Bounce, SparkPostEventTypes.Delivery],
                Campaigns = ["blackfriday"],
                PerPage = 500,
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var query = handler.LastRequest!.RequestUri!.Query;

        // The documented format is YYYY-MM-DDTHH:MM:ssZ in UTC; the offset above is +06:00.
        Assert.Contains("from=2026-08-01T06%3A00%3A00Z", query, StringComparison.Ordinal);
        Assert.DoesNotContain("timezone", query, StringComparison.Ordinal);
        Assert.Contains("events=bounce%2Cdelivery", query, StringComparison.Ordinal);
        Assert.Contains("campaigns=blackfriday", query, StringComparison.Ordinal);
        Assert.Contains("per_page=500", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Time_range_keeps_its_seconds()
    {
        // A minute-precision format collapsed a ten-second window into from == to.
        var (client, handler) = CreateClient(SinglePage);

        await client.Events.GetPageAsync(
            new EventQuery
            {
                From = new DateTimeOffset(2026, 9, 6, 10, 0, 45, TimeSpan.Zero),
                To = new DateTimeOffset(2026, 9, 6, 10, 0, 55, TimeSpan.Zero),
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var query = handler.LastRequest!.RequestUri!.Query;

        Assert.Contains("from=2026-09-06T10%3A00%3A45Z", query, StringComparison.Ordinal);
        Assert.Contains("to=2026-09-06T10%3A00%3A55Z", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Transmission_and_message_filters_use_the_documented_wire_names()
    {
        // SparkPost ignores unknown query parameters, so "transmission_ids" silently returned
        // every event instead of the ones asked for.
        var (client, handler) = CreateClient(SinglePage);

        await client.Events.GetPageAsync(
            new EventQuery
            {
                TransmissionIds = ["t1", "t2"],
                MessageIds = ["m1"],
                EventIds = ["e1"],
                AbTests = ["ab"],
                AbTestVersions = ["1", "2"],
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var query = handler.LastRequest!.RequestUri!.Query;

        Assert.Contains("&transmissions=t1%2Ct2", query, StringComparison.Ordinal);
        Assert.Contains("&messages=m1", query, StringComparison.Ordinal);
        Assert.Contains("&event_ids=e1", query, StringComparison.Ordinal);
        Assert.Contains("&ab_test_versions=1%2C2", query, StringComparison.Ordinal);
        Assert.DoesNotContain("transmission_ids", query, StringComparison.Ordinal);
        Assert.DoesNotContain("message_ids", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Custom_delimiter_joins_the_lists_it_announces()
    {
        // Declaring delimiter=| while still joining with a comma made SparkPost read
        // "one,two" as a single campaign name.
        var (client, handler) = CreateClient(SinglePage);

        await client.Events.GetPageAsync(
            new EventQuery { Delimiter = "|", Campaigns = ["one", "two"], Subjects = ["Hi, there"] },
            cancellationToken: TestContext.Current.CancellationToken);

        var query = handler.LastRequest!.RequestUri!.Query;

        Assert.Contains("campaigns=one%7Ctwo", query, StringComparison.Ordinal);
        Assert.Contains("subjects=Hi%2C%20there", query, StringComparison.Ordinal);
        Assert.Contains("delimiter=%7C", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Next_cursor_is_extracted_from_the_links_object()
    {
        var (client, _) = CreateClient(
            """
            {"results":[],"total_count":0,
             "links":{"next":"/api/v1/events/message?cursor=abc%2Fdef&per_page=1000"}}
            """);

        var page = await client.Events.GetPageAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("abc/def", page.NextCursor);
    }

    [Fact]
    public async Task Next_cursor_is_extracted_from_the_links_array()
    {
        var (client, _) = CreateClient(
            """
            {"results":[],"total_count":0,
             "links":[{"rel":"next","href":"/api/v1/events/message?cursor=xyz"}]}
            """);

        var page = await client.Events.GetPageAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("xyz", page.NextCursor);
    }

    [Fact]
    public async Task Search_walks_every_page()
    {
        var handler = FakeHttpMessageHandler.ReturningSequence(
            """{"results":[{"type":"delivery","event_id":"1"}],"total_count":2,"links":{"next":"?cursor=page2"}}""",
            """{"results":[{"type":"delivery","event_id":"2"}],"total_count":2,"links":{}}""");
        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });

        var ids = new List<string?>();

        await foreach (var @event in client.Events.SearchAsync(cancellationToken: TestContext.Current.CancellationToken))
        {
            ids.Add(@event.EventId);
        }

        Assert.Equal(["1", "2"], ids);
        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("cursor=page2", handler.LastRequest!.RequestUri!.Query, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_does_not_call_the_api_before_enumeration_starts()
    {
        var (client, handler) = CreateClient(SinglePage);

        _ = client.Events.SearchAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, handler.RequestCount);
    }

    private static (SparkPostClient Client, FakeHttpMessageHandler Handler) CreateClient(string body)
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, body);
        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });
        return (client, handler);
    }
}
