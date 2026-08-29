namespace SparkPoster;

/// <summary>Отправка писем.</summary>
public interface ITransmissions
{
    /// <summary>Отправляет письмо.</summary>
    /// <param name="transmission">Запрос на отправку, обычно собранный через <see cref="Transmission.Create"/>.</param>
    /// <param name="idempotencyKey">
    /// Ключ идемпотентности. Если не задан, генерируется автоматически — этого достаточно,
    /// чтобы повтор на транспортном уровне (например, из resilience-handler) не отправил
    /// письмо дважды. Задавайте явно, когда повторяете вызов из прикладного кода: тогда
    /// ключ надо вывести из бизнес-идентификатора, например из номера заказа.
    /// SparkPost помнит ключ 24 часа.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат отправки.</returns>
    /// <exception cref="SparkPostApiException">SparkPost ответил кодом ошибки.</exception>
    /// <exception cref="SparkPostRateLimitException">Превышен лимит запросов (429) или отправки (420).</exception>
    Task<TransmissionResponse> SendAsync(
        TransmissionRequest transmission,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);
}
