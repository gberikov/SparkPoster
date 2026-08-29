using Microsoft.Extensions.DependencyInjection;

namespace SparkPoster.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddSparkPost_регистрирует_клиента()
    {
        var services = new ServiceCollection();

        services.AddSparkPost(options =>
        {
            options.ApiKey = "test-key";
            options.BaseUrl = SparkPostEndpoints.Eu;
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<ISparkPostClient>();

        Assert.NotNull(client);
        Assert.NotNull(client.Transmissions);
    }

    [Fact]
    public void AddSparkPost_возвращает_построитель_http_клиента()
    {
        var services = new ServiceCollection();

        var builder = services.AddSparkPost(options => options.ApiKey = "test-key");

        Assert.NotNull(builder);
        Assert.NotEmpty(builder.Name);
    }
}
