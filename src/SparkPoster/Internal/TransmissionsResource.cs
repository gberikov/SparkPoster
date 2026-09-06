using System.Net.Http.Json;

namespace SparkPoster.Internal;

internal sealed class TransmissionsResource : ITransmissions
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// The SparkPost documentation spells the replay header differently in different places,
    /// so both spellings are checked.
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

        if (idempotencyKey is not null && !IsValidIdempotencyKey(idempotencyKey))
        {
            throw new ArgumentException(
                "The idempotency key must be 1 to 255 characters from [A-Za-z0-9._-]. "
                + "Hash or encode a business identifier that contains anything else.",
                nameof(idempotencyKey));
        }

        using var request = _requester.CreateRequest(HttpMethod.Post, "transmissions");
        request.Content = JsonContent.Create(transmission, SparkPostJsonContext.Default.TransmissionRequest);

        // The key lives on the HttpRequestMessage, so it survives a retry inside a
        // DelegatingHandler: the very same request is sent again with the very same key,
        // and SparkPost replays the original result instead of sending a second message.
        request.Headers.TryAddWithoutValidation(
            IdempotencyKeyHeader,
            idempotencyKey ?? Guid.NewGuid().ToString("N"));

        using var response = await _requester.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var result = await SparkPostRequester
            .ReadResultsAsync(response, SparkPostJsonContext.Default.TransmissionEnvelope, cancellationToken)
            .ConfigureAwait(false);

        return result with { IsIdempotentReplay = IsReplay(response) };
    }

    public async Task DeleteByCampaignAsync(string campaignId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);

        var query = new QueryBuilder();
        query.Add("campaign_id", campaignId);

        using var request = _requester.CreateRequest(HttpMethod.Delete, $"transmissions{query}");

        using var response = await _requester.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The documented contract is <c>^[A-Za-z0-9._-]{1,255}$</c>. Checked here rather than left to
    /// the server: the header goes out through <c>TryAddWithoutValidation</c>, so a key built from
    /// an unchecked business identifier could otherwise carry a CR/LF and inject a header.
    /// </summary>
    private static bool IsValidIdempotencyKey(string key) =>
        key.Length is >= 1 and <= 255
        && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

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
