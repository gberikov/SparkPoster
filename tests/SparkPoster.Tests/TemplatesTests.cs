using System.Net;
using System.Text.Json.Nodes;

namespace SparkPoster.Tests;

public sealed class TemplatesTests
{
    [Fact]
    public async Task Create_posts_the_template_and_returns_its_id()
    {
        var (client, handler) = CreateClient("""{"results":{"id":"welcome"}}""");

        var id = await client.Templates.CreateAsync(
            new TemplateRequest
            {
                Id = "welcome",
                Name = "Welcome",
                Content = new TemplateContent
                {
                    From = new Address { Email = "noreply@example.com", Name = "Example" },
                    Subject = "Hi {{name}}",
                    Html = "<p>Hi {{name}}</p>",
                },
                Options = new TemplateOptions { Transactional = true },
            },
            TestContext.Current.CancellationToken);

        Assert.Equal("welcome", id);
        Assert.Equal("https://api.sparkpost.com/api/v1/templates", handler.LastRequest!.RequestUri!.ToString());

        AssertJson(
            """
            {
              "id": "welcome",
              "name": "Welcome",
              "content": {
                "from": { "email": "noreply@example.com", "name": "Example" },
                "subject": "Hi {{name}}",
                "html": "<p>Hi {{name}}</p>"
              },
              "options": { "transactional": true }
            }
            """,
            handler.LastBody!);
    }

    [Fact]
    public async Task Get_requests_the_draft_when_asked()
    {
        var (client, handler) = CreateClient(
            """{"results":{"id":"welcome","name":"Welcome","has_draft":true,"has_published":true}}""");

        var template = await client.Templates.GetAsync("welcome", draft: true, TestContext.Current.CancellationToken);

        Assert.Equal("welcome", template.Id);
        Assert.True(template.HasDraft);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/templates/welcome?draft=true",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task List_returns_templates()
    {
        var (client, handler) = CreateClient("""{"results":[{"id":"a"},{"id":"b"}]}""");

        var templates = await client.Templates.ListAsync(
            sharedWithSubaccounts: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(["a", "b"], templates.Select(template => template.Id));
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/templates?shared_with_subaccounts=true",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task List_puts_both_filters_in_the_query()
    {
        var (client, handler) = CreateClient("""{"results":[]}""");

        await client.Templates.ListAsync(
            draft: false,
            sharedWithSubaccounts: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://api.sparkpost.com/api/v1/templates?draft=false&shared_with_subaccounts=true",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task List_without_filters_sends_no_query()
    {
        var (client, handler) = CreateClient("""{"results":[]}""");

        await client.Templates.ListAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("https://api.sparkpost.com/api/v1/templates", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Update_targets_the_published_version_when_asked()
    {
        var (client, handler) = CreateClient("""{"results":{"id":"welcome"}}""");

        await client.Templates.UpdateAsync(
            "welcome",
            new TemplateRequest { Name = "Renamed" },
            updatePublished: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/templates/welcome?update_published=true",
            handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task Update_sends_no_query_when_the_draft_stays_the_target()
    {
        var (client, handler) = CreateClient("""{"results":{"id":"welcome"}}""");

        await client.Templates.UpdateAsync(
            "welcome",
            new TemplateRequest { Name = "Renamed" },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            "https://api.sparkpost.com/api/v1/templates/welcome",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Publish_sends_nothing_but_the_published_flag()
    {
        var (client, handler) = CreateClient("""{"results":{"id":"welcome"}}""");

        await client.Templates.PublishAsync("welcome", TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        AssertJson("""{"published":true}""", handler.LastBody!);
    }

    [Fact]
    public async Task Delete_is_sent_as_delete()
    {
        var (client, handler) = CreateClient(string.Empty, HttpStatusCode.NoContent);

        await client.Templates.DeleteAsync("welcome", TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
        Assert.Equal("https://api.sparkpost.com/api/v1/templates/welcome", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task Preview_renders_the_template_with_substitution_data()
    {
        var (client, handler) = CreateClient(
            """{"results":{"subject":"Hi Bob","html":"<p>Hi Bob</p>","from":{"email":"noreply@example.com"}}}""");

        var content = await client.Templates.PreviewAsync(
            "welcome",
            new JsonObject { ["name"] = "Bob" },
            draft: true,
            TestContext.Current.CancellationToken);

        Assert.Equal("Hi Bob", content.Subject);
        Assert.Equal("<p>Hi Bob</p>", content.Html);
        Assert.Equal("noreply@example.com", content.From!.Email);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/templates/welcome/preview?draft=true",
            handler.LastRequest!.RequestUri!.ToString());
        AssertJson("""{"substitution_data":{"name":"Bob"}}""", handler.LastBody!);
    }

    [Fact]
    public async Task Preview_accepts_substitution_data_that_belongs_to_another_tree()
    {
        var (client, handler) = CreateClient("""{"results":{"subject":"Hi Bob"}}""");
        var tree = new JsonObject { ["data"] = new JsonObject { ["name"] = "Bob" } };

        await client.Templates.PreviewAsync(
            "welcome",
            tree["data"],
            cancellationToken: TestContext.Current.CancellationToken);

        AssertJson("""{"substitution_data":{"name":"Bob"}}""", handler.LastBody!);
    }

    [Fact]
    public async Task Deleting_a_template_in_use_surfaces_the_conflict()
    {
        var (client, _) = CreateClient(
            """{"errors":[{"message":"resource conflict","description":"Template is in use by msg generation"}]}""",
            HttpStatusCode.Conflict);

        var exception = await Assert.ThrowsAsync<SparkPostApiException>(
            () => client.Templates.DeleteAsync("welcome", TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Contains("in use", exception.Message, StringComparison.Ordinal);
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
