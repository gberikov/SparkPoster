using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace SparkPoster.Internal;

internal sealed class WebhooksResource : IWebhooks
{
    /// <summary>The batch SparkPost posts to the target when validating a webhook.</summary>
    private const string ValidationBatch = """[{"msys":{}}]""";

    private readonly SparkPostRequester _requester;

    public WebhooksResource(SparkPostRequester requester) => _requester = requester;

    public async Task<string> CreateAsync(WebhookRequest webhook, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(webhook);

        using var request = _requester.CreateRequest(HttpMethod.Post, "webhooks");
        request.Content = JsonContent.Create(webhook, SparkPostJsonContext.Default.WebhookRequest);

        var created = await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.WebhookIdEnvelope, cancellationToken)
            .ConfigureAwait(false);

        return created.Id;
    }

    public async Task<Webhook> GetAsync(
        string id,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var request = _requester.CreateRequest(
            HttpMethod.Get,
            $"webhooks/{Uri.EscapeDataString(id)}{TimezoneQuery(timezone)}");

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.WebhookEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Webhook>> ListAsync(
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        using var request = _requester.CreateRequest(HttpMethod.Get, $"webhooks{TimezoneQuery(timezone)}");

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.WebhookListEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(string id, WebhookRequest webhook, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(webhook);

        using var request = _requester.CreateRequest(HttpMethod.Put, $"webhooks/{Uri.EscapeDataString(id)}");
        request.Content = JsonContent.Create(webhook, SparkPostJsonContext.Default.WebhookRequest);

        await _requester.SendIgnoringResultAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var request = _requester.CreateRequest(HttpMethod.Delete, $"webhooks/{Uri.EscapeDataString(id)}");

        await _requester.SendIgnoringResultAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WebhookValidationResult> ValidateAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var request = _requester.CreateRequest(
            HttpMethod.Post,
            $"webhooks/{Uri.EscapeDataString(id)}/validate");
        request.Content = new StringContent(ValidationBatch, Encoding.UTF8, "application/json");

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.WebhookValidationEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WebhookBatchStatus>> GetBatchStatusAsync(
        string id,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var query = limit is null ? string.Empty : $"?limit={limit.Value.ToString(CultureInfo.InvariantCulture)}";

        using var request = _requester.CreateRequest(
            HttpMethod.Get,
            $"webhooks/{Uri.EscapeDataString(id)}/batch-status{query}");

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.WebhookBatchStatusListEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<JsonNode> GetEventsDocumentationAsync(CancellationToken cancellationToken = default)
    {
        using var request = _requester.CreateRequest(HttpMethod.Get, "webhooks/events/documentation");

        return await _requester.SendAndReadRawAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonNode> GetEventSamplesAsync(
        IEnumerable<string>? events = null,
        CancellationToken cancellationToken = default)
    {
        var query = events is null ? string.Empty : BuildEventsQuery(events);

        using var request = _requester.CreateRequest(HttpMethod.Get, $"webhooks/events/samples{query}");

        return await _requester.SendAndReadRawAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildEventsQuery(IEnumerable<string> events)
    {
        var list = string.Join(',', events);

        return string.IsNullOrEmpty(list) ? string.Empty : $"?events={Uri.EscapeDataString(list)}";
    }

    private static string TimezoneQuery(string? timezone) =>
        string.IsNullOrWhiteSpace(timezone) ? string.Empty : $"?timezone={Uri.EscapeDataString(timezone)}";
}
