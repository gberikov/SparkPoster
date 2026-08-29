namespace SparkPoster;

/// <summary>
/// A sending domain: the domain your mail is sent from.
/// </summary>
/// <remarks>
/// Sending only works once the domain is verified, which means publishing the DNS records
/// SparkPost hands back and then calling
/// <see cref="ISendingDomains.VerifyAsync"/>.
/// </remarks>
public sealed record SendingDomain
{
    /// <summary>The domain name.</summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>The verification state of the domain.</summary>
    public SendingDomainStatus? Status { get; init; }

    /// <summary>The DKIM settings.</summary>
    public DkimSettings? Dkim { get; init; }

    /// <summary>The tracking domain used to wrap links in mail from this domain.</summary>
    public string? TrackingDomain { get; init; }

    /// <summary>Whether the domain is the default bounce domain.</summary>
    public bool? IsDefaultBounceDomain { get; init; }

    /// <summary>Whether subaccounts may send from this domain.</summary>
    public bool? SharedWithSubaccounts { get; init; }

    /// <summary>The subaccount the domain belongs to.</summary>
    public int? SubaccountId { get; init; }
}

/// <summary>The verification state of a sending domain.</summary>
public sealed record SendingDomainStatus
{
    /// <summary>Whether ownership of the domain has been proven.</summary>
    public bool? OwnershipVerified { get; init; }

    /// <summary>The state of the DKIM record.</summary>
    public string? DkimStatus { get; init; }

    /// <summary>The state of the SPF record.</summary>
    public string? SpfStatus { get; init; }

    /// <summary>The state of the CNAME record for the bounce domain.</summary>
    public string? CnameStatus { get; init; }

    /// <summary>The state of the MX record.</summary>
    public string? MxStatus { get; init; }

    /// <summary>The state of verification through the abuse@ mailbox.</summary>
    public string? AbuseAtStatus { get; init; }

    /// <summary>The state of verification through the postmaster@ mailbox.</summary>
    public string? PostmasterAtStatus { get; init; }

    /// <summary>The state of verification through an arbitrary mailbox.</summary>
    public string? VerificationMailboxStatus { get; init; }

    /// <summary>The compliance state of the domain.</summary>
    public string? ComplianceStatus { get; init; }
}

/// <summary>DKIM settings for a sending domain.</summary>
public sealed record DkimSettings
{
    /// <summary>The DKIM selector, which becomes part of the DNS record name.</summary>
    public string? Selector { get; init; }

    /// <summary>The public key to publish in DNS.</summary>
    public string? Public { get; init; }

    /// <summary>
    /// The private key. SparkPost accepts it when you bring your own key pair and never
    /// returns it afterwards.
    /// </summary>
    public string? Private { get; init; }

    /// <summary>The headers covered by the signature.</summary>
    public string? Headers { get; init; }

    /// <summary>The signing domain, when it differs from the sending domain.</summary>
    public string? SigningDomain { get; init; }
}

/// <summary>Creating or updating a sending domain.</summary>
public sealed record SendingDomainRequest
{
    /// <summary>The domain name. Required when creating.</summary>
    public string? Domain { get; init; }

    /// <summary>The tracking domain used to wrap links in mail from this domain.</summary>
    public string? TrackingDomain { get; init; }

    /// <summary>Whether the domain should be the default bounce domain.</summary>
    public bool? IsDefaultBounceDomain { get; init; }

    /// <summary>Whether subaccounts may send from this domain.</summary>
    public bool? SharedWithSubaccounts { get; init; }

    /// <summary>Let SparkPost generate the DKIM key pair. On by default when creating.</summary>
    public bool? GenerateDkim { get; init; }

    /// <summary>The length of the generated DKIM key.</summary>
    public int? DkimKeyLength { get; init; }

    /// <summary>Your own DKIM key pair, as an alternative to generating one.</summary>
    public DkimSettings? Dkim { get; init; }
}

/// <summary>Which checks to run when verifying a domain.</summary>
/// <remarks>
/// The DNS checks are free to run as often as you like. The mailbox checks send a message
/// with a token to the given address and then expect that token back, so they take two steps.
/// </remarks>
public sealed record DomainVerificationOptions
{
    /// <summary>Check the DKIM record.</summary>
    public bool? DkimVerify { get; init; }

    /// <summary>Check the SPF record.</summary>
    public bool? SpfVerify { get; init; }

    /// <summary>Check the CNAME record for the bounce domain.</summary>
    public bool? CnameVerify { get; init; }

    /// <summary>Check the MX record.</summary>
    public bool? MxVerify { get; init; }

    /// <summary>Send a verification message to abuse@ at this domain.</summary>
    public bool? AbuseAtVerify { get; init; }

    /// <summary>Send a verification message to postmaster@ at this domain.</summary>
    public bool? PostmasterAtVerify { get; init; }

    /// <summary>Send a verification message to <see cref="VerificationMailbox"/>.</summary>
    public bool? VerificationMailboxVerify { get; init; }

    /// <summary>The local part of the mailbox to send the verification message to.</summary>
    public string? VerificationMailbox { get; init; }

    /// <summary>The token received at abuse@, sent back to complete verification.</summary>
    public string? AbuseAtToken { get; init; }

    /// <summary>The token received at postmaster@, sent back to complete verification.</summary>
    public string? PostmasterAtToken { get; init; }

    /// <summary>The token received at the verification mailbox, sent back to complete verification.</summary>
    public string? VerificationMailboxToken { get; init; }
}
