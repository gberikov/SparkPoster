using System.Net;
using System.Text.Json.Nodes;

namespace SparkPoster.Tests;

public sealed class SuppressionListTests
{
    [Fact]
    public async Task Upsert_puts_the_address_in_the_path_and_the_rest_in_the_body()
    {
        var (client, handler) = CreateClient("""{"results":{"message":"Recipient successfully created"}}""");

        await client.SuppressionList.UpsertAsync(
            new SuppressionEntry
            {
                Recipient = "user+tag@example.com",
                Type = SuppressionTypes.NonTransactional,
                Description = "unsubscribed from the newsletter",
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/suppression-list/user%2Btag%40example.com",
            handler.LastRequest.RequestUri!.AbsoluteUri);

        AssertJson(
            """{"type":"non_transactional","description":"unsubscribed from the newsletter"}""",
            handler.LastBody!);
    }

    [Fact]
    public async Task Bulk_upsert_sends_every_entry_in_one_request()
    {
        var (client, handler) = CreateClient("""{"results":{"message":"Suppression list successfully updated"}}""");

        await client.SuppressionList.UpsertManyAsync(
            [
                new SuppressionEntry { Recipient = "a@example.com", Type = SuppressionTypes.Transactional },
                new SuppressionEntry { Recipient = "b@example.com", Type = SuppressionTypes.NonTransactional },
            ],
            TestContext.Current.CancellationToken);

        Assert.Equal("https://api.sparkpost.com/api/v1/suppression-list", handler.LastRequest!.RequestUri!.ToString());

        AssertJson(
            """
            {"recipients":[
              {"recipient":"a@example.com","type":"transactional"},
              {"recipient":"b@example.com","type":"non_transactional"}
            ]}
            """,
            handler.LastBody!);
    }

    [Fact]
    public async Task Bulk_upsert_strips_read_only_fields()
    {
        var (client, handler) = CreateClient("""{"results":{"message":"Suppression list successfully updated"}}""");

        await client.SuppressionList.UpsertManyAsync(
            [
                new SuppressionEntry
                {
                    Recipient = "a@example.com",
                    Type = SuppressionTypes.Transactional,
                    Description = "bounced",
                    Source = "Bounce Rule",
                    Created = DateTimeOffset.UnixEpoch,
                    Updated = DateTimeOffset.UnixEpoch,
                    SubaccountId = 7,
                },
                new SuppressionEntry
                {
                    Recipient = "b@example.com",
                    Type = SuppressionTypes.NonTransactional,
                    Description = "unsubscribed from the newsletter",
                    ListId = "newsletter",
                    Source = "Manually Added",
                    Created = DateTimeOffset.UnixEpoch,
                    Updated = DateTimeOffset.UnixEpoch,
                    SubaccountId = 7,
                },
            ],
            TestContext.Current.CancellationToken);

        var recipients = JsonNode.Parse(handler.LastBody!)!["recipients"]!.AsArray();

        Assert.Equal<string>(
            ["recipient", "type", "description"],
            recipients[0]!.AsObject().Select(property => property.Key));
        Assert.Equal<string>(
            ["recipient", "type", "description", "list_id"],
            recipients[1]!.AsObject().Select(property => property.Key));
    }

    [Fact]
    public async Task Bulk_upsert_rejects_a_blank_recipient()
    {
        var (client, handler) = CreateClient("""{"results":{"message":"Suppression list successfully updated"}}""");

        await Assert.ThrowsAsync<ArgumentException>(() => client.SuppressionList.UpsertManyAsync(
            [
                new SuppressionEntry { Recipient = "a@example.com", Type = SuppressionTypes.Transactional },
                new SuppressionEntry { Recipient = " ", Type = SuppressionTypes.Transactional },
            ],
            TestContext.Current.CancellationToken));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Get_returns_one_entry_per_kind()
    {
        var (client, _) = CreateClient(
            """
            {"results":[
              {"recipient":"user@example.com","type":"transactional","source":"Manually Added",
               "description":"test","created":"2026-08-01T10:00:00+00:00"},
              {"recipient":"user@example.com","type":"non_transactional","source":"Bounce Rule"}
            ]}
            """);

        var entries = await client.SuppressionList.GetAsync("user@example.com", TestContext.Current.CancellationToken);

        Assert.Equal(2, entries.Count);
        Assert.Equal(SuppressionTypes.Transactional, entries[0].Type);
        Assert.Equal("Manually Added", entries[0].Source);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero), entries[0].Created);
    }

    [Fact]
    public async Task Delete_can_be_scoped_to_a_mailing_list()
    {
        var (client, handler) = CreateClient(string.Empty, HttpStatusCode.NoContent);

        await client.SuppressionList.DeleteAsync(
            "user@example.com",
            "newsletter",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/suppression-list/user%40example.com?list_id=newsletter",
            handler.LastRequest.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Search_filters_are_serialized_into_the_query_string()
    {
        var (client, handler) = CreateClient("""{"results":[],"total_count":0,"links":{}}""");

        await client.SuppressionList.SearchPageAsync(
            new SuppressionQuery
            {
                Domain = "example.com",
                Types = [SuppressionTypes.NonTransactional],
                Sources = ["Bounce Rule"],
                DescriptionStrict = true,
                PerPage = 100,
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var query = handler.LastRequest!.RequestUri!.Query;

        Assert.Contains("domain=example.com", query, StringComparison.Ordinal);
        Assert.Contains("types=non_transactional", query, StringComparison.Ordinal);
        Assert.Contains("sources=Bounce%20Rule", query, StringComparison.Ordinal);
        Assert.Contains("description_strict=true", query, StringComparison.Ordinal);
        Assert.Contains("per_page=100", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_dates_carry_seconds_and_an_offset_and_no_timezone_parameter()
    {
        var (client, handler) = CreateClient("""{"results":[],"total_count":0,"links":{}}""");

        await client.SuppressionList.SearchPageAsync(
            new SuppressionQuery
            {
                From = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.FromHours(6)),
                To = new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero),
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var query = handler.LastRequest!.RequestUri!.Query;

        Assert.Contains("from=2026-08-01T06%3A00%3A00%2B00%3A00", query, StringComparison.Ordinal);
        Assert.Contains("to=2026-08-02T00%3A00%3A00%2B00%3A00", query, StringComparison.Ordinal);
        Assert.DoesNotContain("timezone", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_walks_every_page()
    {
        var handler = FakeHttpMessageHandler.ReturningSequence(
            """{"results":[{"recipient":"a@example.com"}],"total_count":2,"links":{"next":"?cursor=page2"}}""",
            """{"results":[{"recipient":"b@example.com"}],"total_count":2,"links":{}}""");
        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });

        var recipients = new List<string>();

        await foreach (var entry in client.SuppressionList.SearchAsync(
            cancellationToken: TestContext.Current.CancellationToken))
        {
            recipients.Add(entry.Recipient);
        }

        Assert.Equal(["a@example.com", "b@example.com"], recipients);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Total_count_is_read_when_it_arrives_as_a_string()
    {
        var (client, _) = CreateClient(
            """{"results":[{"recipient":"a@example.com"}],"total_count":"3","links":{}}""");

        var page = await client.SuppressionList.SearchPageAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(3, page.TotalCount);
    }

    [Fact]
    public async Task Summary_reports_counts_per_kind()
    {
        var (client, handler) = CreateClient("""{"results":{"transactional":1234,"non_transactional":5678}}""");

        var summary = await client.SuppressionList.GetSummaryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1234, summary.Transactional);
        Assert.Equal(5678, summary.NonTransactional);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/suppression-list/summary",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Subaccount_scope_reaches_its_own_list()
    {
        var (client, handler) = CreateClient("""{"results":[]}""");

        await client.ForSubaccount(7).SuppressionList
            .GetAsync("user@example.com", TestContext.Current.CancellationToken);

        Assert.Equal("7", handler.LastRequest!.Headers.GetValues("X-MSYS-SUBACCOUNT").Single());
    }

    private static void AssertJson(string expected, string actual) =>
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected)),
            $"Unexpected request body:{Environment.NewLine}{actual}");

    private static (SparkPostClient Client, FakeHttpMessageHandler Handler) CreateClient(
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = FakeHttpMessageHandler.Returning(statusCode, body);
        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });
        return (client, handler);
    }
}
