using System.Net.Http.Json;

namespace SparkPoster.Internal;

internal sealed class TransmissionsResource : ITransmissions
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Документация SparkPost называет заголовок повтора по-разному в разных местах,
    /// поэтому проверяем оба варианта.
    /// </summary>
    private static readonly string[] ReplayHeaders = ["X-Idempotent-Replayed", "Idempotency-Replay"];

    private readonly SparkPostRequester _requester;

    public TransmissionsResource(SparkPostRequester requester) => _requester = requester;

    public async Task<TransmissionResponse> SendAsync(
        TransmissionRequest transmission,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transmission);

        using var request = _requester.CreateRequest(HttpMethod.Post, "transmissions");
        request.Content = JsonContent.Create(transmission, SparkPostJsonContext.Default.TransmissionRequest);

        // Ключ ставится на HttpRequestMessage, поэтому переживает повтор в DelegatingHandler:
        // повторно отправляется тот же запрос с тем же ключом, и SparkPost возвращает
        // исходный результат вместо второго письма.
        request.Headers.TryAddWithoutValidation(
            IdempotencyKeyHeader,
            idempotencyKey ?? Guid.NewGuid().ToString("N"));

        using var response = await _requester.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var result = await SparkPostRequester
            .ReadResultsAsync(response, SparkPostJsonContext.Default.TransmissionEnvelope, cancellationToken)
            .ConfigureAwait(false);

        return result with { IsIdempotentReplay = IsReplay(response) };
    }

    private static bool IsReplay(HttpResponseMessage response)
    {
        foreach (var header in ReplayHeaders)
        {
            if (response.Headers.TryGetValues(header, out var values)
                && values.Any(value => bool.TryParse(value, out var flag) && flag))
            {
                return true;
            }
        }

        return false;
    }
}
