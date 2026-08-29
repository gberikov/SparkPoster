using System.Net;
using System.Text.Json.Nodes;

namespace SparkPoster.Tests;

public sealed class TransmissionsTests
{
    private const string SuccessBody =
        """{"results":{"total_rejected_recipients":0,"total_accepted_recipients":1,"id":"11668787484950529"}}""";

    [Fact]
    public async Task Builder_chain_produces_the_expected_request_body()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessBody);

        var transmission = Transmission.Create()
            .From("noreply@example.com", "Example")
            .To("user@example.com")
            .Subject("Hi {{name}}")
            .Html("<p>Hi {{name}}</p>")
            .SubstitutionData(new { name = "Bob" })
            .Sandbox()
            .Build();

        await client.Transmissions.SendAsync(transmission, cancellationToken: TestContext.Current.CancellationToken);

        const string expected = """
            {
              "content": {
                "from": { "email": "noreply@example.com", "name": "Example" },
                "subject": "Hi {{name}}",
                "html": "<p>Hi {{name}}</p>"
              },
              "recipients": [ { "address": { "email": "user@example.com" } } ],
              "substitution_data": { "name": "Bob" },
              "options": { "sandbox": true }
            }
            """;

        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(handler.LastBody!), JsonNode.Parse(expected)),
            $"Unexpected request body:{Environment.NewLine}{handler.LastBody}");
    }

    [Fact]
    public async Task Request_targets_transmissions_with_api_key_header()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessBody);

        await client.Transmissions.SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.sparkpost.com/api/v1/transmissions", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("test-key", handler.LastRequest.Headers.GetValues("Authorization").Single());
    }

    [Fact]
    public async Task Idempotency_key_is_generated_automatically()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessBody);

        await client.Transmissions.SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken);

        var key = handler.LastRequest!.Headers.GetValues("Idempotency-Key").Single();
        Assert.NotEmpty(key);
        Assert.Matches("^[A-Za-z0-9._-]{1,255}$", key);
    }

    [Fact]
    public async Task Explicit_idempotency_key_is_passed_through()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessBody);

        await client.Transmissions.SendAsync(BuildMinimal(), "order-4815", TestContext.Current.CancellationToken);

        Assert.Equal("order-4815", handler.LastRequest!.Headers.GetValues("Idempotency-Key").Single());
    }

    [Fact]
    public async Task Idempotent_replay_is_visible_in_the_result()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, SuccessBody, ("X-Idempotent-Replayed", "true"));

        var result = await client.Transmissions.SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsIdempotentReplay);
        Assert.Equal("11668787484950529", result.Id);
        Assert.Equal(1, result.TotalAcceptedRecipients);
    }

    [Fact]
    public async Task Regular_response_is_not_marked_as_replay()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, SuccessBody);

        var result = await client.Transmissions.SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsIdempotentReplay);
    }

    [Fact]
    public async Task Status_422_surfaces_description_from_body()
    {
        const string body =
            """{"errors":[{"message":"required field is missing","description":"content object or template_id required","code":"1400"}]}""";
        var (client, _) = CreateClient(HttpStatusCode.UnprocessableEntity, body);

        var exception = await Assert.ThrowsAsync<SparkPostApiException>(
            () => client.Transmissions.SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.Equal("content object or template_id required", exception.Errors.Single().Description);
        Assert.Equal("1400", exception.Errors.Single().Code);
        Assert.Contains("content object or template_id required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Status_429_exposes_retry_after()
    {
        var (client, _) = CreateClient(
            HttpStatusCode.TooManyRequests,
            """{"errors":[{"message":"Too many requests"}]}""",
            ("Retry-After", "5"));

        var exception = await Assert.ThrowsAsync<SparkPostRateLimitException>(
            () => client.Transmissions.SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(TimeSpan.FromSeconds(5), exception.RetryAfter);
    }

    [Fact]
    public async Task Status_420_is_treated_as_rate_limit()
    {
        var (client, _) = CreateClient((HttpStatusCode)420, """{"errors":[{"message":"sending limit reached"}]}""");

        var exception = await Assert.ThrowsAsync<SparkPostRateLimitException>(
            () => client.Transmissions.SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(420, (int)exception.StatusCode);
        Assert.Null(exception.RetryAfter);
    }

    [Fact]
    public async Task Non_json_error_body_does_not_break_parsing()
    {
        var (client, _) = CreateClient(HttpStatusCode.BadGateway, "<html><body>502 Bad Gateway</body></html>");

        var exception = await Assert.ThrowsAsync<SparkPostApiException>(
            () => client.Transmissions.SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(exception.Errors);
        Assert.Contains("502 Bad Gateway", exception.RawBody!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForSubaccount_adds_subaccount_header()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessBody);

        await client.ForSubaccount(42).Transmissions
            .SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("42", handler.LastRequest!.Headers.GetValues("X-MSYS-SUBACCOUNT").Single());
    }

    [Fact]
    public async Task Subaccount_header_is_absent_by_default()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, SuccessBody);

        await client.Transmissions.SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(handler.LastRequest!.Headers.Contains("X-MSYS-SUBACCOUNT"));
    }

    [Fact]
    public async Task European_base_url_is_taken_from_options()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);
        var client = new SparkPostClient(
            handler.CreateClient(),
            new SparkPostOptions { ApiKey = "test-key", BaseUrl = SparkPostEndpoints.Eu });

        await client.Transmissions.SendAsync(BuildMinimal(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("https://api.eu.sparkpost.com/api/v1/transmissions", handler.LastRequest!.RequestUri!.ToString());
    }

    private static TransmissionRequest BuildMinimal() =>
        Transmission.Create()
            .From("noreply@example.com")
            .To("user@example.com")
            .Html("<p>hi</p>")
            .Build();

    private static (SparkPostClient Client, FakeHttpMessageHandler Handler) CreateClient(
        HttpStatusCode statusCode,
        string body,
        params (string Name, string Value)[] headers)
    {
        var handler = FakeHttpMessageHandler.Returning(statusCode, body, headers);
        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });
        return (client, handler);
    }
}
