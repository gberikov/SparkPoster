using System.Text.Json.Serialization;

namespace SparkPoster;

/// <summary>The result of sending a transmission.</summary>
public sealed record TransmissionResponse
{
    /// <summary>The transmission identifier assigned by SparkPost.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>How many recipients were accepted for delivery.</summary>
    public int TotalAcceptedRecipients { get; init; }

    /// <summary>How many recipients were rejected, for example by the suppression list.</summary>
    public int TotalRejectedRecipients { get; init; }

    /// <summary>
    /// The response replays an earlier request that used the same idempotency key: the mail
    /// was already sent and is not sent a second time.
    /// </summary>
    /// <remarks>
    /// Filled in from a response header rather than from the body.
    /// </remarks>
    [JsonIgnore]
    public bool IsIdempotentReplay { get; init; }
}
