using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace SparkPoster.Internal;

internal sealed class TemplatesResource : ITemplates
{
    private readonly SparkPostRequester _requester;

    public TemplatesResource(SparkPostRequester requester) => _requester = requester;

    public async Task<string> CreateAsync(TemplateRequest definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        using var request = _requester.CreateRequest(HttpMethod.Post, "templates");
        request.Content = JsonContent.Create(definition, SparkPostJsonContext.Default.TemplateRequest);

        var created = await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.CreatedResourceEnvelope, cancellationToken)
            .ConfigureAwait(false);

        return created.Id;
    }

    public async Task<Template> GetAsync(
        string id,
        bool? draft = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var query = draft is null ? string.Empty : $"?draft={Bool(draft.Value)}";

        using var request = _requester.CreateRequest(HttpMethod.Get, $"templates/{Uri.EscapeDataString(id)}{query}");

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.TemplateEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Template>> ListAsync(
        bool? draft = null,
        bool? sharedWithSubaccounts = null,
        CancellationToken cancellationToken = default)
    {
        var query = new StringBuilder();

        if (draft is not null)
        {
            query.Append("?draft=").Append(Bool(draft.Value));
        }

        if (sharedWithSubaccounts is not null)
        {
            query.Append(query.Length == 0 ? '?' : '&')
                .Append("shared_with_subaccounts=")
                .Append(Bool(sharedWithSubaccounts.Value));
        }

        using var request = _requester.CreateRequest(HttpMethod.Get, $"templates{query}");

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.TemplateListEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        string id,
        TemplateRequest definition,
        bool updatePublished = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(definition);

        var query = updatePublished ? "?update_published=true" : string.Empty;

        using var request = _requester.CreateRequest(HttpMethod.Put, $"templates/{Uri.EscapeDataString(id)}{query}");
        request.Content = JsonContent.Create(definition, SparkPostJsonContext.Default.TemplateRequest);

        await _requester.SendIgnoringResultAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task PublishAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        // Publishing is an update carrying nothing but published=true: SparkPost has no
        // separate endpoint for it.
        using var request = _requester.CreateRequest(HttpMethod.Put, $"templates/{Uri.EscapeDataString(id)}");
        request.Content = JsonContent.Create(
            new TemplateRequest { Published = true },
            SparkPostJsonContext.Default.TemplateRequest);

        await _requester.SendIgnoringResultAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        using var request = _requester.CreateRequest(HttpMethod.Delete, $"templates/{Uri.EscapeDataString(id)}");

        await _requester.SendIgnoringResultAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TemplateContent> PreviewAsync(
        string id,
        JsonNode? substitutionData = null,
        bool? draft = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var query = draft is null ? string.Empty : $"?draft={Bool(draft.Value)}";

        using var request = _requester.CreateRequest(
            HttpMethod.Post,
            $"templates/{Uri.EscapeDataString(id)}/preview{query}");

        // DeepClone: a JsonNode has a single parent, so a node taken from the caller's own tree
        // cannot be attached here directly.
        var body = new JsonObject { ["substitution_data"] = substitutionData?.DeepClone() ?? new JsonObject() };
        request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.TemplateContentEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
