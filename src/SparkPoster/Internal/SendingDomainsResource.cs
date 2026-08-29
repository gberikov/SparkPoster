using System.Net.Http.Json;

namespace SparkPoster.Internal;

internal sealed class SendingDomainsResource : ISendingDomains
{
    private const string Path = "sending-domains";

    private readonly SparkPostRequester _requester;

    public SendingDomainsResource(SparkPostRequester requester) => _requester = requester;

    public async Task<SendingDomain> CreateAsync(
        SendingDomainRequest domain,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(domain);

        using var request = _requester.CreateRequest(HttpMethod.Post, Path);
        request.Content = JsonContent.Create(domain, SparkPostJsonContext.Default.SendingDomainRequest);

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.SendingDomainEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SendingDomain> GetAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        using var request = _requester.CreateRequest(HttpMethod.Get, $"{Path}/{Uri.EscapeDataString(domain)}");

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.SendingDomainEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SendingDomain>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var request = _requester.CreateRequest(HttpMethod.Get, Path);

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.SendingDomainListEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        string domain,
        SendingDomainRequest changes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        ArgumentNullException.ThrowIfNull(changes);

        using var request = _requester.CreateRequest(HttpMethod.Put, $"{Path}/{Uri.EscapeDataString(domain)}");
        request.Content = JsonContent.Create(changes, SparkPostJsonContext.Default.SendingDomainRequest);

        await _requester.SendIgnoringResultAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string domain, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        using var request = _requester.CreateRequest(HttpMethod.Delete, $"{Path}/{Uri.EscapeDataString(domain)}");

        await _requester.SendIgnoringResultAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SendingDomainStatus> VerifyAsync(
        string domain,
        DomainVerificationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);

        using var request = _requester.CreateRequest(HttpMethod.Post, $"{Path}/{Uri.EscapeDataString(domain)}/verify");

        // Checking DKIM and SPF is what almost every caller means by "verify", and both are
        // plain DNS lookups with no side effects — unlike the mailbox checks, which send mail.
        request.Content = JsonContent.Create(
            options ?? new DomainVerificationOptions { DkimVerify = true, SpfVerify = true },
            SparkPostJsonContext.Default.DomainVerificationOptions);

        return await _requester
            .SendAndReadAsync(request, SparkPostJsonContext.Default.SendingDomainStatusEnvelope, cancellationToken)
            .ConfigureAwait(false);
    }
}
