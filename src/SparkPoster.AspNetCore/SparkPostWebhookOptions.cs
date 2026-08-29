namespace SparkPoster.AspNetCore;

/// <summary>
/// How an incoming webhook call is proven genuine.
/// </summary>
/// <remarks>
/// <para>
/// SparkPost webhooks carry no signature, so authenticity rests entirely on what you
/// configured when creating the webhook. Set either Basic authentication or a secret header,
/// and mirror the same values in the <see cref="WebhookRequest"/>.
/// </para>
/// <para>
/// Configure exactly one of: the secret header, Basic authentication, or
/// <see cref="AllowAnonymous"/>. The other combinations are refused at startup, because each
/// of them used to do less than it read as: with both pairs set only the header was checked,
/// and <see cref="AllowAnonymous"/> next to a configured check was ignored.
/// </para>
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

    /// <summary>
    /// Accepts every call without proving it genuine. Off by default, and there is no way to
    /// arrive at it by accident: an endpoint with no check configured refuses to start.
    /// </summary>
    /// <remarks>
    /// Only sane when something in front of the endpoint already does the checking — a gateway
    /// or a service mesh. Anyone who learns the URL can otherwise feed you forged bounce and
    /// unsubscribe events.
    /// </remarks>
    public bool AllowAnonymous { get; set; }

    private bool HasSecretHeader => SecretHeaderName is not null || SecretHeaderValue is not null;

    private bool HasBasicAuth => BasicAuthUsername is not null || BasicAuthPassword is not null;

    /// <summary>
    /// Rejects a configuration that would let calls through unchecked. A half-filled pair is the
    /// case worth catching: it used to disable the check silently, which is the one failure mode
    /// nobody notices until the forged events are already in the database.
    /// </summary>
    internal void Validate()
    {
        if (HasSecretHeader && (string.IsNullOrEmpty(SecretHeaderName) || string.IsNullOrEmpty(SecretHeaderValue)))
        {
            throw new InvalidOperationException(
                "SparkPostWebhookOptions: SecretHeaderName and SecretHeaderValue must both be set.");
        }

        if (HasBasicAuth && (string.IsNullOrEmpty(BasicAuthUsername) || string.IsNullOrEmpty(BasicAuthPassword)))
        {
            throw new InvalidOperationException(
                "SparkPostWebhookOptions: BasicAuthUsername and BasicAuthPassword must both be set.");
        }

        if (HasSecretHeader && HasBasicAuth)
        {
            throw new InvalidOperationException(
                "SparkPostWebhookOptions: configure either SecretHeaderName/SecretHeaderValue "
                + "or BasicAuthUsername/BasicAuthPassword, not both.");
        }

        if (AllowAnonymous && (HasSecretHeader || HasBasicAuth))
        {
            throw new InvalidOperationException(
                "SparkPostWebhookOptions: AllowAnonymous cannot be combined with a configured check. "
                + "Remove one of them.");
        }

        if (!HasSecretHeader && !HasBasicAuth && !AllowAnonymous)
        {
            throw new InvalidOperationException(
                "SparkPostWebhookOptions: no check is configured. Set SecretHeaderName/SecretHeaderValue "
                + "or BasicAuthUsername/BasicAuthPassword — SparkPost webhooks carry no signature, so an "
                + "unchecked endpoint accepts forged events from anyone who learns its URL. "
                + "If something in front of the endpoint already checks, set AllowAnonymous = true.");
        }
    }
}
