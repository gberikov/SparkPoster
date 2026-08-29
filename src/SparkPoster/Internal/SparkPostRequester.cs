using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace SparkPoster.Internal;

/// <summary>
/// The shared HTTP plumbing: authorization, the subaccount header, unwrapping the response
/// envelope and turning error statuses into exceptions.
/// </summary>
internal sealed class SparkPostRequester
{
    private const string AuthorizationHeader = "Authorization";
    private const string SubaccountHeader = "X-MSYS-SUBACCOUNT";
    private const string UserAgentHeader = "User-Agent";

    /// <summary>
    /// Identifies the library in SparkPost's logs, which is the first thing their support asks
    /// for. The informational version carries the commit hash after a '+'; that part is dropped.
    /// </summary>
    private static readonly string UserAgent = BuildUserAgent();

    private readonly HttpClient _http;
    private readonly SparkPostOptions _options;
    private readonly Uri _baseUrl;
    private readonly int? _subaccountId;

    public SparkPostRequester(HttpClient http, SparkPostOptions options, int? subaccountId)
    {
        _http = http;
        _options = options;
        _baseUrl = NormalizeBaseUrl(options.BaseUrl);
        _subaccountId = subaccountId;
    }

    /// <summary>
    /// A base address without a trailing slash silently loses its last segment when a relative
    /// path is resolved against it: an enterprise endpoint typed as <c>https://host/api/v1</c>
    /// would send every request to <c>https://host/api/</c>.
    /// </summary>
    private static Uri NormalizeBaseUrl(Uri baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);

        return baseUrl.AbsoluteUri.EndsWith('/') ? baseUrl : new Uri(baseUrl.AbsoluteUri + "/");
    }

    private static string BuildUserAgent()
    {
        var version = typeof(SparkPostRequester).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(version))
        {
            return "SparkPoster";
        }

        var metadata = version.IndexOf('+', StringComparison.Ordinal);

        return "SparkPoster/" + (metadata < 0 ? version : version[..metadata]);
    }

    /// <summary>
    /// Builds a request. Headers go on the request itself rather than into
    /// <see cref="HttpClient.DefaultRequestHeaders"/>: the client is shared by the whole
    /// application, while a subaccount is the scope of one particular call.
    /// </summary>
    public HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var request = new HttpRequestMessage(method, new Uri(_baseUrl, relativePath));
        request.Headers.TryAddWithoutValidation(AuthorizationHeader, _options.ApiKey);
        request.Headers.TryAddWithoutValidation(UserAgentHeader, UserAgent);

        var subaccount = _subaccountId ?? _options.SubaccountId;
        if (subaccount is not null)
        {
            request.Headers.TryAddWithoutValidation(
                SubaccountHeader,
                subaccount.Value.ToString(CultureInfo.InvariantCulture));
        }

        return request;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiExceptionAsync(response, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>Sends a request and unwraps the response envelope.</summary>
    public async Task<T> SendAndReadAsync<T>(
        HttpRequestMessage request,
        JsonTypeInfo<SparkPostEnvelope<T>> typeInfo,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await ReadResultsAsync(response, typeInfo, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends a request whose response body is not needed.</summary>
    public async Task SendIgnoringResultAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a request and returns the contents of <c>results</c> without mapping it to a model.
    /// Needed for reference endpoints whose shape changes along with the API.
    /// </summary>
    public async Task<JsonNode> SendAndReadRawAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var document = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new SparkPostException("SparkPost returned an empty response.");

            return document["results"] ?? document;
        }
    }

    /// <summary>
    /// Sends a request and returns the whole response document. Needed where the envelope
    /// itself carries data, such as the events cursor in <c>links.next</c>.
    /// </summary>
    public async Task<JsonNode> SendAndReadDocumentAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
                ?? throw new SparkPostException("SparkPost returned an empty response.");
        }
    }

    public static async Task<T> ReadResultsAsync<T>(
        HttpResponseMessage response,
        JsonTypeInfo<SparkPostEnvelope<T>> typeInfo,
        CancellationToken cancellationToken)
    {
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var envelope = await JsonSerializer
                .DeserializeAsync(stream, typeInfo, cancellationToken)
                .ConfigureAwait(false);

            return envelope is { Results: not null }
                ? envelope.Results
                : throw new SparkPostException("SparkPost returned a response without a results field.");
        }
    }

    private static async Task ThrowApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await ReadBodySafelyAsync(response, cancellationToken).ConfigureAwait(false);
        var errors = ParseErrors(body);
        var statusCode = response.StatusCode;

        // 420 means the sending limit was exceeded; it is not part of the HttpStatusCode enum.
        var isRateLimited = (int)statusCode is 429 or 420;
        var retryAfter = isRateLimited ? ReadRetryAfter(response) : null;

        // The caller gets an exception instead of the response and can no longer dispose it.
        response.Dispose();

        throw isRateLimited
            ? new SparkPostRateLimitException(statusCode, errors, body, retryAfter)
            : new SparkPostApiException(statusCode, errors, body);
    }

    private static async Task<string?> ReadBodySafelyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the error body. The body may not be JSON at all — a proxy stub, an HTML page;
    /// the exception still has to reach the caller either way, so failures are swallowed here
    /// and the raw body travels on in <see cref="SparkPostApiException.RawBody"/>.
    /// </summary>
    private static List<SparkPostError> ParseErrors(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return [];
        }

        try
        {
            var envelope = JsonSerializer.Deserialize(body, SparkPostJsonContext.Default.SparkPostErrorEnvelope);
            return envelope?.Errors ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }
}
