using SparkPoster.Internal;

namespace SparkPoster;

/// <summary>The SparkPost client.</summary>
public interface ISparkPostClient
{
    /// <summary>Sending mail.</summary>
    ITransmissions Transmissions { get; }

    /// <summary>Managing event webhooks.</summary>
    IWebhooks Webhooks { get; }

    /// <summary>Searching recent events.</summary>
    IEvents Events { get; }

    /// <summary>Managing stored templates.</summary>
    ITemplates Templates { get; }

    /// <summary>Managing the suppression list.</summary>
    ISuppressionList SuppressionList { get; }

    /// <summary>Managing sending domains.</summary>
    ISendingDomains SendingDomains { get; }

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
    /// <summary>
    /// The fallback for the <see cref="SparkPostClient(SparkPostOptions)"/> constructor.
    /// <c>PooledConnectionLifetime</c> is what keeps a static client from pinning a stale DNS
    /// record forever — the one real hazard of not going through <c>IHttpClientFactory</c>.
    /// </summary>
    private static readonly HttpClient SharedHttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    });

    private readonly HttpClient _httpClient;
    private readonly SparkPostOptions _options;

    /// <summary>Creates a client.</summary>
    /// <param name="httpClient">The HTTP client. Retries, timeouts and circuit breaking are configured on it.</param>
    /// <param name="options">Configuration: key, base address, default subaccount.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="httpClient"/> or <paramref name="options"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <see cref="SparkPostOptions.ApiKey"/> is empty or contains a line break, or
    /// <see cref="SparkPostOptions.BaseUrl"/> is not an absolute URI.
    /// </exception>
    public SparkPostClient(HttpClient httpClient, SparkPostOptions options)
        : this(httpClient, options, subaccountId: null)
    {
    }

    /// <summary>
    /// Creates a client over a shared <see cref="HttpClient"/>. For console applications,
    /// scripts and tests — anything without a service container.
    /// </summary>
    /// <param name="options">Configuration: key, base address, default subaccount.</param>
    /// <remarks>
    /// The <see cref="HttpClient"/> is static and shared by every client built this way, which
    /// is what it should be: one per process, not one per call. It carries no retries and no
    /// timeout policy — inside a host, register through <c>AddSparkPost</c> instead and hang
    /// <c>AddStandardResilienceHandler()</c> off the builder it returns.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <see cref="SparkPostOptions.ApiKey"/> is empty or contains a line break, or
    /// <see cref="SparkPostOptions.BaseUrl"/> is not an absolute URI.
    /// </exception>
    public SparkPostClient(SparkPostOptions options)
        : this(SharedHttpClient, options, subaccountId: null)
    {
    }

    private SparkPostClient(HttpClient httpClient, SparkPostOptions options, int? subaccountId)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        _httpClient = httpClient;
        _options = options;

        var requester = new SparkPostRequester(httpClient, options, subaccountId);
        Transmissions = new TransmissionsResource(requester);
        Webhooks = new WebhooksResource(requester);
        Events = new EventsResource(requester);
        Templates = new TemplatesResource(requester);
        SuppressionList = new SuppressionListResource(requester);
        SendingDomains = new SendingDomainsResource(requester);
    }

    /// <inheritdoc />
    public ITransmissions Transmissions { get; }

    /// <inheritdoc />
    public IWebhooks Webhooks { get; }

    /// <inheritdoc />
    public IEvents Events { get; }

    /// <inheritdoc />
    public ITemplates Templates { get; }

    /// <inheritdoc />
    public ISuppressionList SuppressionList { get; }

    /// <inheritdoc />
    public ISendingDomains SendingDomains { get; }

    /// <inheritdoc />
    public ISparkPostClient ForSubaccount(int subaccountId) =>
        new SparkPostClient(_httpClient, _options, subaccountId);

    /// <summary>
    /// Fails on a missing key here rather than letting SparkPost answer 401 to a request that
    /// carried an empty Authorization header — the two look nothing alike in a log.
    /// The line-break check is defence in depth: the key goes onto the request through
    /// <c>TryAddWithoutValidation</c>, which by design validates nothing.
    /// The base address is checked here for the same reason: a relative URI survives until the
    /// first request and then fails with an <see cref="InvalidOperationException"/> naming no option.
    /// </summary>
    private static void ValidateOptions(SparkPostOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException(
                "SparkPostOptions.ApiKey is not set. Read it from an environment variable or a secret store.",
                nameof(options));
        }

        if (options.ApiKey.AsSpan().ContainsAny('\r', '\n'))
        {
            throw new ArgumentException(
                "SparkPostOptions.ApiKey contains a line break. It was most likely read from a file "
                + "together with its trailing newline.",
                nameof(options));
        }

        if (options.BaseUrl is null || !options.BaseUrl.IsAbsoluteUri)
        {
            throw new ArgumentException(
                "SparkPostOptions.BaseUrl must be an absolute URI, for example https://api.sparkpost.com/api/v1/.",
                nameof(options));
        }
    }
}
