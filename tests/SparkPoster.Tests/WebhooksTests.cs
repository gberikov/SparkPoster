using System.Net;
using System.Text.Json.Nodes;

namespace SparkPoster.Tests;

public sealed class WebhooksTests
{
    [Fact]
    public async Task Создание_отправляет_вебхук_и_возвращает_идентификатор()
    {
        var (client, handler) = CreateClient(
            HttpStatusCode.OK,
            """{"results":{"id":"12affc24-f183-11e3-9234-3c15c2c818c2"}}""");

        var id = await client.Webhooks.CreateAsync(
            new WebhookRequest
            {
                Name = "Example webhook",
                Target = "https://client.example.com/hooks",
                Events = [SparkPostEventTypes.Delivery, SparkPostEventTypes.Bounce],
                AuthType = WebhookAuthType.Basic,
                AuthCredentials = new WebhookAuthCredentials { Username = "user", Password = "secret" },
                ExceptionSubaccounts = [123],
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("12affc24-f183-11e3-9234-3c15c2c818c2", id);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://api.sparkpost.com/api/v1/webhooks", handler.LastRequest.RequestUri!.ToString());

        AssertJson(
            """
            {
              "name": "Example webhook",
              "target": "https://client.example.com/hooks",
              "events": [ "delivery", "bounce" ],
              "auth_type": "basic",
              "auth_credentials": { "username": "user", "password": "secret" },
              "exception_subaccounts": [ 123 ]
            }
            """,
            handler.LastBody!);
    }

    [Fact]
    public async Task Чтение_разбирает_вебхук()
    {
        var (client, handler) = CreateClient(
            HttpStatusCode.OK,
            """
            {"results":{"id":"abc","name":"Example","target":"https://x.io/h","events":["delivery"],
             "active":true,"auth_type":"oauth2","last_successful":"2026-08-01 10:00:00"}}
            """);

        var webhook = await client.Webhooks.GetAsync("abc", "America/New_York", TestContext.Current.CancellationToken);

        Assert.Equal("abc", webhook.Id);
        Assert.Equal(WebhookAuthType.OAuth2, webhook.AuthType);
        Assert.Equal("delivery", webhook.Events!.Single());
        Assert.True(webhook.Active);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/webhooks/abc?timezone=America%2FNew_York",
            handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Незнакомый_способ_авторизации_не_роняет_разбор()
    {
        var (client, _) = CreateClient(
            HttpStatusCode.OK,
            """{"results":{"id":"abc","auth_type":"mtls"}}""");

        var webhook = await client.Webhooks.GetAsync("abc", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(WebhookAuthType.Unknown, webhook.AuthType);
    }

    [Fact]
    public async Task Список_возвращает_все_вебхуки()
    {
        var (client, handler) = CreateClient(
            HttpStatusCode.OK,
            """{"results":[{"id":"a"},{"id":"b"}]}""");

        var webhooks = await client.Webhooks.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b"], webhooks.Select(webhook => webhook.Id));
        Assert.Equal("https://api.sparkpost.com/api/v1/webhooks", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Изменение_уходит_put_запросом()
    {
        var (client, handler) = CreateClient(HttpStatusCode.OK, """{"results":{"id":"abc"}}""");

        await client.Webhooks.UpdateAsync(
            "abc",
            new WebhookRequest
            {
                Name = "Renamed",
                Target = "https://client.example.com/hooks",
                Events = [SparkPostEventTypes.Delivery],
                Active = false,
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Equal("https://api.sparkpost.com/api/v1/webhooks/abc", handler.LastRequest.RequestUri!.ToString());
        Assert.False((bool?)JsonNode.Parse(handler.LastBody!)!["active"]);
    }

    [Fact]
    public async Task Удаление_уходит_delete_запросом()
    {
        var (client, handler) = CreateClient(HttpStatusCode.NoContent, string.Empty);

        await client.Webhooks.DeleteAsync("abc", TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal("https://api.sparkpost.com/api/v1/webhooks/abc", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task Проверка_отправляет_тестовый_батч_и_возвращает_ответ_эндпоинта()
    {
        var (client, handler) = CreateClient(
            HttpStatusCode.OK,
            """
            {"results":{"msg":"Test POST to endpoint succeeded",
             "response":{"status":200,"headers":{"Content-Type":"text/plain"},"body":"OK"}}}
            """);

        var result = await client.Webhooks.ValidateAsync("abc", TestContext.Current.CancellationToken);

        Assert.Equal("Test POST to endpoint succeeded", result.Msg);
        Assert.Equal(200, result.Response!.Status);
        Assert.Equal("OK", result.Response.Body);
        Assert.Equal("https://api.sparkpost.com/api/v1/webhooks/abc/validate", handler.LastRequest!.RequestUri!.ToString());
        AssertJson("""[{"msys":{}}]""", handler.LastBody!);
    }

    [Fact]
    public async Task Статус_батчей_разбирает_и_числа_и_строки()
    {
        // response_code в документации описан числом, а в примерах приходит строкой.
        var (client, handler) = CreateClient(
            HttpStatusCode.OK,
            """
            {"results":[
              {"batch_id":"032d33","ts":"2014-07-30T21:38:08.000Z","attempts":7,"response_code":"200","latency":160},
              {"batch_id":"13c676","ts":"2014-07-30T20:38:08.000Z","attempts":2,"failure_code":400,"response_code":400}
            ]}
            """);

        var statuses = await client.Webhooks.GetBatchStatusAsync("abc", 10, TestContext.Current.CancellationToken);

        Assert.Equal(200, statuses[0].ResponseCode);
        Assert.Equal(160, statuses[0].Latency);
        Assert.Equal(400, statuses[1].FailureCode);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/webhooks/abc/batch-status?limit=10",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Примеры_событий_возвращаются_как_есть()
    {
        var (client, handler) = CreateClient(
            HttpStatusCode.OK,
            """{"results":[{"msys":{"message_event":{"type":"delivery"}}}]}""");

        var samples = await client.Webhooks.GetEventSamplesAsync(
            [SparkPostEventTypes.Delivery, SparkPostEventTypes.Bounce],
            TestContext.Current.CancellationToken);

        Assert.Equal("delivery", (string?)samples[0]!["msys"]!["message_event"]!["type"]);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/webhooks/events/samples?events=delivery%2Cbounce",
            handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Ошибка_создания_доносит_код_и_сообщение()
    {
        var (client, _) = CreateClient(
            HttpStatusCode.BadRequest,
            """{"errors":[{"code":400,"message":"POST to webhook tokens URL failed"}]}""");

        var exception = await Assert.ThrowsAsync<SparkPostApiException>(
            () => client.Webhooks.CreateAsync(
                new WebhookRequest
                {
                    Name = "x",
                    Target = "https://x.io/h",
                    Events = [SparkPostEventTypes.Delivery],
                },
                TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("POST to webhook tokens URL failed", exception.Errors.Single().Message);
    }

    private static void AssertJson(string expected, string actual) =>
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected)),
            $"Отправлено не то тело:{Environment.NewLine}{actual}");

    private static (SparkPostClient Client, FakeHttpMessageHandler Handler) CreateClient(
        HttpStatusCode statusCode,
        string body)
    {
        var handler = FakeHttpMessageHandler.Returning(statusCode, body);
        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });
        return (client, handler);
    }
}
