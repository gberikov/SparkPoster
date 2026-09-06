using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SparkPoster.Internal;

namespace SparkPoster.Webhooks;

/// <summary>
/// An event from a webhook batch or from the Events API.
/// </summary>
/// <remarks>
/// <para>
/// The fields present in the official samples are typed; everything else, including fields
/// SparkPost added after this version of the library shipped, lives in <see cref="Extra"/>.
/// An unfamiliar event type never raises an exception — it becomes an
/// <see cref="UnknownSparkPostEvent"/>.
/// </para>
/// <para>
/// Fields that SparkPost sends for more than one category — tracking flags, envelope sender,
/// injection time, A/B test identifiers — sit on this base type so that a handler can read
/// them without knowing the concrete category. A field that is absent from the payload is
/// <c>null</c>.
/// </para>
/// </remarks>
public abstract record SparkPostEvent
{
    /// <summary>The event type: <c>delivery</c>, <c>bounce</c>, <c>open</c> and so on.</summary>
    /// <remarks>Known values are listed in <see cref="SparkPostEventTypes"/>.</remarks>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? Type { get; init; }

    /// <summary>The unique event identifier. Useful for guarding against duplicate processing.</summary>
    public string? EventId { get; init; }

    /// <summary>When the event happened.</summary>
    [JsonConverter(typeof(UnixTimestampJsonConverter))]
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>The SparkPost customer the message belongs to.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? CustomerId { get; init; }

    /// <summary>The subaccount the message was sent on behalf of.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? SubaccountId { get; init; }

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

    /// <summary>A hash of the recipient address.</summary>
    public string? RcptHash { get; init; }

    /// <summary>The recipient kind: <c>cc</c>, <c>bcc</c>, or empty for the primary recipient.</summary>
    public string? RcptType { get; init; }

    /// <summary>Recipient metadata supplied at send time.</summary>
    public JsonNode? RcptMeta { get; init; }

    /// <summary>Tags applied to the recipient.</summary>
    public IReadOnlyList<string>? RcptTags { get; init; }

    /// <summary>The template the message was built from.</summary>
    public string? TemplateId { get; init; }

    /// <summary>The template version.</summary>
    public string? TemplateVersion { get; init; }

    /// <summary>The A/B test the message was part of.</summary>
    public string? AbTestId { get; init; }

    /// <summary>The A/B test version.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? AbTestVersion { get; init; }

    /// <summary>The From header of the original message.</summary>
    public string? FriendlyFrom { get; init; }

    /// <summary>The sender on the SMTP envelope.</summary>
    public string? MsgFrom { get; init; }

    /// <summary>The domain part of the From header.</summary>
    public string? SendingDomain { get; init; }

    /// <summary>The subject line.</summary>
    public string? Subject { get; init; }

    /// <summary>The message size in bytes.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? MsgSize { get; init; }

    /// <summary>The IP pool the message was sent through.</summary>
    public string? IpPool { get; init; }

    /// <summary>The IP the message was sent from.</summary>
    public string? SendingIp { get; init; }

    /// <summary>
    /// For delivery events, the IP of the host the message was delivered to; for engagement
    /// events, the IP the HTTP request came from.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>The domain receiving the message.</summary>
    public string? RoutingDomain { get; init; }

    /// <summary>The recipient domain.</summary>
    public string? RecipientDomain { get; init; }

    /// <summary>The recipient's mailbox provider.</summary>
    public string? MailboxProvider { get; init; }

    /// <summary>The region of the mailbox provider.</summary>
    public string? MailboxProviderRegion { get; init; }

    /// <summary>How many delivery attempts failed before the message got through.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? NumRetries { get; init; }

    /// <summary>The bounce classification code of a failure.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? BounceClass { get; init; }

    /// <summary>The error code of a failure: the receiving server's, or SparkPost's own.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? ErrorCode { get; init; }

    /// <summary>The canonicalized reason for a failure.</summary>
    public string? Reason { get; init; }

    /// <summary>The verbatim reason for a failure, as the receiving server put it.</summary>
    public string? RawReason { get; init; }

    /// <summary>The address of the remote host the message was received from or delivered to.</summary>
    public string? RemoteAddr { get; init; }

    /// <summary>The TLS status of the outbound connection, for example <c>1</c> when TLS was used.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? OutboundTls { get; init; }

    /// <summary>The protocol SparkPost received the message over.</summary>
    public string? RecvMethod { get; init; }

    /// <summary>The protocol SparkPost delivered the message over.</summary>
    public string? DelvMethod { get; init; }

    /// <summary>When the message was injected into SparkPost.</summary>
    [JsonConverter(typeof(UnixTimestampJsonConverter))]
    public DateTimeOffset? InjectionTime { get; init; }

    /// <summary>When the message was scheduled to be sent.</summary>
    [JsonConverter(typeof(UnixTimestampJsonConverter))]
    public DateTimeOffset? ScheduledTime { get; init; }

    /// <summary>How long the message waited in the queue, in seconds.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? QueueTime { get; init; }

    /// <summary>Whether the transmission was marked transactional. Arrives as <c>"1"</c>.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? Transactional { get; init; }

    /// <summary>Whether open tracking was enabled for the message.</summary>
    public bool? OpenTracking { get; init; }

    /// <summary>Whether click tracking was enabled for the message.</summary>
    public bool? ClickTracking { get; init; }

    /// <summary>
    /// Whether initial open pixel tracking was enabled for the message. This is a setting,
    /// not the source of a particular open: the type <c>initial_open</c> says that.
    /// </summary>
    public bool? InitialPixel { get; init; }

    /// <summary>Whether the message carried an AMP part.</summary>
    public bool? AmpEnabled { get; init; }

    /// <summary>
    /// Fields that are not among the typed ones — including those that appeared in the API
    /// after this version of the library shipped. Nothing is lost.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>
/// A message lifecycle event: injection, delivery, bounce, delay, complaint, SMS status.
/// The <c>message_event</c> category.
/// </summary>
/// <remarks>
/// The SMS-specific fields of an <c>sms_status</c> event (<c>sms_dst</c>, <c>stat_state</c>
/// and the like) are not typed and stay in <see cref="SparkPostEvent.Extra"/>.
/// </remarks>
public sealed record MessageEvent : SparkPostEvent
{
    /// <summary>The device token of a push notification. Only for <c>gcm</c> and <c>apn</c> delivery.</summary>
    public string? DeviceToken { get; init; }

    /// <summary>For a spam complaint, the feedback report type, for example <c>abuse</c>.</summary>
    public string? Fbtype { get; init; }

    /// <summary>For a spam complaint, who reported it.</summary>
    public string? ReportBy { get; init; }

    /// <summary>For a spam complaint, the address the report was sent to.</summary>
    public string? ReportTo { get; init; }
}

/// <summary>
/// An engagement event: an open, a click, or their AMP counterparts.
/// The <c>track_event</c> category.
/// </summary>
public sealed record TrackEvent : SparkPostEvent
{
    /// <summary>The User-Agent the request came from.</summary>
    public string? UserAgent { get; init; }

    /// <summary>The User-Agent broken down by SparkPost.</summary>
    public UserAgentInfo? UserAgentParsed { get; init; }

    /// <summary>The URL of the link that was clicked.</summary>
    public string? TargetLinkUrl { get; init; }

    /// <summary>The name of the link that was clicked.</summary>
    public string? TargetLinkName { get; init; }

    /// <summary>Geolocation derived from the IP.</summary>
    public GeoLocation? GeoIp { get; init; }
}

/// <summary>Geolocation of the host an engagement request came from.</summary>
public sealed record GeoLocation
{
    /// <summary>The country code.</summary>
    public string? Country { get; init; }

    /// <summary>The region or state.</summary>
    public string? Region { get; init; }

    /// <summary>The city.</summary>
    public string? City { get; init; }

    /// <summary>The latitude. SparkPost sends it as a number or as a string.</summary>
    public double? Latitude { get; init; }

    /// <summary>The longitude. SparkPost sends it as a number or as a string.</summary>
    public double? Longitude { get; init; }

    /// <summary>The postal code. SparkPost sends it as a number or as a string.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? Zip { get; init; }

    /// <summary>The postal code.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? PostalCode { get; init; }
}

/// <summary>A User-Agent string broken down by SparkPost.</summary>
public sealed record UserAgentInfo
{
    /// <summary>The browser engine family.</summary>
    public string? AgentFamily { get; init; }

    /// <summary>The device brand.</summary>
    public string? DeviceBrand { get; init; }

    /// <summary>The device family.</summary>
    public string? DeviceFamily { get; init; }

    /// <summary>The operating system family.</summary>
    public string? OsFamily { get; init; }

    /// <summary>The operating system version.</summary>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? OsVersion { get; init; }

    /// <summary>Whether the device is mobile.</summary>
    public bool? IsMobile { get; init; }

    /// <summary>Whether the request came through a proxy, such as an image cache.</summary>
    public bool? IsProxy { get; init; }

    /// <summary>Whether the request was a prefetch rather than a person opening the message.</summary>
    public bool? IsPrefetched { get; init; }
}

/// <summary>
/// A generation event: the message could not be built, or building it was rejected.
/// The <c>gen_event</c> category.
/// </summary>
public sealed record GenerationEvent : SparkPostEvent
{
    /// <summary>The recipient's substitution data.</summary>
    public JsonNode? RcptSubs { get; init; }
}

/// <summary>
/// An unsubscribe, either through the List-Unsubscribe header or through a link in the
/// message. The <c>unsubscribe_event</c> category.
/// </summary>
public sealed record UnsubscribeEvent : SparkPostEvent
{
    /// <summary>The envelope sender of the original message. Arrives as <c>mailfrom</c>.</summary>
    [JsonPropertyName("mailfrom")]
    public string? MailFrom { get; init; }

    /// <summary>For a link unsubscribe, the URL of the link that was clicked.</summary>
    public string? TargetLinkUrl { get; init; }

    /// <summary>For a link unsubscribe, the name of the link that was clicked.</summary>
    public string? TargetLinkName { get; init; }

    /// <summary>The User-Agent the request came from.</summary>
    public string? UserAgent { get; init; }
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

    /// <summary>Where the message came from, for example <c>inbound</c>.</summary>
    public string? Origination { get; init; }

    /// <summary>
    /// The content of the inbound message, when SparkPost includes it. The official event
    /// samples carry no content — the message itself is delivered through the relay webhook,
    /// not through the event webhook — so the shape is left untyped.
    /// </summary>
    public JsonNode? Content { get; init; }
}

/// <summary>
/// An A/B test finished or was cancelled. The <c>ab_test_event</c> category.
/// </summary>
public sealed record AbTestEvent : SparkPostEvent
{
    /// <summary>The test and its results.</summary>
    public AbTestSummary? AbTest { get; init; }
}

/// <summary>An A/B test as reported in an <see cref="AbTestEvent"/>.</summary>
public sealed record AbTestSummary
{
    /// <summary>The test identifier.</summary>
    public string? Id { get; init; }

    /// <summary>The test name.</summary>
    public string? Name { get; init; }

    /// <summary>The test version.</summary>
    public int? Version { get; init; }

    /// <summary>How the winner is chosen: <c>bayesian</c> or <c>learning</c>.</summary>
    public string? TestMode { get; init; }

    /// <summary>The metric the templates were compared on.</summary>
    public string? EngagementMetric { get; init; }

    /// <summary>The default template and its numbers.</summary>
    public AbTestTemplateResult? DefaultTemplate { get; init; }

    /// <summary>The variants and their numbers.</summary>
    public IReadOnlyList<AbTestTemplateResult>? Variants { get; init; }

    /// <summary>The template that won. Absent when the test was cancelled.</summary>
    public string? WinningTemplateId { get; init; }
}

/// <summary>The numbers of one template in an A/B test.</summary>
public sealed record AbTestTemplateResult
{
    /// <summary>The template identifier.</summary>
    public string? TemplateId { get; init; }

    /// <summary>How many recipients clicked at least once.</summary>
    public int? CountUniqueClicked { get; init; }

    /// <summary>How many recipients opened at least once.</summary>
    public int? CountUniqueConfirmedOpened { get; init; }

    /// <summary>How many messages were accepted.</summary>
    public int? CountAccepted { get; init; }

    /// <summary>The engagement rate the test measured.</summary>
    public double? EngagementRate { get; init; }
}

/// <summary>
/// The outcome of an ingest batch: <c>success</c> or <c>error</c>.
/// The <c>ingest_event</c> category, only for accounts with the Ingest API.
/// </summary>
public sealed record IngestEvent : SparkPostEvent
{
    /// <summary>The ingest batch identifier.</summary>
    public string? BatchId { get; init; }

    /// <summary>When the batch is discarded.</summary>
    [JsonConverter(typeof(UnixTimestampJsonConverter))]
    public DateTimeOffset? ExpirationTimestamp { get; init; }

    /// <summary>How many events in the batch were accepted.</summary>
    public int? NumberSucceeded { get; init; }

    /// <summary>How many events in the batch were duplicates.</summary>
    public int? NumberDuplicates { get; init; }

    /// <summary>How many events in the batch were rejected.</summary>
    public int? NumberFailed { get; init; }

    /// <summary>The kind of error, for example <c>validation</c>.</summary>
    public string? ErrorType { get; init; }

    /// <summary>Whether resending the batch may help.</summary>
    public bool? Retryable { get; init; }

    /// <summary>Where the rejected events can be fetched from.</summary>
    public string? Href { get; init; }
}

/// <summary>
/// An event this library could not map to a typed model.
/// </summary>
/// <remarks>
/// <para>
/// It exists so that a new SparkPost event category cannot take down your handler:
/// a handler that throws makes SparkPost resend the entire batch, including the events
/// it had already processed.
/// </para>
/// <para>
/// Two situations end up here, told apart by <see cref="ParseError"/>: an event of a category
/// or type this library does not know (<see cref="ParseError"/> is <c>null</c>), and an event of
/// a known category whose body did not fit the typed model (<see cref="ParseError"/> says why).
/// In both cases the common fields — <see cref="SparkPostEvent.EventId"/>,
/// <see cref="SparkPostEvent.Timestamp"/>, <see cref="SparkPostEvent.MessageId"/> and so on — are
/// filled in wherever the payload allowed it, so deduplication keeps working; anything that
/// could not be read is still in <see cref="Raw"/>.
/// </para>
/// </remarks>
public sealed record UnknownSparkPostEvent : SparkPostEvent
{
    /// <summary>
    /// The category name exactly as it arrived inside <c>msys</c>. Empty for an event from the
    /// Events API, which carries no category.
    /// </summary>
    [JsonIgnore]
    public string Category { get; init; } = string.Empty;

    /// <summary>The whole event body as received.</summary>
    [JsonIgnore]
    public JsonNode? Raw { get; init; }

    /// <summary>
    /// Why a known event could not be read into its typed model, or <c>null</c> when the
    /// category or type is simply not one this library knows.
    /// </summary>
    [JsonIgnore]
    public string? ParseError { get; init; }
}
