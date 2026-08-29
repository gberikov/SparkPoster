using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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
