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

    /// <summary>Отменяет отложенные письма кампании.</summary>
    /// <param name="campaignId">Идентификатор кампании.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после принятия запроса.</returns>
    /// <remarks>
    /// SparkPost отвечает сразу, а удаление идёт в фоне: по каждому отменённому письму
    /// придёт событие <c>bounce</c> с причиной «554 5.7.1 [internal] Campaign cancelled».
    /// Чтобы отменить письма субаккаунта, запрос надо делать от его имени —
    /// через <see cref="ISparkPostClient.ForSubaccount"/> или ключом субаккаунта.
    /// </remarks>
    Task DeleteByCampaignAsync(string campaignId, CancellationToken cancellationToken = default);
}
