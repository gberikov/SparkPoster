using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
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
            NextCursor = ExtractNextCursor(document["links"]),
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

    /// <summary>
    /// Pulls the cursor out of the next-page link. SparkPost hands back a ready-made URL with
    /// every filter already in place; only the cursor is carried over, so that the caller can
    /// store it and resume later.
    /// </summary>
    private static string? ExtractNextCursor(JsonNode? links)
    {
        var next = links switch
        {
            JsonObject linkObject => (string?)linkObject["next"],
            JsonArray linkArray => linkArray
                .OfType<JsonObject>()
                .FirstOrDefault(link => (string?)link["rel"] is "next")?["href"]?.GetValue<string>(),
            _ => null,
        };

        if (string.IsNullOrEmpty(next))
        {
            return null;
        }

        var queryStart = next.IndexOf('?', StringComparison.Ordinal);

        if (queryStart < 0)
        {
            return null;
        }

        foreach (var pair in next[(queryStart + 1)..].Split('&'))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0 && pair[..separator] is "cursor")
            {
                return Uri.UnescapeDataString(pair[(separator + 1)..]);
            }
        }

        return null;
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

        if (query.PerPage is { } perPage)
        {
            builder.Add("per_page", perPage.ToString(CultureInfo.InvariantCulture));
        }

        if (query.AdditionalFilters is { } filters)
        {
            foreach (var (name, value) in filters)
            {
                builder.Add(name, value);
            }
        }

        return builder.ToString();
    }

    private sealed class QueryBuilder
    {
        private readonly StringBuilder _builder = new();

        public void Add(string name, string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            _builder.Append(_builder.Length == 0 ? '?' : '&')
                .Append(name)
                .Append('=')
                .Append(Uri.EscapeDataString(value));
        }

        public void AddList(string name, IReadOnlyList<string>? values)
        {
            if (values is { Count: > 0 })
            {
                Add(name, string.Join(',', values));
            }
        }

        /// <summary>
        /// SparkPost expects <c>YYYY-MM-DDTHH:MM</c> and reads it in the account time zone
        /// unless a timezone parameter says otherwise, so the value is converted to UTC and
        /// the caller declares the timezone alongside it.
        /// </summary>
        public void AddTimestamp(string name, DateTimeOffset? value)
        {
            if (value is { } moment)
            {
                Add(name, moment.UtcDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
            }
        }

        public override string ToString() => _builder.ToString();
    }
}
