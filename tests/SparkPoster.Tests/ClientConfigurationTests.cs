using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SparkPoster.Tests;

public sealed class ClientConfigurationTests
{
    private const string SuccessBody =
        """{"results":{"id":"1","total_accepted_recipients":1,"total_rejected_recipients":0}}""";

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_api_key_is_rejected_at_construction(string apiKey)
    {
        // Otherwise the request goes out with an empty Authorization header and comes back as
        // SparkPost's 401, which says nothing about the real cause.
        var exception = Assert.Throws<ArgumentException>(
            () => new SparkPostClient(new SparkPostOptions { ApiKey = apiKey }));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void An_api_key_with_a_line_break_is_rejected()
    {
        // The usual cause: the key was read from a file together with its trailing newline.
        var exception = Assert.Throws<ArgumentException>(
            () => new SparkPostClient(new SparkPostOptions { ApiKey = "abc123\n" }));

        Assert.DoesNotContain("abc123", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Relative_base_url_is_rejected()
    {
        // "SparkPost:BaseUrl": "api/v1" in appsettings would otherwise reach the first request
        // and fail there with InvalidOperationException, which names no option.
        var options = new SparkPostOptions { ApiKey = "key", BaseUrl = new Uri("api/v1", UriKind.Relative) };

        var exception = Assert.Throws<ArgumentException>(() => new SparkPostClient(options));

        Assert.Equal("options", exception.ParamName);
        Assert.Contains("BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_base_url_without_a_trailing_slash_keeps_its_last_segment()
    {
        // Uri resolution would otherwise drop "v1" and post to https://host/api/transmissions.
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);

        var client = new SparkPostClient(
            handler.CreateClient(),
            new SparkPostOptions { ApiKey = "test-key", BaseUrl = new Uri("https://host.example/api/v1") });

        await client.Transmissions.SendAsync(
            Transmission.Create().From("a@example.com").To("b@example.com").Text("hi").Build(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("https://host.example/api/v1/transmissions", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task Every_request_identifies_the_library()
    {
        var handler = FakeHttpMessageHandler.Returning(HttpStatusCode.OK, SuccessBody);

        var client = new SparkPostClient(handler.CreateClient(), new SparkPostOptions { ApiKey = "test-key" });

        await client.Transmissions.SendAsync(
            Transmission.Create().From("a@example.com").To("b@example.com").Text("hi").Build(),
            cancellationToken: TestContext.Current.CancellationToken);

        var userAgent = handler.LastRequest!.Headers.GetValues("User-Agent").Single();

        Assert.StartsWith("SparkPoster", userAgent, StringComparison.Ordinal);
    }

    [Fact]
    public void Options_bind_from_a_configuration_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SparkPost:ApiKey"] = "test-key",
                ["SparkPost:BaseUrl"] = "https://api.eu.sparkpost.com/api/v1/",
                ["SparkPost:SubaccountId"] = "42",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSparkPost(configuration.GetSection("SparkPost"));

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ISparkPostClient>());
    }
}
