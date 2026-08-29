using System.Text.Json.Nodes;
using SparkPoster.Internal;

namespace SparkPoster.Webhooks;

/// <summary>
/// Parses the event batches SparkPost posts to your endpoint.
/// </summary>
/// <remarks>
/// <para>
/// SparkPost webhooks carry <b>no signature</b>: a call is only proven genuine by whatever you
/// configured when creating the webhook — Basic authentication, OAuth, or a secret header.
/// Your endpoint must run over HTTPS and must check that secret, otherwise anyone can feed you
/// forged bounce and unsubscribe events.
/// </para>
/// <para>
/// Delivery is at-least-once and unordered: a batch that does not get a 200 is retried for
/// 8 hours. Deduplicate on <see cref="SparkPostEventBatch.BatchId"/> or on
/// <see cref="SparkPostEvent.EventId"/>.
/// </para>
/// </remarks>
public static class SparkPostWebhookParser
{
    /// <summary>The header carrying the batch identifier.</summary>
    public const string BatchIdHeader = "X-MessageSystems-Batch-ID";

    /// <summary>Parses a batch from a string.</summary>
    /// <param name="json">The request body.</param>
    /// <returns>The events of the batch.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="json"/> is <c>null</c>.</exception>
    /// <exception cref="System.Text.Json.JsonException">The body is not valid JSON.</exception>
    public static IReadOnlyList<SparkPostEvent> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return SparkPostEventReader.Read(JsonNode.Parse(json));
    }

    /// <summary>Parses a batch from a stream.</summary>
    /// <param name="stream">The request body.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The events of the batch.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <c>null</c>.</exception>
    /// <exception cref="System.Text.Json.JsonException">The body is not valid JSON.</exception>
    /// <exception cref="IOException">Reading the stream failed.</exception>
    public static async Task<IReadOnlyList<SparkPostEvent>> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return SparkPostEventReader.Read(node);
    }
}

/// <summary>A batch of events together with its identifier.</summary>
public sealed record SparkPostEventBatch
{
    /// <summary>
    /// The batch identifier from the <see cref="SparkPostWebhookParser.BatchIdHeader"/> header.
    /// Use it to discard repeated deliveries of the same batch.
    /// </summary>
    public string? BatchId { get; init; }

    /// <summary>The events of the batch.</summary>
    public required IReadOnlyList<SparkPostEvent> Events { get; init; }
}
