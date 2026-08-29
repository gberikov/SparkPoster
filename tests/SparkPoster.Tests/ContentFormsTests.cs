using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SparkPoster.Tests;

public sealed class ContentFormsTests
{
    private const string SuccessBody =
        """{"results":{"total_rejected_recipients":0,"total_accepted_recipients":1,"id":"11668787484950529"}}""";

    private static readonly JsonSerializerOptions SnakeCase = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    [Fact]
    public async Task Шаблон_сериализуется_в_template_id()
    {
        var body = await CaptureBodyAsync(
            Transmission.Create()
                .To("user@example.com")
                .Template("black_friday", useDraft: true)
                .Build());

        AssertJson(
            """
            {
              "content": { "template_id": "black_friday", "use_draft_template": true },
              "recipients": [ { "address": { "email": "user@example.com" } } ]
            }
            """,
            body);
    }

    [Fact]
    public async Task AB_тест_сериализуется_в_ab_test_id()
    {
        var body = await CaptureBodyAsync(
            Transmission.Create().To("user@example.com").AbTest("subject-test").Build());

        AssertJson(
            """
            {
              "content": { "ab_test_id": "subject-test" },
              "recipients": [ { "address": { "email": "user@example.com" } } ]
            }
            """,
            body);
    }

    [Fact]
    public async Task Rfc822_сериализуется_в_email_rfc822()
    {
        var body = await CaptureBodyAsync(
            Transmission.Create().To("user@example.com").RawRfc822("From: a@b.io\r\n\r\nhi").Build());

        AssertJson(
            """
            {
              "content": { "email_rfc822": "From: a@b.io\r\n\r\nhi" },
              "recipients": [ { "address": { "email": "user@example.com" } } ]
            }
            """,
            body);
    }

    [Fact]
    public async Task Вложение_кодируется_в_base64()
    {
        var body = await CaptureBodyAsync(
            Transmission.Create()
                .From("noreply@example.com")
                .To("user@example.com")
                .Html("<p>hi</p>")
                .Attach(Attachment.FromBytes("billing.pdf", "application/pdf", "hello"u8))
                .Build());

        var attachment = JsonNode.Parse(body)!["content"]!["attachments"]![0]!;

        Assert.Equal("billing.pdf", (string?)attachment["name"]);
        Assert.Equal("application/pdf", (string?)attachment["type"]);
        Assert.Equal("hello", Encoding.UTF8.GetString(Convert.FromBase64String((string)attachment["data"]!)));
    }

    [Fact]
    public async Task Встроенное_изображение_уходит_в_inline_images()
    {
        var body = await CaptureBodyAsync(
            Transmission.Create()
                .From("noreply@example.com")
                .To("user@example.com")
                .Html("""<img src="cid:logo.png">""")
                .InlineImage(Attachment.FromBytes("logo.png", "image/png", [1, 2, 3]))
                .Build());

        Assert.Equal("logo.png", (string?)JsonNode.Parse(body)!["content"]!["inline_images"]![0]!["name"]);
    }

    [Fact]
    public async Task Копии_становятся_получателями_с_подменённым_заголовком_To()
    {
        var body = await CaptureBodyAsync(
            Transmission.Create()
                .From("noreply@example.com")
                .To("user@example.com", "User")
                .Cc("boss@example.com")
                .Bcc("audit@example.com")
                .Html("<p>hi</p>")
                .Build());

        var json = JsonNode.Parse(body)!;
        var recipients = json["recipients"]!.AsArray();

        Assert.Equal(3, recipients.Count);
        Assert.Null(recipients[0]!["address"]!["header_to"]);
        Assert.Equal("\"User\" <user@example.com>", (string?)recipients[1]!["address"]!["header_to"]);
        Assert.Equal("\"User\" <user@example.com>", (string?)recipients[2]!["address"]!["header_to"]);

        // Скрытый получатель нигде не упоминается, получатель копии — упоминается.
        Assert.Equal("boss@example.com", (string?)json["content"]!["headers"]!["CC"]);
    }

    [Fact]
    public async Task Сохранённый_список_сериализуется_объектом()
    {
        var body = await CaptureBodyAsync(
            Transmission.Create().RecipientList("christmas_2013").Template("promo").Build());

        AssertJson(
            """
            {
              "content": { "template_id": "promo" },
              "recipients": { "list_id": "christmas_2013" }
            }
            """,
            body);
    }

    [Fact]
    public async Task Отложенная_отправка_попадает_в_options()
    {
        var startTime = new DateTimeOffset(2026, 9, 1, 14, 30, 0, TimeSpan.FromHours(6));

        var body = await CaptureBodyAsync(
            Transmission.Create()
                .From("noreply@example.com")
                .To("user@example.com")
                .Html("<p>hi</p>")
                .StartTime(startTime)
                .Build());

        Assert.Equal(
            startTime,
            DateTimeOffset.Parse((string)JsonNode.Parse(body)!["options"]!["start_time"]!, provider: null));
    }

    [Fact]
    public void Смешение_форм_содержимого_отвергается()
    {
        var builder = Transmission.Create()
            .From("noreply@example.com")
            .To("user@example.com")
            .Html("<p>hi</p>")
            .Template("promo");

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains("inline-содержимое", exception.Message, StringComparison.Ordinal);
        Assert.Contains("шаблон", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Сохранённый_список_вместе_с_явными_получателями_отвергается()
    {
        var builder = Transmission.Create()
            .To("user@example.com")
            .RecipientList("christmas_2013")
            .Template("promo");

        var exception = Assert.Throws<InvalidOperationException>(builder.Build);

        Assert.Contains("дважды", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Шаблону_отправитель_не_нужен()
    {
        var request = Transmission.Create().To("user@example.com").Template("promo").Build();

        Assert.Null(request.Content.From);
        Assert.Equal("promo", request.Content.TemplateId);
    }

    [Fact]
    public async Task Отмена_кампании_уходит_delete_запросом()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.NoContent, string.Empty);
        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });

        await client.Transmissions.DeleteByCampaignAsync("christmas 2026", TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        // Именно AbsoluteUri: ToString() показывает раскодированную форму, а на провод
        // уходит экранированная.
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/transmissions?campaign_id=christmas%202026",
            handler.LastRequest.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Запрос_переживает_сериализацию_и_обратный_разбор()
    {
        var original = Transmission.Create()
            .From("noreply@example.com", "Example")
            .To("user@example.com")
            .Subject("Hi")
            .Html("<p>hi</p>")
            .Build();

        var json = await CaptureBodyAsync(original);

        var restored = JsonSerializer.Deserialize<TransmissionRequest>(json, SnakeCase);

        Assert.NotNull(restored);
        Assert.Equal("user@example.com", restored.Recipients.Items!.Single().Address.Email);
        Assert.Equal("noreply@example.com", restored.Content.From!.Email);
        Assert.Equal("<p>hi</p>", restored.Content.Html);
    }

    [Fact]
    public async Task Сохранённый_список_переживает_обратный_разбор()
    {
        var json = await CaptureBodyAsync(
            Transmission.Create().RecipientList("christmas_2013").Template("promo").Build());

        var restored = JsonSerializer.Deserialize<TransmissionRequest>(json, SnakeCase);

        Assert.Equal("christmas_2013", restored!.Recipients.ListId);
        Assert.Null(restored.Recipients.Items);
    }

    private static void AssertJson(string expected, string actual) =>
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected)),
            $"Отправлено не то тело:{Environment.NewLine}{actual}");

    private static async Task<string> CaptureBodyAsync(TransmissionRequest transmission)
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);
        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });

        await client.Transmissions.SendAsync(transmission, cancellationToken: TestContext.Current.CancellationToken);

        return handler.LastBody!;
    }
}
