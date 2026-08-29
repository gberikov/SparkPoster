using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace SparkPoster.Internal;

/// <summary>
/// Общая обвязка HTTP: авторизация, заголовок субаккаунта, разбор конверта ответа
/// и превращение кодов ошибок в исключения.
/// </summary>
internal sealed class SparkPostRequester
{
    private const string AuthorizationHeader = "Authorization";
    private const string SubaccountHeader = "X-MSYS-SUBACCOUNT";

    private readonly HttpClient _http;
    private readonly SparkPostOptions _options;
    private readonly int? _subaccountId;

    public SparkPostRequester(HttpClient http, SparkPostOptions options, int? subaccountId)
    {
        _http = http;
        _options = options;
        _subaccountId = subaccountId;
    }

    /// <summary>
    /// Собирает запрос. Заголовки ставятся именно на запрос, а не в
    /// <see cref="HttpClient.DefaultRequestHeaders"/>: клиент один на всё приложение,
    /// а субаккаунт — область действия конкретного вызова.
    /// </summary>
    public HttpRequestMessage CreateRequest(HttpMethod method, string relativePath)
    {
        var request = new HttpRequestMessage(method, new Uri(_options.BaseUrl, relativePath));
        request.Headers.TryAddWithoutValidation(AuthorizationHeader, _options.ApiKey);

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
                : throw new SparkPostException("SparkPost вернул ответ без поля results.");
        }
    }

    private static async Task ThrowApiExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await ReadBodySafelyAsync(response, cancellationToken).ConfigureAwait(false);
        var errors = ParseErrors(body);
        var statusCode = response.StatusCode;

        // 420 — превышен лимит отправки, его нет в перечислении HttpStatusCode.
        var isRateLimited = (int)statusCode is 429 or 420;
        var retryAfter = isRateLimited ? ReadRetryAfter(response) : null;

        // Вызывающий получает исключение вместо ответа и уже не сможет его освободить.
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
    /// Разбирает тело ошибки. Тело может оказаться не JSON — заглушка прокси, HTML-страница;
    /// в этом случае исключение всё равно должно долететь до вызывающего, поэтому здесь молчим,
    /// а сырое тело уезжает в <see cref="SparkPostApiException.RawBody"/>.
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
