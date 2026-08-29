namespace SparkPoster;

/// <summary>Managing sending domains.</summary>
public interface ISendingDomains
{
    /// <summary>Registers a sending domain.</summary>
    /// <param name="domain">The domain definition.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>
    /// The registered domain, including the generated DKIM key that has to be published in DNS.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="domain"/> is <c>null</c>.</exception>
    /// <exception cref="SparkPostApiException">
    /// SparkPost answered with an error status: the domain is already registered, on this
    /// account or another one (400).
    /// </exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task<SendingDomain> CreateAsync(SendingDomainRequest domain, CancellationToken cancellationToken = default);

    /// <summary>Returns a sending domain.</summary>
    /// <param name="domain">The domain name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The domain together with its verification state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="domain"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="domain"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">No such domain is registered (404).</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task<SendingDomain> GetAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>Returns every sending domain.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The domains.</returns>
    /// <exception cref="SparkPostApiException">SparkPost answered with an error status.</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task<IReadOnlyList<SendingDomain>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates a sending domain.</summary>
    /// <param name="domain">The domain name.</param>
    /// <param name="changes">The new values.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the domain is updated.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="domain"/> or <paramref name="changes"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="domain"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">No such domain is registered (404).</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task UpdateAsync(string domain, SendingDomainRequest changes, CancellationToken cancellationToken = default);

    /// <summary>Removes a sending domain.</summary>
    /// <param name="domain">The domain name.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the domain is removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="domain"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="domain"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">No such domain is registered (404).</exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    Task DeleteAsync(string domain, CancellationToken cancellationToken = default);

    /// <summary>Runs the requested verification checks.</summary>
    /// <param name="domain">The domain name.</param>
    /// <param name="options">Which checks to run. DKIM and SPF when omitted.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The verification state after the checks.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="domain"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="domain"/> is empty or whitespace.</exception>
    /// <exception cref="SparkPostApiException">
    /// No such domain is registered (404), or the requested check cannot be run (400).
    /// A failed check is not an error: it comes back in the returned status.
    /// </exception>
    /// <exception cref="SparkPostRateLimitException">The request limit was exceeded (429).</exception>
    /// <remarks>
    /// The DNS checks simply look the records up. The mailbox checks are a two-step affair:
    /// the first call sends a message carrying a token, and a second call passes that token
    /// back through the matching property of <see cref="DomainVerificationOptions"/>.
    /// </remarks>
    Task<SendingDomainStatus> VerifyAsync(
        string domain,
        DomainVerificationOptions? options = null,
        CancellationToken cancellationToken = default);
}
