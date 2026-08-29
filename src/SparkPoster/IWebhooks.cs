using System.Text.Json.Nodes;

namespace SparkPoster;

/// <summary>Управление вебхуками событий.</summary>
public interface IWebhooks
{
    /// <summary>Создаёт вебхук.</summary>
    /// <param name="webhook">Описание вебхука.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор созданного вебхука.</returns>
    /// <remarks>
    /// При создании SparkPost делает тестовый POST на <see cref="WebhookRequest.Target"/>.
    /// Если эндпоинт не ответит 200, вебхук не создастся, а запрос завершится ошибкой 400.
    /// Данные начнут приходить примерно через минуту после создания.
    /// </remarks>
    Task<string> CreateAsync(WebhookRequest webhook, CancellationToken cancellationToken = default);

    /// <summary>Возвращает вебхук.</summary>
    /// <param name="id">Идентификатор вебхука.</param>
    /// <param name="timezone">Часовой пояс для дат в ответе, например <c>America/New_York</c>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Вебхук.</returns>
    Task<Webhook> GetAsync(string id, string? timezone = null, CancellationToken cancellationToken = default);

    /// <summary>Возвращает все вебхуки.</summary>
    /// <param name="timezone">Часовой пояс для дат в ответе.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список вебхуков.</returns>
    Task<IReadOnlyList<Webhook>> ListAsync(string? timezone = null, CancellationToken cancellationToken = default);

    /// <summary>Изменяет вебхук.</summary>
    /// <param name="id">Идентификатор вебхука.</param>
    /// <param name="webhook">Новые значения. Массивы заменяются целиком, а не дополняются.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после изменения.</returns>
    Task UpdateAsync(string id, WebhookRequest webhook, CancellationToken cancellationToken = default);

    /// <summary>Удаляет вебхук.</summary>
    /// <param name="id">Идентификатор вебхука.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача, завершающаяся после удаления.</returns>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Отправляет на эндпоинт тестовый батч и возвращает, что тот ответил.</summary>
    /// <param name="id">Идентификатор вебхука.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат проверки.</returns>
    Task<WebhookValidationResult> ValidateAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Возвращает состояние доставки последних батчей.</summary>
    /// <param name="id">Идентификатор вебхука.</param>
    /// <param name="limit">Сколько записей вернуть. По умолчанию SparkPost отдаёт 1000.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Состояния батчей за последние 24 часа.</returns>
    /// <remarks>
    /// Батч, не получивший ответ 200, повторяется в течение 8 часов, после чего отбрасывается.
    /// </remarks>
    Task<IReadOnlyList<WebhookBatchStatus>> GetBatchStatusAsync(
        string id,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает описание всех типов событий и их полей как есть, в виде JSON.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Документация по событиям.</returns>
    /// <remarks>
    /// Ответ отдаётся необработанным: это справочник, структура которого меняется вместе
    /// с API, и типизировать его — значит устаревать вместе с ним.
    /// </remarks>
    Task<JsonNode> GetEventsDocumentationAsync(CancellationToken cancellationToken = default);

    /// <summary>Возвращает примеры событий как есть, в виде JSON.</summary>
    /// <param name="events">Типы событий; если не заданы, возвращаются все.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Примеры событий — удобны как фикстуры для тестов вашего обработчика.</returns>
    Task<JsonNode> GetEventSamplesAsync(
        IEnumerable<string>? events = null,
        CancellationToken cancellationToken = default);
}
