using Microsoft.Extensions.Options;
using SparkPoster;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Регистрация SparkPoster в контейнере.</summary>
public static class SparkPostServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует <see cref="ISparkPostClient"/> поверх <c>IHttpClientFactory</c>.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configure">Настройка параметров: ключ, адрес, субаккаунт по умолчанию.</param>
    /// <returns>
    /// Построитель HTTP-клиента — на нём навешиваются повторы и таймауты, например
    /// <c>.AddStandardResilienceHandler()</c> из <c>Microsoft.Extensions.Http.Resilience</c>.
    /// Повторы безопасны: клиент проставляет заголовок <c>Idempotency-Key</c> на каждую отправку.
    /// </returns>
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
