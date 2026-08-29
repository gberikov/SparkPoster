using System.Text.Json.Nodes;

namespace SparkPoster;

/// <summary>Managing event webhooks.</summary>
public interface IWebhooks
{
    /// <summary>Creates a webhook.</summary>
    /// <param name="webhook">The webhook definition.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The identifier of the created webhook.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="webhook"/> is <c>null</c>.</exception>
    /// <exception cref="SparkPostApiException">
    /// The definition did not pass validation, or the target did not answer 200 to the test
    /// POST described below (400).
    /// </exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    /// <remarks>
    /// On creation SparkPost sends a test POST to <see cref="WebhookRequest.Target"/>.
    /// If the endpoint does not answer 200, the webhook is not created and the request fails
    /// with a 400. Events start arriving about a minute after creation.
    /// </remarks>
    Task<string> CreateAsync(WebhookRequest webhook, CancellationToken cancellationToken = default);

    /// <summary>Returns a webhook.</summary>
    /// <param name="id">The webhook identifier.</param>
    /// <param name="timezone">The time zone for dates in the response, for example <c>America/New_York</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The webhook.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">No webhook with this identifier exists (404).</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task<Webhook> GetAsync(string id, string? timezone = null, CancellationToken cancellationToken = default);

    /// <summary>Returns every webhook.</summary>
    /// <param name="timezone">The time zone for dates in the response.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The list of webhooks.</returns>
    /// <exception cref="SparkPostApiException">SparkPost answered with an error status.</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task<IReadOnlyList<Webhook>> ListAsync(string? timezone = null, CancellationToken cancellationToken = default);

    /// <summary>Updates a webhook.</summary>
    /// <param name="id">The webhook identifier.</param>
    /// <param name="webhook">The new values. Arrays are replaced wholesale, not merged.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the webhook is updated.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="id"/> or <paramref name="webhook"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">
    /// No webhook with this identifier exists (404), or the new values did not pass
    /// validation (400).
    /// </exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task UpdateAsync(string id, WebhookRequest webhook, CancellationToken cancellationToken = default);

    /// <summary>Deletes a webhook.</summary>
    /// <param name="id">The webhook identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the webhook is deleted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">No webhook with this identifier exists (404).</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Posts a test batch to the target and reports what it answered.</summary>
    /// <param name="id">The webhook identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">No webhook with this identifier exists (404).</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    /// <remarks>
    /// A target that answers something other than 200 is not an error here: that answer is
    /// what the returned result reports.
    /// </remarks>
    Task<WebhookValidationResult> ValidateAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Returns the delivery status of recent batches.</summary>
    /// <param name="id">The webhook identifier.</param>
    /// <param name="limit">How many records to return. SparkPost defaults to 1000.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Batch statuses for the last 24 hours.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="id"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">No webhook with this identifier exists (404).</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    /// <remarks>
    /// A batch that does not receive a 200 is retried for 8 hours and then discarded.
    /// </remarks>
    Task<IReadOnlyList<WebhookBatchStatus>> GetBatchStatusAsync(
        string id,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns the description of every event type and its fields, as raw JSON.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The event documentation.</returns>
    /// <exception cref="SparkPostApiException">SparkPost answered with an error status.</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    /// <remarks>
    /// The response is returned unprocessed: this is a reference whose shape changes along
    /// with the API, and typing it would mean going stale along with it.
    /// </remarks>
    Task<JsonNode> GetEventsDocumentationAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns sample events as raw JSON.</summary>
    /// <param name="events">The event types; all of them when omitted.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Sample events — handy as fixtures for testing your own handler.</returns>
    /// <exception cref="SparkPostApiException">One of the requested event types is unknown (400).</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task<JsonNode> GetEventSamplesAsync(
        IEnumerable<string>? events = null,
        CancellationToken cancellationToken = default);
}
