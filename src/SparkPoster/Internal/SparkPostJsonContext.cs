using System.Text.Json.Serialization;

namespace SparkPoster.Internal;

/// <summary>
/// Контекст сериализации. Source-gen, а не рефлексия: библиотека должна оставаться
/// пригодной для trimming и Native AOT.
/// </summary>
/// <remarks>
/// <see cref="JsonNumberHandling.AllowReadingFromString"/> здесь не перестраховка:
/// SparkPost отдаёт одни и те же числовые поля то числами, то строками — например,
/// <c>response_code</c> в статусе батча описан числом, а в примерах приходит строкой.
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
internal sealed partial class SparkPostJsonContext : JsonSerializerContext;
