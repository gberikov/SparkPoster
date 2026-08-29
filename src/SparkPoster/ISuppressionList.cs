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
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The recipient of <paramref name="entry"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">SparkPost answered with an error status.</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task UpsertAsync(SuppressionEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Adds or updates several entries in one request.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the entries are stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <c>null</c>.</exception>
    /// <exception cref="SparkPostApiException">
    /// SparkPost answered with an error status: one of the entries did not pass validation (400).
    /// The response does not say which entries were stored before the error, so treat the whole
    /// batch as being of unknown state and repeat it — an upsert is idempotent.
    /// </exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task UpsertManyAsync(IEnumerable<SuppressionEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>Returns the entries for one address.</summary>
    /// <param name="recipient">The address.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// The entries found. An address can hold one entry per kind, which is why this returns
    /// a list rather than a single entry.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="recipient"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="recipient"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">The address is not on the list (404).</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task<IReadOnlyList<SuppressionEntry>> GetAsync(string recipient, CancellationToken cancellationToken = default);

    /// <summary>Removes an address from the list.</summary>
    /// <param name="recipient">The address.</param>
    /// <param name="listId">Removes only the entry scoped to this mailing list.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the entry is removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="recipient"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="recipient"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">
    /// The address is not on the list (404), or it is a compliance entry, which SparkPost
    /// refuses to remove through the API (403).
    /// </exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task DeleteAsync(string recipient, string? listId = null, CancellationToken cancellationToken = default);

    /// <summary>Returns a single page of search results.</summary>
    /// <param name="query">The search. The whole list when omitted.</param>
    /// <param name="cursor">The page cursor; <c>null</c> starts from the beginning.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page of entries.</returns>
    /// <exception cref="SparkPostApiException">
    /// SparkPost answered with an error status: the query did not pass validation (400).
    /// </exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task<SuppressionPage> SearchPageAsync(
        SuppressionQuery? query = null,
        string? cursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>Walks every matching entry, fetching pages as needed.</summary>
    /// <param name="query">The search. The whole list when omitted.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The matching entries.</returns>
    /// <exception cref="SparkPostApiException">SparkPost answered with an error status.</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    /// <remarks>
    /// Lazy: pages are fetched as the enumeration advances, so both exceptions surface while
    /// enumerating rather than at the call itself, and they can surface on any page — put the
    /// <c>await foreach</c> inside the <c>try</c>, not just this call.
    /// </remarks>
    IAsyncEnumerable<SuppressionEntry> SearchAsync(
        SuppressionQuery? query = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns how many entries the list holds, broken down by kind.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The summary.</returns>
    /// <exception cref="SparkPostApiException">SparkPost answered with an error status.</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task<SuppressionSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
