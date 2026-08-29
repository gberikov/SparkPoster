namespace SparkPoster;

/// <summary>Configuration for the SparkPost client.</summary>
public sealed class SparkPostOptions
{
    /// <summary>
    /// The API key. Keep it in environment variables or a secret store — never in an
    /// <c>appsettings.json</c> that is under version control.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The API base address. Defaults to <see cref="SparkPostEndpoints.Us"/>; use
    /// <see cref="SparkPostEndpoints.Eu"/> for an EU account. Enterprise accounts may
    /// have their own endpoint.
    /// </summary>
    public Uri BaseUrl { get; set; } = SparkPostEndpoints.Us;

    /// <summary>
    /// The default subaccount for every request this client makes. Usually left unset:
    /// <see cref="ISparkPostClient.ForSubaccount"/> is the better way to scope individual calls.
    /// </summary>
    public int? SubaccountId { get; set; }
}
