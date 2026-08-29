using SparkPoster.Webhooks;

namespace SparkPoster;

/// <summary>
/// A search over recent events.
/// </summary>
/// <remarks>
/// <para>
/// Event data is retained for 10 days. For anything older, use the metrics endpoints.
/// </para>
/// <para>
/// Several filters support keyword search: <see cref="Campaigns"/>, <see cref="Templates"/>,
/// <see cref="IpPools"/>, <see cref="Reasons"/>, <see cref="RecipientDomains"/>,
/// <see cref="SendingDomains"/>, <see cref="Subjects"/>, <see cref="MailboxProviders"/>,
/// <see cref="MailboxProviderRegions"/> and <see cref="AbTests"/>. A keyword matches whole
/// words split on spaces, dashes and underscores, so <c>blackfriday</c> matches
/// <c>blackfriday-specials</c> while <c>friday</c> matches nothing.
/// </para>
/// <para>
/// SparkPost caps the request URI at 4096 characters, so a very long list of filters has to
/// be split across several queries.
/// </para>
/// </remarks>
public sealed record EventQuery
{
    /// <summary>The start of the time range. Defaults to 24 hours ago.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>The end of the time range. Defaults to one minute ago.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>The event types to return. All of them when omitted.</summary>
    public IReadOnlyList<string>? Events { get; init; }

    /// <summary>Recipient addresses.</summary>
    public IReadOnlyList<string>? Recipients { get; init; }

    /// <summary>Sender addresses.</summary>
    public IReadOnlyList<string>? FromAddresses { get; init; }

    /// <summary>Campaigns.</summary>
    public IReadOnlyList<string>? Campaigns { get; init; }

    /// <summary>Templates.</summary>
    public IReadOnlyList<string>? Templates { get; init; }

    /// <summary>Transmission identifiers.</summary>
    public IReadOnlyList<string>? TransmissionIds { get; init; }

    /// <summary>Message identifiers.</summary>
    public IReadOnlyList<string>? MessageIds { get; init; }

    /// <summary>Bounce classification codes.</summary>
    public IReadOnlyList<string>? BounceClasses { get; init; }

    /// <summary>Bounce reasons.</summary>
    public IReadOnlyList<string>? Reasons { get; init; }

    /// <summary>Sending IP addresses.</summary>
    public IReadOnlyList<string>? SendingIps { get; init; }

    /// <summary>IP pools.</summary>
    public IReadOnlyList<string>? IpPools { get; init; }

    /// <summary>
    /// Subaccount identifiers. This endpoint filters by query parameter and ignores the
    /// <c>X-MSYS-SUBACCOUNT</c> header, so <see cref="ISparkPostClient.ForSubaccount"/>
    /// has no effect here.
    /// </summary>
    public IReadOnlyList<string>? Subaccounts { get; init; }

    /// <summary>Sending domains.</summary>
    public IReadOnlyList<string>? SendingDomains { get; init; }

    /// <summary>Recipient domains.</summary>
    public IReadOnlyList<string>? RecipientDomains { get; init; }

    /// <summary>Subject lines.</summary>
    public IReadOnlyList<string>? Subjects { get; init; }

    /// <summary>Mailbox providers.</summary>
    public IReadOnlyList<string>? MailboxProviders { get; init; }

    /// <summary>Mailbox provider regions.</summary>
    public IReadOnlyList<string>? MailboxProviderRegions { get; init; }

    /// <summary>A/B tests.</summary>
    public IReadOnlyList<string>? AbTests { get; init; }

    /// <summary>How many events to return per page. The maximum is 10,000.</summary>
    public int? PerPage { get; init; }

    /// <summary>The separator used inside list parameters. A comma by default.</summary>
    public string? Delimiter { get; init; }

    /// <summary>
    /// Any other query parameters, for filters this library does not know about yet.
    /// Values are sent as-is.
    /// </summary>
    public IReadOnlyDictionary<string, string>? AdditionalFilters { get; init; }
}

/// <summary>One page of event search results.</summary>
public sealed record EventPage
{
    /// <summary>The events on this page.</summary>
    public required IReadOnlyList<SparkPostEvent> Events { get; init; }

    /// <summary>How many events matched the query in total.</summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// The cursor for the next page, or <c>null</c> when this was the last one.
    /// Store it to resume the walk later — that is exactly why this low-level method
    /// exists alongside <see cref="IEvents.SearchAsync"/>.
    /// </summary>
    public string? NextCursor { get; init; }
}
