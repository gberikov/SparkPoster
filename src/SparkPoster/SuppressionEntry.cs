namespace SparkPoster;

/// <summary>The kinds of suppression.</summary>
/// <remarks>
/// Constants rather than an enum, for the same reason as <see cref="SparkPostEventTypes"/>:
/// the vocabulary belongs to the server.
/// </remarks>
public static class SuppressionTypes
{
    /// <summary>Suppresses transactional mail — password resets, receipts and the like.</summary>
    public const string Transactional = "transactional";

    /// <summary>Suppresses bulk mail — newsletters, campaigns.</summary>
    public const string NonTransactional = "non_transactional";
}

/// <summary>
/// An entry on the suppression list: an address that must not be mailed.
/// </summary>
/// <remarks>
/// Suppressions are account-wide by default. Set <see cref="ListId"/> to scope an entry to a
/// single mailing list instead. Each subaccount keeps its own independent list, which is
/// reached through <see cref="ISparkPostClient.ForSubaccount"/>.
/// </remarks>
public sealed record SuppressionEntry
{
    /// <summary>The suppressed address.</summary>
    public required string Recipient { get; init; }

    /// <summary>
    /// What is suppressed: <see cref="SuppressionTypes.Transactional"/> or
    /// <see cref="SuppressionTypes.NonTransactional"/>.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>Why the address was suppressed.</summary>
    public string? Description { get; init; }

    /// <summary>Who added the entry: a bounce, a complaint, a manual upload, the API.</summary>
    public string? Source { get; init; }

    /// <summary>The mailing list the entry is scoped to. Account-wide when absent.</summary>
    public string? ListId { get; init; }

    /// <summary>When the entry was created.</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>When the entry was last changed.</summary>
    public DateTimeOffset? Updated { get; init; }

    /// <summary>The subaccount the entry belongs to.</summary>
    public int? SubaccountId { get; init; }
}

/// <summary>A search over the suppression list.</summary>
public sealed record SuppressionQuery
{
    /// <summary>Only entries changed at or after this moment.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Only entries changed at or before this moment.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Only addresses in this domain.</summary>
    public string? Domain { get; init; }

    /// <summary>Only entries added by these sources.</summary>
    public IReadOnlyList<string>? Sources { get; init; }

    /// <summary>Only entries of these kinds.</summary>
    public IReadOnlyList<string>? Types { get; init; }

    /// <summary>Only entries whose description contains this text.</summary>
    public string? Description { get; init; }

    /// <summary>Match <see cref="Description"/> exactly rather than as a substring.</summary>
    public bool? DescriptionStrict { get; init; }

    /// <summary>Only entries scoped to this mailing list.</summary>
    public string? ListId { get; init; }

    /// <summary>How many entries to return per page.</summary>
    public int? PerPage { get; init; }
}

/// <summary>One page of suppression search results.</summary>
public sealed record SuppressionPage
{
    /// <summary>The entries on this page.</summary>
    public required IReadOnlyList<SuppressionEntry> Entries { get; init; }

    /// <summary>How many entries matched the query in total.</summary>
    public int TotalCount { get; init; }

    /// <summary>The cursor for the next page, or <c>null</c> when this was the last one.</summary>
    public string? NextCursor { get; init; }
}

/// <summary>How many entries the suppression list holds.</summary>
public sealed record SuppressionSummary
{
    /// <summary>Entries suppressing transactional mail.</summary>
    public int Transactional { get; init; }

    /// <summary>Entries suppressing bulk mail.</summary>
    public int NonTransactional { get; init; }
}
