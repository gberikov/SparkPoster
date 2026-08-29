using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SparkPoster.Internal;

namespace SparkPoster.Webhooks;

/// <summary>
/// An event from a webhook batch.
/// </summary>
/// <remarks>
/// The most commonly used fields are typed; everything else, including fields SparkPost
/// added after this version of the library shipped, lives in <see cref="Extra"/>.
/// An unfamiliar event type never raises an exception — it becomes an
/// <see cref="UnknownSparkPostEvent"/>.
/// </remarks>
public abstract record SparkPostEvent
{
    /// <summary>The event type: <c>delivery</c>, <c>bounce</c>, <c>open</c> and so on.</summary>
    /// <remarks>Known values are listed in <see cref="SparkPostEventTypes"/>.</remarks>
    public string? Type { get; init; }

    /// <summary>The unique event identifier. Useful for guarding against duplicate processing.</summary>
    public string? EventId { get; init; }

    /// <summary>When the event happened.</summary>
    [JsonConverter(typeof(UnixTimestampJsonConverter))]
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>The campaign the message belonged to.</summary>
    public string? CampaignId { get; init; }

    /// <summary>The transmission that produced the message.</summary>
    public string? TransmissionId { get; init; }

    /// <summary>The message identifier within SparkPost.</summary>
    public string? MessageId { get; init; }

    /// <summary>The recipient address, lower cased.</summary>
    public string? RcptTo { get; init; }

    /// <summary>The recipient address as originally given.</summary>
    public string? RawRcptTo { get; init; }

    /// <summary>The recipient kind: <c>cc</c>, <c>bcc</c>, or empty for the primary recipient.</summary>
    public string? RcptType { get; init; }

    /// <summary>Recipient metadata supplied at send time.</summary>
    public JsonNode? RcptMeta { get; init; }

    /// <summary>Tags applied to the recipient.</summary>
    public IReadOnlyList<string>? RcptTags { get; init; }

    /// <summary>The subaccount the message was sent on behalf of.</summary>
    public string? SubaccountId { get; init; }

    /// <summary>The template the message was built from.</summary>
    public string? TemplateId { get; init; }

    /// <summary>The template version.</summary>
    public string? TemplateVersion { get; init; }

    /// <summary>The From header of the original message.</summary>
    public string? FriendlyFrom { get; init; }

    /// <summary>The subject line.</summary>
    public string? Subject { get; init; }

    /// <summary>The IP pool the message was sent through.</summary>
    public string? IpPool { get; init; }

    /// <summary>Whether the transmission was marked transactional.</summary>
    public string? Transactional { get; init; }

    /// <summary>
    /// Fields that are not among the typed ones — including those that appeared in the API
    /// after this version of the library shipped. Nothing is lost.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>
/// A message lifecycle event: injection, delivery, bounce, delay, complaint.
/// The <c>message_event</c> category.
/// </summary>
public sealed record MessageEvent : SparkPostEvent
{
    /// <summary>The bounce classification code.</summary>
    public string? BounceClass { get; init; }

    /// <summary>The error code returned by the receiving server.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>The canonicalized response of the receiving server.</summary>
    public string? Reason { get; init; }

    /// <summary>The verbatim response of the receiving server.</summary>
    public string? RawReason { get; init; }

    /// <summary>How many delivery attempts failed before this one.</summary>
    public string? NumRetries { get; init; }

    /// <summary>The IP the message was sent from.</summary>
    public string? SendingIp { get; init; }

    /// <summary>The IP of the host the message was delivered to.</summary>
    public string? IpAddress { get; init; }

    /// <summary>The recipient domain.</summary>
    public string? RecipientDomain { get; init; }

    /// <summary>The domain receiving the message.</summary>
    public string? RoutingDomain { get; init; }

    /// <summary>The message size in bytes.</summary>
    public string? MsgSize { get; init; }

    /// <summary>The delivery protocol.</summary>
    public string? DelvMethod { get; init; }

    /// <summary>The recipient's mailbox provider.</summary>
    public string? MailboxProvider { get; init; }

    /// <summary>The region of the mailbox provider.</summary>
    public string? MailboxProviderRegion { get; init; }

    /// <summary>When the message was injected into SparkPost.</summary>
    public string? InjectionTime { get; init; }
}

/// <summary>
/// An engagement event: an open, a click, or their AMP counterparts.
/// The <c>track_event</c> category.
/// </summary>
public sealed record TrackEvent : SparkPostEvent
{
    /// <summary>The User-Agent the request came from.</summary>
    public string? UserAgent { get; init; }

    /// <summary>The IP the request came from.</summary>
    public string? IpAddress { get; init; }

    /// <summary>The URL of the link that was clicked.</summary>
    public string? TargetLinkUrl { get; init; }

    /// <summary>The name of the link that was clicked.</summary>
    public string? TargetLinkName { get; init; }

    /// <summary>Geolocation derived from the IP.</summary>
    public JsonNode? GeoIp { get; init; }

    /// <summary>The open was recorded by the initial open pixel.</summary>
    public string? InitialPixel { get; init; }
}

/// <summary>
/// A generation event: the message could not be built, or building it was rejected.
/// The <c>gen_event</c> category.
/// </summary>
public sealed record GenerationEvent : SparkPostEvent
{
    /// <summary>The error code.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>The reason for the failure.</summary>
    public string? Reason { get; init; }

    /// <summary>The verbatim reason for the failure.</summary>
    public string? RawReason { get; init; }

    /// <summary>The recipient's substitution data.</summary>
    public JsonNode? RcptSubs { get; init; }
}

/// <summary>
/// An unsubscribe, either through the List-Unsubscribe header or through a link in the
/// message. The <c>unsubscribe_event</c> category.
/// </summary>
public sealed record UnsubscribeEvent : SparkPostEvent
{
    /// <summary>The address the unsubscribe request came from.</summary>
    public string? MailFrom { get; init; }

    /// <summary>The User-Agent the request came from.</summary>
    public string? UserAgent { get; init; }

    /// <summary>The IP the request came from.</summary>
    public string? IpAddress { get; init; }
}

/// <summary>
/// An inbound mail (relay) event. The <c>relay_event</c> category.
/// </summary>
public sealed record RelayEvent : SparkPostEvent
{
    /// <summary>The relay event identifier.</summary>
    public string? RelayId { get; init; }

    /// <summary>The webhook that accepted the message.</summary>
    public string? WebhookId { get; init; }

    /// <summary>The protocol the message was received over.</summary>
    public string? Protocol { get; init; }

    /// <summary>The content of the inbound message.</summary>
    public JsonNode? Content { get; init; }

    /// <summary>The sender on the SMTP envelope.</summary>
    public string? MsgFrom { get; init; }

    /// <summary>The reason for the failure.</summary>
    public string? Reason { get; init; }

    /// <summary>The error code.</summary>
    public string? ErrorCode { get; init; }
}

/// <summary>
/// An event from a category this library does not know.
/// </summary>
/// <remarks>
/// It exists so that a new SparkPost event category cannot take down your handler:
/// a handler that throws makes SparkPost resend the entire batch, including the events
/// it had already processed.
/// </remarks>
public sealed record UnknownSparkPostEvent : SparkPostEvent
{
    /// <summary>The category name exactly as it arrived inside <c>msys</c>.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>The whole event body as received.</summary>
    public JsonNode? Raw { get; init; }
}
