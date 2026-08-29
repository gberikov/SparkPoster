namespace SparkPoster;

/// <summary>Sending mail.</summary>
public interface ITransmissions
{
    /// <summary>Sends a transmission.</summary>
    /// <param name="transmission">The request, usually assembled through <see cref="Transmission.Create"/>.</param>
    /// <param name="idempotencyKey">
    /// The idempotency key. When omitted, one is generated automatically — enough to keep a
    /// transport-level retry (from a resilience handler, say) from sending the mail twice.
    /// Pass it explicitly when your own code retries the call: then derive the key from a
    /// business identifier such as an order number. SparkPost remembers a key for 24 hours.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The result of the send.</returns>
    /// <exception cref="SparkPostApiException">SparkPost answered with an error status.</exception>
    /// <exception cref="SparkPostRateLimitException">The request (429) or sending (420) limit was exceeded.</exception>
    Task<TransmissionResponse> SendAsync(
        TransmissionRequest transmission,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>Cancels the scheduled transmissions of a campaign.</summary>
    /// <param name="campaignId">The campaign identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the request is accepted.</returns>
    /// <remarks>
    /// SparkPost answers immediately and deletes in the background: every cancelled message
    /// produces a <c>bounce</c> event with the reason "554 5.7.1 [internal] Campaign cancelled".
    /// To cancel a subaccount's messages the request must be made as that subaccount —
    /// through <see cref="ISparkPostClient.ForSubaccount"/> or with a subaccount API key.
    /// </remarks>
    Task DeleteByCampaignAsync(string campaignId, CancellationToken cancellationToken = default);
}
