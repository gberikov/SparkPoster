using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SparkPoster.AspNetCore;
using SparkPoster.Webhooks;

namespace SparkPoster.Tests;

public sealed class WebhookEndpointTests
{
    private const string DeliveryBatch =
        """[{"msys":{"message_event":{"type":"delivery","event_id":"1","rcpt_to":"user@example.com"}}}]""";

    [Fact]
    public async Task Valid_batch_reaches_the_handler_and_answers_200()
    {
        SparkPostEventBatch? received = null;

        using var host = await StartHostAsync(
            (batch, _) =>
            {
                received = batch;
                return Task.CompletedTask;
            });

        using var client = host.GetTestClient();
        using var request = CreateRequest();
        request.Headers.TryAddWithoutValidation(SparkPostWebhookParser.BatchIdHeader, "032d33");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("032d33", received!.BatchId);
        Assert.Equal("user@example.com", received.Events.Single().RcptTo);
    }

    [Fact]
    public async Task Request_without_the_secret_header_is_rejected_with_401()
    {
        using var host = await StartHostAsync(
            (_, _) => Task.CompletedTask,
            new SparkPostWebhookOptions { SecretHeaderName = "X-Webhook-Secret", SecretHeaderValue = "s3cret" });

        using var client = host.GetTestClient();
        using var request = CreateRequest();

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_a_wrong_secret_is_rejected_with_401()
    {
        using var host = await StartHostAsync(
            (_, _) => Task.CompletedTask,
            new SparkPostWebhookOptions { SecretHeaderName = "X-Webhook-Secret", SecretHeaderValue = "s3cret" });

        using var client = host.GetTestClient();
        using var request = CreateRequest();
        request.Headers.TryAddWithoutValidation("X-Webhook-Secret", "wrong");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_with_the_correct_secret_is_accepted()
    {
        using var host = await StartHostAsync(
            (_, _) => Task.CompletedTask,
            new SparkPostWebhookOptions { SecretHeaderName = "X-Webhook-Secret", SecretHeaderValue = "s3cret" });

        using var client = host.GetTestClient();
        using var request = CreateRequest();
        request.Headers.TryAddWithoutValidation("X-Webhook-Secret", "s3cret");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Basic_auth_credentials_are_verified()
    {
        var options = new SparkPostWebhookOptions { BasicAuthUsername = "hook", BasicAuthPassword = "p@ss" };
        using var host = await StartHostAsync((_, _) => Task.CompletedTask, options);
        using var client = host.GetTestClient();

        using var wrong = CreateRequest();
        wrong.Headers.TryAddWithoutValidation("Authorization", "Basic " + Encode("hook:nope"));
        using var wrongResponse = await client.SendAsync(wrong, TestContext.Current.CancellationToken);

        using var right = CreateRequest();
        right.Headers.TryAddWithoutValidation("Authorization", "Basic " + Encode("hook:p@ss"));
        using var rightResponse = await client.SendAsync(right, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rightResponse.StatusCode);
    }

    [Fact]
    public async Task Handler_exception_is_not_swallowed()
    {
        // Swallowing the exception would silently turn at-least-once delivery into
        // at-most-once: SparkPost would get a 200 and never resend the batch.
        using var host = await StartHostAsync((_, _) => throw new InvalidOperationException("handler blew up"));

        using var client = host.GetTestClient();
        using var request = CreateRequest();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SendAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_body_that_is_not_json_is_rejected_with_400()
    {
        // 500 would be indistinguishable in the logs from a handler that threw.
        using var host = await StartHostAsync((_, _) => Task.CompletedTask);

        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hooks/sparkpost")
        {
            Content = new StringContent("not json at all", Encoding.UTF8, "application/json"),
        };

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void An_endpoint_without_any_check_refuses_to_start()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => MapWith(new SparkPostWebhookOptions()));

        Assert.Contains("no check is configured", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SecretHeaderName")]
    [InlineData("SecretHeaderValue")]
    [InlineData("BasicAuthUsername")]
    [InlineData("BasicAuthPassword")]
    public void A_half_filled_pair_refuses_to_start(string filled)
    {
        // This is the case that used to disable the check silently.
        var options = new SparkPostWebhookOptions();

        switch (filled)
        {
            case "SecretHeaderName": options.SecretHeaderName = "X-Secret"; break;
            case "SecretHeaderValue": options.SecretHeaderValue = "s3cret"; break;
            case "BasicAuthUsername": options.BasicAuthUsername = "hook"; break;
            default: options.BasicAuthPassword = "p@ss"; break;
        }

        Assert.Throws<InvalidOperationException>(() => MapWith(options));
    }

    [Fact]
    public void Both_checks_at_once_refuse_to_start()
    {
        // Only the header was ever checked, and nothing said so.
        var options = new SparkPostWebhookOptions
        {
            SecretHeaderName = "X-Secret",
            SecretHeaderValue = "s3cret",
            BasicAuthUsername = "hook",
            BasicAuthPassword = "p@ss",
        };

        Assert.Throws<InvalidOperationException>(() => MapWith(options));
    }

    [Fact]
    public void AllowAnonymous_next_to_a_configured_check_refuses_to_start()
    {
        // The flag used to be ignored, which reads as "anonymous" and behaves as "checked".
        var options = new SparkPostWebhookOptions
        {
            AllowAnonymous = true,
            SecretHeaderName = "X-Secret",
            SecretHeaderValue = "s3cret",
        };

        Assert.Throws<InvalidOperationException>(() => MapWith(options));
    }

    [Fact]
    public void Options_are_required()
    {
        Assert.Throws<ArgumentNullException>(() => MapWith(null!));
    }

    [Fact]
    public async Task AllowAnonymous_accepts_an_unauthenticated_call()
    {
        using var host = await StartHostAsync(
            (_, _) => Task.CompletedTask,
            new SparkPostWebhookOptions { AllowAnonymous = true });

        using var client = host.GetTestClient();
        using var request = CreateRequest();

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Maps the endpoint outside a host, so the guard is observed directly rather than through
    /// whatever the host wraps a startup exception in.
    /// </summary>
    private static void MapWith(SparkPostWebhookOptions options) =>
        new TestRouteBuilder(new ServiceCollection().AddRouting().BuildServiceProvider())
            .MapSparkPostWebhook("/hooks/sparkpost", (_, _) => Task.CompletedTask, options);

    private sealed class TestRouteBuilder(IServiceProvider services) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider => services;

        public ICollection<EndpointDataSource> DataSources { get; } = [];

        public IApplicationBuilder CreateApplicationBuilder() => new ApplicationBuilder(services);
    }

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static HttpRequestMessage CreateRequest() =>
        new(HttpMethod.Post, "/hooks/sparkpost")
        {
            Content = new StringContent(DeliveryBatch, Encoding.UTF8, "application/json"),
        };

    private static async Task<IHost> StartHostAsync(
        Func<SparkPostEventBatch, CancellationToken, Task> handler,
        SparkPostWebhookOptions? options = null)
    {
        options ??= new SparkPostWebhookOptions { AllowAnonymous = true };

        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddRouting())
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                        endpoints.MapSparkPostWebhook("/hooks/sparkpost", handler, options));
                }))
            .StartAsync(TestContext.Current.CancellationToken);

        return host;
    }
}
