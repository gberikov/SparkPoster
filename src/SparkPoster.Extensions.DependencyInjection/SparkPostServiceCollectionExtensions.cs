using Microsoft.Extensions.Options;
using SparkPoster;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers SparkPoster in the service container.</summary>
public static class SparkPostServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ISparkPostClient"/> on top of <c>IHttpClientFactory</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the key, base address and default subaccount.</param>
    /// <returns>
    /// The HTTP client builder — attach retries and timeouts to it, for example
    /// <c>.AddStandardResilienceHandler()</c> from <c>Microsoft.Extensions.Http.Resilience</c>.
    /// Retries are safe: the client stamps an <c>Idempotency-Key</c> header on every send.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.
    /// </exception>
    public static IHttpClientBuilder AddSparkPost(this IServiceCollection services, Action<SparkPostOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);

        return services.AddHttpClient<ISparkPostClient, SparkPostClient>(
            (httpClient, provider) =>
                new SparkPostClient(httpClient, provider.GetRequiredService<IOptions<SparkPostOptions>>().Value));
    }
}
