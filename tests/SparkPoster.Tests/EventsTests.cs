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

        Assert.Contains("from=2026-08-01T06%3A00", query, StringComparison.Ordinal);
        Assert.Contains("timezone=UTC", query, StringComparison.Ordinal);
        Assert.Contains("events=bounce%2Cdelivery", query, StringComparison.Ordinal);
        Assert.Contains("campaigns=blackfriday", query, StringComparison.Ordinal);
        Assert.Contains("per_page=500", query, StringComparison.Ordinal);
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
