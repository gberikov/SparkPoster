using System.Net;
using System.Text.Json.Nodes;

namespace SparkPoster.Tests;

public sealed class SendingDomainsTests
{
    [Fact]
    public async Task Create_returns_the_generated_dkim_key_to_publish()
    {
        var (client, handler) = CreateClient(
            """
            {"results":{"message":"Successfully Created domain.","domain":"example.com",
             "dkim":{"selector":"scph0126","public":"MIGfMA0...","headers":"from:to:subject:date"}}}
            """);

        var domain = await client.SendingDomains.CreateAsync(
            new SendingDomainRequest { Domain = "example.com", SharedWithSubaccounts = true },
            TestContext.Current.CancellationToken);

        Assert.Equal("example.com", domain.Domain);
        Assert.Equal("scph0126", domain.Dkim!.Selector);
        Assert.Equal("MIGfMA0...", domain.Dkim.Public);
        Assert.Equal("https://api.sparkpost.com/api/v1/sending-domains", handler.LastRequest!.RequestUri!.ToString());

        AssertJson("""{"domain":"example.com","shared_with_subaccounts":true}""", handler.LastBody!);
    }

    [Fact]
    public async Task Get_reports_the_verification_state()
    {
        var (client, _) = CreateClient(
            """
            {"results":{"domain":"example.com","status":{"ownership_verified":true,
             "dkim_status":"valid","spf_status":"unverified","cname_status":"pending",
             "compliance_status":"valid"}}}
            """);

        var domain = await client.SendingDomains.GetAsync("example.com", TestContext.Current.CancellationToken);

        Assert.True(domain.Status!.OwnershipVerified);
        Assert.Equal("valid", domain.Status.DkimStatus);
        Assert.Equal("unverified", domain.Status.SpfStatus);
    }

    [Fact]
    public async Task List_returns_domains()
    {
        var (client, handler) = CreateClient("""{"results":[{"domain":"a.io"},{"domain":"b.io"}]}""");

        var domains = await client.SendingDomains.ListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["a.io", "b.io"], domains.Select(domain => domain.Domain));
        Assert.Equal("https://api.sparkpost.com/api/v1/sending-domains", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Update_is_sent_as_put()
    {
        var (client, handler) = CreateClient("""{"results":{"message":"Successfully Updated Domain."}}""");

        await client.SendingDomains.UpdateAsync(
            "example.com",
            new SendingDomainRequest { TrackingDomain = "click.example.com" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/sending-domains/example.com",
            handler.LastRequest.RequestUri!.ToString());
        AssertJson("""{"tracking_domain":"click.example.com"}""", handler.LastBody!);
    }

    [Fact]
    public async Task Delete_is_sent_as_delete()
    {
        var (client, handler) = CreateClient(string.Empty, HttpStatusCode.NoContent);

        await client.SendingDomains.DeleteAsync("example.com", TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task Verify_defaults_to_the_dns_checks()
    {
        var (client, handler) = CreateClient(
            """{"results":{"ownership_verified":true,"dkim_status":"valid","spf_status":"valid"}}""");

        var status = await client.SendingDomains.VerifyAsync(
            "example.com",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(status.OwnershipVerified);
        Assert.Equal("valid", status.DkimStatus);
        Assert.Equal(
            "https://api.sparkpost.com/api/v1/sending-domains/example.com/verify",
            handler.LastRequest!.RequestUri!.ToString());

        // The DNS checks are side-effect free, unlike the mailbox ones, which send mail.
        AssertJson("""{"dkim_verify":true,"spf_verify":true}""", handler.LastBody!);
    }

    [Fact]
    public async Task Verify_passes_a_mailbox_token_back()
    {
        var (client, handler) = CreateClient("""{"results":{"abuse_at_status":"valid"}}""");

        await client.SendingDomains.VerifyAsync(
            "example.com",
            new DomainVerificationOptions { AbuseAtToken = "abc123" },
            TestContext.Current.CancellationToken);

        AssertJson("""{"abuse_at_token":"abc123"}""", handler.LastBody!);
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
