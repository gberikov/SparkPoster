using System.Text.Json.Serialization;
using SparkPoster.Internal;

namespace SparkPoster;

/// <summary>
/// A single error from a SparkPost response body.
/// </summary>
/// <remarks>
/// The useful text usually lives in <see cref="Description"/> ("content object or
/// template_id required"), while <see cref="Message"/> stays generic ("required field
/// is missing").
/// </remarks>
public sealed record SparkPostError
{
    /// <summary>A short statement of what went wrong.</summary>
    public string? Message { get; init; }

    /// <summary>The detailed description — usually the most informative part of the response.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// The SparkPost error code, for example <c>1600</c> for a reused idempotency key.
    /// </summary>
    /// <remarks>Arrives as a string or as a number, depending on the endpoint.</remarks>
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string? Code { get; init; }
}
