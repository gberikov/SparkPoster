using System.Net;

namespace SparkPoster;

/// <summary>The base exception for this library.</summary>
public class SparkPostException : Exception
{
    /// <summary>Creates an exception with a message.</summary>
    /// <param name="message">The message text.</param>
    public SparkPostException(string message) : base(message)
    {
    }

    /// <summary>Creates an exception with a message and an inner exception.</summary>
    /// <param name="message">The message text.</param>
    /// <param name="innerException">The inner exception.</param>
    public SparkPostException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// SparkPost answered with an error status. Tell the cases apart by <see cref="StatusCode"/>.
/// </summary>
public class SparkPostApiException : SparkPostException
{
    /// <summary>Creates an exception from an API response.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="errors">Errors parsed from the response body.</param>
    /// <param name="rawBody">The response body as received.</param>
    public SparkPostApiException(HttpStatusCode statusCode, IReadOnlyList<SparkPostError> errors, string? rawBody)
        : base(BuildMessage(statusCode, errors))
    {
        StatusCode = statusCode;
        Errors = errors;
        RawBody = rawBody;
    }

    /// <summary>The HTTP status code of the response.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Errors from the response body. Empty when the body could not be parsed.</summary>
    public IReadOnlyList<SparkPostError> Errors { get; }

    /// <summary>
    /// The response body as received — useful when the server replied with something other
    /// than JSON, such as a proxy stub or an HTML page.
    /// </summary>
    /// <remarks>
    /// May contain personal data: validation errors echo recipient addresses back.
    /// Think before dumping this into your logs.
    /// </remarks>
    public string? RawBody { get; }

    private static string BuildMessage(HttpStatusCode statusCode, IReadOnlyList<SparkPostError> errors)
    {
        var first = errors.Count > 0 ? errors[0] : null;
        var detail = first?.Description ?? first?.Message;

        return detail is null
            ? $"SparkPost returned {(int)statusCode} {statusCode}."
            : $"SparkPost returned {(int)statusCode} {statusCode}: {detail}";
    }
}

/// <summary>
/// The request rate limit (429) or the sending limit (420) was exceeded.
/// </summary>
/// <remarks>
/// A separate type exists for the sake of <see cref="RetryAfter"/>: that value appears
/// nowhere else, and retry policies are built on it.
/// </remarks>
public sealed class SparkPostRateLimitException : SparkPostApiException
{
    /// <summary>Creates an exception from an API response.</summary>
    /// <param name="statusCode">The HTTP status code (429 or 420).</param>
    /// <param name="errors">Errors parsed from the response body.</param>
    /// <param name="rawBody">The response body as received.</param>
    /// <param name="retryAfter">The value of the <c>Retry-After</c> header, when present.</param>
    public SparkPostRateLimitException(
        HttpStatusCode statusCode,
        IReadOnlyList<SparkPostError> errors,
        string? rawBody,
        TimeSpan? retryAfter)
        : base(statusCode, errors, rawBody)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>How long to wait before retrying, when the server said so.</summary>
    public TimeSpan? RetryAfter { get; }
}
