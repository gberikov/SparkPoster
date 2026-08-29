using System.Text.Json.Serialization;

namespace SparkPoster;

/// <summary>Результат отправки письма.</summary>
public sealed record TransmissionResponse
{
    /// <summary>Идентификатор письма в SparkPost.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Сколько получателей принято к отправке.</summary>
    public int TotalAcceptedRecipients { get; init; }

    /// <summary>Сколько получателей отклонено (например, из-за списка подавления).</summary>
    public int TotalRejectedRecipients { get; init; }

    /// <summary>
    /// Ответ является повтором ранее выполненного запроса с тем же ключом идемпотентности:
    /// письмо уже было отправлено, второй раз оно не уходит.
    /// </summary>
    /// <remarks>
    /// Заполняется из заголовка ответа, а не из тела.
    /// </remarks>
    [JsonIgnore]
    public bool IsIdempotentReplay { get; init; }
}
