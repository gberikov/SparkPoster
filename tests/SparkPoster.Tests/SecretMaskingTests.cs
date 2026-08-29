using System.Text.Json.Nodes;

namespace SparkPoster.Tests;

/// <summary>
/// The compiler-generated record <c>ToString()</c> prints every property, and a webhook read
/// back from the API travels straight into <c>logger.LogInformation("{Webhook}", webhook)</c>.
/// </summary>
public sealed class SecretMaskingTests
{
    [Fact]
    public void Webhook_credentials_do_not_print_the_password_or_the_token()
    {
        var credentials = new WebhookAuthCredentials
        {
            Username = "hook",
            Password = "p@ssw0rd",
            AccessToken = "ya29.token",
            ExpiresIn = 3600,
        };

        var text = credentials.ToString();

        Assert.DoesNotContain("p@ssw0rd", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ya29.token", text, StringComparison.Ordinal);
        Assert.Contains("hook", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_webhook_does_not_print_its_own_credentials()
    {
        // The nested record's ToString() is what the outer one calls, so masking the leaf is enough.
        var webhook = new Webhook
        {
            Id = "1",
            Name = "Delivery events",
            AuthCredentials = new WebhookAuthCredentials { Username = "hook", Password = "p@ssw0rd" },
        };

        Assert.DoesNotContain("p@ssw0rd", webhook.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_oauth_token_request_does_not_print_its_client_secret()
    {
        // Body is a JsonNode, and a JsonNode prints its JSON — unlike a dictionary.
        var details = new WebhookAuthRequestDetails
        {
            Url = "https://auth.example.com/token",
            Body = JsonNode.Parse("""{"client_id":"id","client_secret":"shhh"}"""),
        };

        var text = details.ToString();

        Assert.DoesNotContain("shhh", text, StringComparison.Ordinal);
        Assert.Contains("https://auth.example.com/token", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Dkim_private_key_is_masked()
    {
        // Bringing your own key pair means the private key travels inside the request record.
        // The fixture deliberately does not look like a PEM key: secret scanners flag the
        // header on its own, and the test is about masking, not about the key's format.
        const string privateKey = "dkim-private-key-material";

        var dkim = new DkimSettings
        {
            Selector = "scph0126",
            Public = "MIGf…",
            Private = privateKey,
        };

        var text = dkim.ToString();

        Assert.DoesNotContain(privateKey, text, StringComparison.Ordinal);
        Assert.Contains("Private = ***", text, StringComparison.Ordinal);
        Assert.Contains("scph0126", text, StringComparison.Ordinal);

        var request = new SendingDomainRequest { Domain = "example.com", Dkim = dkim };

        Assert.DoesNotContain(privateKey, request.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_attachment_prints_the_size_of_its_payload_rather_than_the_payload()
    {
        var attachment = Attachment.FromBytes("invoice.pdf", "application/pdf", "confidential"u8);

        var text = attachment.ToString();

        Assert.DoesNotContain(attachment.Data, text, StringComparison.Ordinal);
        Assert.Contains("invoice.pdf", text, StringComparison.Ordinal);
    }
}
