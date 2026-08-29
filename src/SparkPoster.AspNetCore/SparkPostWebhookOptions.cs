namespace SparkPoster.AspNetCore;

/// <summary>
/// How an incoming webhook call is proven genuine.
/// </summary>
/// <remarks>
/// SparkPost webhooks carry no signature, so authenticity rests entirely on what you
/// configured when creating the webhook. Set either Basic authentication or a secret header,
/// and mirror the same values in the <see cref="WebhookRequest"/>.
/// </remarks>
public sealed class SparkPostWebhookOptions
{
    /// <summary>The user name for Basic authentication.</summary>
    public string? BasicAuthUsername { get; set; }

    /// <summary>The password for Basic authentication.</summary>
    public string? BasicAuthPassword { get; set; }

    /// <summary>
    /// The name of the secret header, for example <c>X-Webhook-Secret</c>.
    /// It must match a key in <see cref="WebhookRequest.CustomHeaders"/>.
    /// </summary>
    public string? SecretHeaderName { get; set; }

    /// <summary>The expected value of the secret header.</summary>
    public string? SecretHeaderValue { get; set; }

    /// <summary>Whether any check at all is configured.</summary>
    internal bool HasAnyCheck =>
        BasicAuthUsername is not null || SecretHeaderName is not null;
}
