using SparkPoster.Webhooks;

namespace SparkPoster;

/// <summary>Searching recent events.</summary>
public interface IEvents
{
    /// <summary>Returns a single page of events.</summary>
    /// <param name="query">The search. All events of the last 24 hours when omitted.</param>
    /// <param name="cursor">
    /// The page cursor. Start with <c>null</c> (equivalent to <c>initial</c>) and pass
    /// <see cref="EventPage.NextCursor"/> for each subsequent page.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page of events.</returns>
    /// <exception cref="SparkPostApiException">
    /// SparkPost answered with an error status: the query did not pass validation (400),
    /// or the cursor has expired.
    /// </exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    /// <remarks>
    /// Use this overload when the cursor has to be persisted — resuming from a checkpoint
    /// after a restart, for instance. When you simply need to walk everything, use
    /// <see cref="SearchAsync"/>.
    /// </remarks>
    Task<EventPage> GetPageAsync(
        EventQuery? query = null,
        string? cursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>Walks every matching event, fetching pages as needed.</summary>
    /// <param name="query">The search. All events of the last 24 hours when omitted.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The matching events.</returns>
    /// <exception cref="SparkPostApiException">SparkPost answered with an error status.</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    /// <remarks>
    /// Lazy: the first page is not fetched until enumeration begins, and each subsequent page
    /// is fetched only when the previous one runs out. Both exceptions therefore surface while
    /// enumerating rather than at the call itself, and they can surface on any page — put the
    /// <c>await foreach</c> inside the <c>try</c>, not just this call.
    /// </remarks>
    IAsyncEnumerable<SparkPostEvent> SearchAsync(
        EventQuery? query = null,
        CancellationToken cancellationToken = default);
}
