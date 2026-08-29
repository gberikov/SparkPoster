using System.Runtime.CompilerServices;
using SparkPoster.Webhooks;

namespace SparkPoster.Internal;

internal sealed class EventsResource : IEvents
{
    private const string Path = "events/message";

    private readonly SparkPostRequester _requester;

    public EventsResource(SparkPostRequester requester) => _requester = requester;

    public async Task<EventPage> GetPageAsync(
        EventQuery? query = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        using var request = _requester.CreateRequest(HttpMethod.Get, Path + BuildQuery(query, cursor));

        var document = await _requester
            .SendAndReadDocumentAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return new EventPage
        {
            Events = SparkPostEventReader.ReadFlat(document["results"]),
            TotalCount = (int?)document["total_count"] ?? 0,
            NextCursor = QueryBuilder.ExtractNextCursor(document["links"]),
        };
    }

    public async IAsyncEnumerable<SparkPostEvent> SearchAsync(
        EventQuery? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? cursor = null;

        do
        {
            var page = await GetPageAsync(query, cursor, cancellationToken).ConfigureAwait(false);

            foreach (var @event in page.Events)
            {
                yield return @event;
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);
    }

    private static string BuildQuery(EventQuery? query, string? cursor)
    {
        var builder = new QueryBuilder();

        // The cursor always travels explicitly: "initial" is what starts a paged walk.
        builder.Add("cursor", cursor ?? "initial");

        if (query is null)
        {
            return builder.ToString();
        }

        builder.AddTimestamp("from", query.From);
        builder.AddTimestamp("to", query.To);

        // Timestamps are converted to UTC above, so the timezone is declared once for both.
        if (query.From is not null || query.To is not null)
        {
            builder.Add("timezone", "UTC");
        }

        builder.AddList("events", query.Events);
        builder.AddList("recipients", query.Recipients);
        builder.AddList("from_addresses", query.FromAddresses);
        builder.AddList("campaigns", query.Campaigns);
        builder.AddList("templates", query.Templates);
        builder.AddList("transmission_ids", query.TransmissionIds);
        builder.AddList("message_ids", query.MessageIds);
        builder.AddList("bounce_classes", query.BounceClasses);
        builder.AddList("reasons", query.Reasons);
        builder.AddList("sending_ips", query.SendingIps);
        builder.AddList("ip_pools", query.IpPools);
        builder.AddList("subaccounts", query.Subaccounts);
        builder.AddList("sending_domains", query.SendingDomains);
        builder.AddList("recipient_domains", query.RecipientDomains);
        builder.AddList("subjects", query.Subjects);
        builder.AddList("mailbox_providers", query.MailboxProviders);
        builder.AddList("mailbox_provider_regions", query.MailboxProviderRegions);
        builder.AddList("ab_tests", query.AbTests);
        builder.Add("delimiter", query.Delimiter);
        builder.Add("per_page", query.PerPage);

        if (query.AdditionalFilters is { } filters)
        {
            foreach (var (name, value) in filters)
            {
                builder.Add(name, value);
            }
        }

        return builder.ToString();
    }
}
