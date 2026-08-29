namespace SparkPoster;

/// <summary>Managing the suppression list.</summary>
/// <remarks>
/// A search that spans the primary account and all subaccounts can lag by up to 20 minutes.
/// A lookup scoped to one list — through <see cref="ISparkPostClient.ForSubaccount"/> — is
/// always up to date.
/// </remarks>
public interface ISuppressionList
{
    /// <summary>Adds or updates one entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the entry is stored.</returns>
    Task UpsertAsync(SuppressionEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Adds or updates several entries in one request.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the entries are stored.</returns>
    Task UpsertManyAsync(IEnumerable<SuppressionEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>Returns the entries for one address.</summary>
    /// <param name="recipient">The address.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// The entries found. An address can hold one entry per kind, which is why this returns
    /// a list rather than a single entry.
    /// </returns>
    /// <exception cref="SparkPostApiException">The address is not on the list (404).</exception>
    Task<IReadOnlyList<SuppressionEntry>> GetAsync(string recipient, CancellationToken cancellationToken = default);

    /// <summary>Removes an address from the list.</summary>
    /// <param name="recipient">The address.</param>
    /// <param name="listId">Removes only the entry scoped to this mailing list.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the entry is removed.</returns>
    Task DeleteAsync(string recipient, string? listId = null, CancellationToken cancellationToken = default);

    /// <summary>Returns a single page of search results.</summary>
    /// <param name="query">The search. The whole list when omitted.</param>
    /// <param name="cursor">The page cursor; <c>null</c> starts from the beginning.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page of entries.</returns>
    Task<SuppressionPage> SearchPageAsync(
        SuppressionQuery? query = null,
        string? cursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>Walks every matching entry, fetching pages as needed.</summary>
    /// <param name="query">The search. The whole list when omitted.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The matching entries.</returns>
    IAsyncEnumerable<SuppressionEntry> SearchAsync(
        SuppressionQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns how many entries the list holds, broken down by kind.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The summary.</returns>
    Task<SuppressionSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
