using System.Text.Json.Serialization;
using SparkPoster.Webhooks;

namespace SparkPoster.Internal;

/// <summary>
/// The serialization context. Source-generated rather than reflection-based: the library
/// has to stay usable under trimming and Native AOT.
/// </summary>
/// <remarks>
/// <see cref="JsonNumberHandling.AllowReadingFromString"/> is not paranoia here: SparkPost
/// returns the same numeric fields sometimes as numbers and sometimes as strings — the batch
/// status <c>response_code</c>, for instance, is documented as a number but shows up quoted
/// in the examples.
/// </remarks>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(TransmissionRequest))]
[JsonSerializable(typeof(SparkPostEnvelope<TransmissionResponse>), TypeInfoPropertyName = "TransmissionEnvelope")]
[JsonSerializable(typeof(SparkPostErrorEnvelope))]
[JsonSerializable(typeof(List<Recipient>))]
[JsonSerializable(typeof(IReadOnlyList<Recipient>))]
[JsonSerializable(typeof(WebhookRequest))]
[JsonSerializable(typeof(SparkPostEnvelope<CreatedResource>), TypeInfoPropertyName = "WebhookIdEnvelope")]
[JsonSerializable(typeof(SparkPostEnvelope<Webhook>), TypeInfoPropertyName = "WebhookEnvelope")]
[JsonSerializable(typeof(SparkPostEnvelope<IReadOnlyList<Webhook>>), TypeInfoPropertyName = "WebhookListEnvelope")]
[JsonSerializable(typeof(SparkPostEnvelope<WebhookValidationResult>), TypeInfoPropertyName = "WebhookValidationEnvelope")]
[JsonSerializable(
    typeof(SparkPostEnvelope<IReadOnlyList<WebhookBatchStatus>>),
    TypeInfoPropertyName = "WebhookBatchStatusListEnvelope")]
[JsonSerializable(typeof(MessageEvent))]
[JsonSerializable(typeof(TrackEvent))]
[JsonSerializable(typeof(GenerationEvent))]
[JsonSerializable(typeof(UnsubscribeEvent))]
[JsonSerializable(typeof(RelayEvent))]
[JsonSerializable(typeof(string))]
internal sealed partial class SparkPostJsonContext : JsonSerializerContext;
