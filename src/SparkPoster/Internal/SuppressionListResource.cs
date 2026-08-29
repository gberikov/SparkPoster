using System.Net.Http.Json;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace SparkPoster.Internal;

internal sealed class SuppressionListResource : ISuppressionList
{
    private const string Path = "suppression-list";

    private readonly SparkPostRequester _requester;

    public SuppressionListResource(SparkPostRequester requester) => _requester = requester;

    public async Task UpsertAsync(SuppressionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Recipient);

        using var request = _requester.CreateRequest(
            HttpMethod.Put,
            $"{Path}/{Uri.EscapeDataString(entry.Recipient)}");

        // The address travels in the path, so the body only carries what can be changed.
        request.Content = JsonContent.Create(
            new SuppressionUpsert { Type = entry.Type, Description = entry.Description, ListId = entry.ListId },
            SparkPostJsonContext.Default.SuppressionUpsert);

        await _requester.SendIgnoringResultAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertManyAsync(
        IEnumerable<SuppressionEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        using var request = _requester.CreateRequest(HttpMethod.Put, Path);
        request.Content = JsonContent.Create(
            new SuppressionBulkUpsert { Recipients = [.. entries] },
            SparkPostJsonContext.Default.SuppressionBulkUpsert);

        await _requester.SendIgnoringResultAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SuppressionEntry>> GetAsync(
        string recipient,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);

        using var request = _requester.CreateRequest(HttpMethod.Get, $"{Path}/{Uri.EscapeDataString(recipient)}");

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.SuppressionListEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        string recipient,
        string? listId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipient);

        var query = new QueryBuilder();
        query.Add("list_id", listId);

        using var request = _requester.CreateRequest(
            HttpMethod.Delete,
            $"{Path}/{Uri.EscapeDataString(recipient)}{query}");

        await _requester.SendIgnoringResultAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SuppressionPage> SearchPageAsync(
        SuppressionQuery? query = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        using var request = _requester.CreateRequest(HttpMethod.Get, Path + BuildQuery(query, cursor));

        var document = await _requester
            .SendAndReadDocumentAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return new SuppressionPage
        {
            Entries = document["results"].Deserialize(SparkPostJsonContext.Default.IReadOnlyListSuppressionEntry) ?? [],
            TotalCount = (int?)document["total_count"] ?? 0,
            NextCursor = QueryBuilder.ExtractNextCursor(document["links"]),
        };
    }

    public async IAsyncEnumerable<SuppressionEntry> SearchAsync(
        SuppressionQuery? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? cursor = null;

        do
        {
            var page = await SearchPageAsync(query, cursor, cancellationToken).ConfigureAwait(false);

            foreach (var entry in page.Entries)
            {
                yield return entry;
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);
    }

    public async Task<SuppressionSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        using var request = _requester.CreateRequest(HttpMethod.Get, $"{Path}/summary");

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.SuppressionSummaryEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildQuery(SuppressionQuery? query, string? cursor)
    {
        var builder = new QueryBuilder();

        builder.Add("cursor", cursor ?? "initial");

        if (query is null)
        {
            return builder.ToString();
        }

        builder.AddTimestamp("from", query.From);
        builder.AddTimestamp("to", query.To);

        if (query.From is not null || query.To is not null)
        {
            builder.Add("timezone", "UTC");
        }

        builder.Add("domain", query.Domain);
        builder.AddList("sources", query.Sources);
        builder.AddList("types", query.Types);
        builder.Add("description", query.Description);
        builder.Add("description_strict", query.DescriptionStrict);
        builder.Add("list_id", query.ListId);
        builder.Add("per_page", query.PerPage);

        return builder.ToString();
    }
}

/// <summary>The body of a single-address upsert; the address itself travels in the path.</summary>
internal sealed record SuppressionUpsert
{
    public string? Type { get; init; }

    public string? Description { get; init; }

    public string? ListId { get; init; }
}

/// <summary>The body of a bulk upsert.</summary>
internal sealed record SuppressionBulkUpsert
{
    public required IReadOnlyList<SuppressionEntry> Recipients { get; init; }
}
