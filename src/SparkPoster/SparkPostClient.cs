using SparkPoster.Internal;

namespace SparkPoster;

/// <summary>Клиент SparkPost.</summary>
public interface ISparkPostClient
{
    /// <summary>Отправка писем.</summary>
    ITransmissions Transmissions { get; }

    /// <summary>Управление вебхуками событий.</summary>
    IWebhooks Webhooks { get; }

    /// <summary>
    /// Возвращает клиента, работающего от лица субаккаунта.
    /// </summary>
    /// <param name="subaccountId">Идентификатор субаккаунта.</param>
    /// <returns>Клиент с областью действия субаккаунта, поверх того же <see cref="HttpClient"/>.</returns>
    /// <remarks>
    /// Область действия задаётся заголовком <c>X-MSYS-SUBACCOUNT</c>. Metrics и Events
    /// этот заголовок игнорируют — там субаккаунты фильтруются query-параметром
    /// <c>subaccounts</c>, и вызов <see cref="ForSubaccount"/> на них не повлияет.
    /// </remarks>
    ISparkPostClient ForSubaccount(int subaccountId);
}

/// <summary>
/// Клиент SparkPost. Потокобезопасен и рассчитан на регистрацию одним экземпляром
/// (singleton) поверх <see cref="HttpClient"/> из <c>IHttpClientFactory</c>.
/// </summary>
public sealed class SparkPostClient : ISparkPostClient
{
    private readonly HttpClient _httpClient;
    private readonly SparkPostOptions _options;

    /// <summary>Создаёт клиента.</summary>
    /// <param name="httpClient">HTTP-клиент. Повторы, таймауты и circuit breaker настраиваются на нём.</param>
    /// <param name="options">Настройки: ключ, адрес, субаккаунт по умолчанию.</param>
    public SparkPostClient(HttpClient httpClient, SparkPostOptions options)
        : this(httpClient, options, subaccountId: null)
    {
    }

    private SparkPostClient(HttpClient httpClient, SparkPostOptions options, int? subaccountId)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;

        var requester = new SparkPostRequester(httpClient, options, subaccountId);
        Transmissions = new TransmissionsResource(requester);
        Webhooks = new WebhooksResource(requester);
    }

    /// <inheritdoc />
    public ITransmissions Transmissions { get; }

    /// <inheritdoc />
    public IWebhooks Webhooks { get; }

    /// <inheritdoc />
    public ISparkPostClient ForSubaccount(int subaccountId) =>
        new SparkPostClient(_httpClient, _options, subaccountId);
}
