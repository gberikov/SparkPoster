using SparkPoster.Internal;

namespace SparkPoster;

/// <summary>The SparkPost client.</summary>
public interface ISparkPostClient
{
    /// <summary>Sending mail.</summary>
    ITransmissions Transmissions { get; }

    /// <summary>Managing event webhooks.</summary>
    IWebhooks Webhooks { get; }

    /// <summary>
    /// Returns a client that acts on behalf of a subaccount.
    /// </summary>
    /// <param name="subaccountId">The subaccount identifier.</param>
    /// <returns>A client scoped to the subaccount, sharing the same <see cref="HttpClient"/>.</returns>
    /// <remarks>
    /// The scope is carried by the <c>X-MSYS-SUBACCOUNT</c> header. Metrics and Events ignore
    /// that header — they filter subaccounts through the <c>subaccounts</c> query parameter,
    /// so <see cref="ForSubaccount"/> has no effect on them.
    /// </remarks>
    ISparkPostClient ForSubaccount(int subaccountId);
}

/// <summary>
/// The SparkPost client. Thread-safe and meant to be registered as a singleton over an
/// <see cref="HttpClient"/> from <c>IHttpClientFactory</c>.
/// </summary>
public sealed class SparkPostClient : ISparkPostClient
{
    private readonly HttpClient _httpClient;
    private readonly SparkPostOptions _options;

    /// <summary>Creates a client.</summary>
    /// <param name="httpClient">The HTTP client. Retries, timeouts and circuit breaking are configured on it.</param>
    /// <param name="options">Configuration: key, base address, default subaccount.</param>
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
