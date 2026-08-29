using System.Text.Json.Nodes;

namespace SparkPoster;

/// <summary>
/// A request to send mail. Usually assembled through <see cref="Transmission.Create"/>,
/// though filling it in by hand works too — and since the object is serializable, it can
/// be stored or queued and sent later.
/// </summary>
public sealed record TransmissionRequest
{
    /// <summary>The message content.</summary>
    public required TransmissionContent Content { get; init; }

    /// <summary>The recipients: listed explicitly or referenced as a stored list.</summary>
    public required RecipientSet Recipients { get; init; }

    /// <summary>
    /// Transmission-level substitution data. Recipient data takes precedence over this.
    /// SparkPost caps it at 100 KB.
    /// </summary>
    public JsonNode? SubstitutionData { get; init; }

    /// <summary>
    /// Transmission-level metadata: available in webhook events and in the template language.
    /// SparkPost caps it at 10 KB.
    /// </summary>
    public JsonNode? Metadata { get; init; }

    /// <summary>Sending options.</summary>
    public TransmissionOptions? Options { get; init; }

    /// <summary>
    /// Overrides for fields of a stored template. Substitutions are not supported here.
    /// </summary>
    public ContentOverride? Override { get; init; }

    /// <summary>The campaign identifier. At most 64 bytes.</summary>
    public string? CampaignId { get; init; }

    /// <summary>A description of the transmission. At most 1024 bytes.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The envelope FROM address that bounces are returned to. Its domain must be a
    /// CNAME-verified sending domain.
    /// </summary>
    public string? ReturnPath { get; init; }

    /// <summary>
    /// The tracking domain used to wrap engagement-tracked links. It must be verified,
    /// otherwise the request is rejected with a 400.
    /// </summary>
    public string? TrackingDomain { get; init; }
}

/// <summary>
/// The message content: given inline, taken from a stored template, from an A/B test, or
/// supplied as raw RFC822. The four forms are mutually exclusive.
/// </summary>
public sealed record TransmissionContent
{
    /// <summary>The sender. Its domain must be a verified sending domain.</summary>
    public Address? From { get; init; }

    /// <summary>The subject line. Supports the template language.</summary>
    public string? Subject { get; init; }

    /// <summary>The HTML part of the message.</summary>
    public string? Html { get; init; }

    /// <summary>The plain text part of the message.</summary>
    public string? Text { get; init; }

    /// <summary>The AMP part of the message. Requires html or text to be set as well.</summary>
    public string? AmpHtml { get; init; }

    /// <summary>The reply-to address.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>
    /// Additional message headers. <c>Subject</c>, <c>From</c>, <c>To</c>, <c>Reply-To</c>,
    /// <c>Content-Type</c> and <c>Content-Transfer-Encoding</c> must not be set here —
    /// they are generated for you.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Attachments. The whole message content is capped at 20 MB.</summary>
    public IReadOnlyList<Attachment>? Attachments { get; init; }

    /// <summary>
    /// Inline images, referenced from the HTML through <c>cid:</c> plus the image name.
    /// </summary>
    public IReadOnlyList<Attachment>? InlineImages { get; init; }

    /// <summary>The identifier of a stored template.</summary>
    public string? TemplateId { get; init; }

    /// <summary>Use the template draft instead of the published version.</summary>
    public bool? UseDraftTemplate { get; init; }

    /// <summary>The A/B test identifier. A/B tests only support single-recipient transmissions.</summary>
    public string? AbTestId { get; init; }

    /// <summary>A ready-made message in RFC822 format.</summary>
    public string? EmailRfc822 { get; init; }
}

/// <summary>Overrides for fields of a stored template.</summary>
public sealed record ContentOverride
{
    /// <summary>The sender.</summary>
    public Address? From { get; init; }

    /// <summary>The reply-to address.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>The value of the <c>List-Unsubscribe</c> header.</summary>
    public string? ListId { get; init; }
}

/// <summary>An email address.</summary>
public sealed record Address
{
    /// <summary>The email address.</summary>
    public required string Email { get; init; }

    /// <summary>The display name.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// The address the recipient sees in the <c>To</c> header. This is how copies work:
    /// CC and BCC recipients carry the primary recipient's address here.
    /// </summary>
    public string? HeaderTo { get; init; }
}

/// <summary>A message recipient.</summary>
public sealed record Recipient
{
    /// <summary>The recipient address.</summary>
    public required Address Address { get; init; }

    /// <summary>Substitution data for this recipient. Takes precedence over transmission-level data.</summary>
    public JsonNode? SubstitutionData { get; init; }

    /// <summary>Metadata for this recipient. Takes precedence over transmission-level metadata.</summary>
    public JsonNode? Metadata { get; init; }

    /// <summary>Tags applied to this recipient.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Per-recipient tracking overrides. Ignored when a stored recipient list is used.
    /// </summary>
    public RecipientOptions? Options { get; init; }

    /// <summary>A per-recipient return path (VERP).</summary>
    public string? ReturnPath { get; init; }
}

/// <summary>Per-recipient tracking overrides.</summary>
public sealed record RecipientOptions
{
    /// <summary>Track opens.</summary>
    public bool? OpenTracking { get; init; }

    /// <summary>Track link clicks.</summary>
    public bool? ClickTracking { get; init; }

    /// <summary>Use the initial open pixel.</summary>
    public bool? InitialOpen { get; init; }
}

/// <summary>Sending options.</summary>
public sealed record TransmissionOptions
{
    /// <summary>Track opens.</summary>
    public bool? OpenTracking { get; init; }

    /// <summary>Track link clicks.</summary>
    public bool? ClickTracking { get; init; }

    /// <summary>
    /// Marks the message as transactional. This affects the suppression check: transactional
    /// mail is not blocked by unsubscribes from bulk mailings.
    /// </summary>
    public bool? Transactional { get; init; }

    /// <summary>
    /// Send through the <c>sparkpostbox.com</c> sandbox domain. Limited to 5 messages for
    /// the lifetime of the account, and only usable with the <c>my-first-email</c> template.
    /// </summary>
    public bool? Sandbox { get; init; }

    /// <summary>Skip the suppression list check. Requires explicit permission from SparkPost.</summary>
    public bool? SkipSuppression { get; init; }

    /// <summary>The IP pool to send through.</summary>
    public string? IpPool { get; init; }

    /// <summary>Inline the CSS into the HTML before sending.</summary>
    public bool? InlineCss { get; init; }

    /// <summary>Perform substitutions in the content.</summary>
    public bool? PerformSubstitutions { get; init; }

    /// <summary>The identifier of the DKIM key used for signing.</summary>
    public string? DkimKey { get; init; }

    /// <summary>
    /// When to send a scheduled message. SparkPost rejects times more than three days ahead.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }
}
