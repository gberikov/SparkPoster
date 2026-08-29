using System.Text.Json.Serialization;

namespace SparkPoster.Internal;

/// <summary>
/// Контекст сериализации. Source-gen, а не рефлексия: библиотека должна оставаться
/// пригодной для trimming и Native AOT.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TransmissionRequest))]
[JsonSerializable(typeof(SparkPostEnvelope<TransmissionResponse>), TypeInfoPropertyName = "TransmissionEnvelope")]
[JsonSerializable(typeof(SparkPostErrorEnvelope))]
[JsonSerializable(typeof(List<Recipient>))]
[JsonSerializable(typeof(IReadOnlyList<Recipient>))]
internal sealed partial class SparkPostJsonContext : JsonSerializerContext;
