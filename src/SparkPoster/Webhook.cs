using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SparkPoster.Internal;

namespace SparkPoster;

/// <summary>How SparkPost authenticates itself when calling your endpoint.</summary>
[JsonConverter(typeof(WebhookAuthTypeJsonConverter))]
public enum WebhookAuthType
{
    /// <summary>A value this enum does not know: SparkPost added a new scheme.</summary>
    Unknown = 0,

    /// <summary>No authentication. Only sane together with a secret in <see cref="WebhookRequest.CustomHeaders"/>.</summary>
    None,

    /// <summary>HTTP Basic.</summary>
    Basic,

    /// <summary>OAuth 2.0 — the token is requested using <see cref="WebhookRequest.AuthRequestDetails"/>.</summary>
    OAuth2,
}

/// <summary>Creating or updating a webhook.</summary>
public sealed record WebhookRequest
{
    /// <summary>The webhook name.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The URL that event batches are POSTed to. Only standard ports are allowed:
    /// 80 for http and 443 for https.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// The event types to subscribe to. Known values live in <see cref="SparkPostEventTypes"/>,
    /// and the current list is available through <see cref="IWebhooks.GetEventsDocumentationAsync"/>.
    /// </summary>
    public required IReadOnlyList<string> Events { get; init; }

    /// <summary>Whether the webhook is active. An inactive one receives no batches.</summary>
    public bool? Active { get; init; }

    /// <summary>
    /// Extra headers sent with every request to your endpoint. This is also where the secret
    /// that lets you tell a genuine call from a forged one usually lives.
    /// </summary>
    public IReadOnlyDictionary<string, string>? CustomHeaders { get; init; }

    /// <summary>Subaccounts whose events are excluded from this webhook. At most 10.</summary>
    public IReadOnlyList<int>? ExceptionSubaccounts { get; init; }

    /// <summary>The authentication scheme.</summary>
    public WebhookAuthType? AuthType { get; init; }

    /// <summary>Token request details. Required when <see cref="WebhookAuthType.OAuth2"/> is used.</summary>
    public WebhookAuthRequestDetails? AuthRequestDetails { get; init; }

    /// <summary>Credentials. Required when <see cref="WebhookAuthType.Basic"/> is used.</summary>
    public WebhookAuthCredentials? AuthCredentials { get; init; }
}

/// <summary>A webhook.</summary>
public sealed record Webhook
{
    /// <summary>The identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The webhook name.</summary>
    public string? Name { get; init; }

    /// <summary>The URL that event batches are POSTed to.</summary>
    public string? Target { get; init; }

    /// <summary>The subscribed event types.</summary>
    public IReadOnlyList<string>? Events { get; init; }

    /// <summary>Whether the webhook is active.</summary>
    public bool? Active { get; init; }

    /// <summary>Extra headers sent with every request.</summary>
    public IReadOnlyDictionary<string, string>? CustomHeaders { get; init; }

    /// <summary>Subaccounts whose events are excluded.</summary>
    public IReadOnlyList<int>? ExceptionSubaccounts { get; init; }

    /// <summary>The authentication scheme.</summary>
    public WebhookAuthType? AuthType { get; init; }

    /// <summary>Token request details.</summary>
    public WebhookAuthRequestDetails? AuthRequestDetails { get; init; }

    /// <summary>Credentials.</summary>
    public WebhookAuthCredentials? AuthCredentials { get; init; }

    /// <summary>When a batch was last delivered successfully.</summary>
    public string? LastSuccessful { get; init; }

    /// <summary>When a batch delivery last failed.</summary>
    public string? LastFailure { get; init; }
}

/// <summary>Details of the OAuth token request.</summary>
public sealed record WebhookAuthRequestDetails
{
    /// <summary>The URL the token is requested from.</summary>
    public string? Url { get; init; }

    /// <summary>The token request body: <c>client_id</c>, <c>client_secret</c>, <c>grant_type</c>.</summary>
    public JsonNode? Body { get; init; }

    /// <summary>Extra headers for the token request.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

/// <summary>Credentials used to authenticate against your endpoint.</summary>
public sealed record WebhookAuthCredentials
{
    /// <summary>The user name for Basic authentication.</summary>
    public string? Username { get; init; }

    /// <summary>The password for Basic authentication.</summary>
    public string? Password { get; init; }

    /// <summary>The OAuth token that was obtained.</summary>
    public string? AccessToken { get; init; }

    /// <summary>The lifetime of the OAuth token, in seconds.</summary>
    public int? ExpiresIn { get; init; }
}

/// <summary>The result of validating a webhook with a test batch.</summary>
public sealed record WebhookValidationResult
{
    /// <summary>A message describing the outcome.</summary>
    public string? Msg { get; init; }

    /// <summary>What your endpoint answered.</summary>
    public WebhookTargetResponse? Response { get; init; }
}

/// <summary>What your endpoint answered to the test batch.</summary>
public sealed record WebhookTargetResponse
{
    /// <summary>The HTTP status code.</summary>
    public int? Status { get; init; }

    /// <summary>The response headers.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>The response body.</summary>
    public string? Body { get; init; }
}

/// <summary>
/// The delivery status of one event batch. Kept for 24 hours.
/// </summary>
public sealed record WebhookBatchStatus
{
    /// <summary>
    /// The batch identifier, the same one that arrives in the
    /// <c>X-MessageSystems-Batch-ID</c> header.
    /// </summary>
    public string? BatchId { get; init; }

    /// <summary>The webhook identifier.</summary>
    public string? WebhookId { get; init; }

    /// <summary>When the batch was created.</summary>
    public DateTimeOffset? Ts { get; init; }

    /// <summary>How many events the batch held.</summary>
    public int? BatchSize { get; init; }

    /// <summary>How many attempts failed before delivery. Zero when the first attempt succeeded.</summary>
    public int? Attempts { get; init; }

    /// <summary>The status code your endpoint answered with.</summary>
    public int? ResponseCode { get; init; }

    /// <summary>The failure code, when delivery did not succeed.</summary>
    public int? FailureCode { get; init; }

    /// <summary>The duration of the whole round trip, in milliseconds.</summary>
    public int? Latency { get; init; }
}
