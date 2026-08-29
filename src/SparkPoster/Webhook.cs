using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SparkPoster.Internal;

namespace SparkPoster;

/// <summary>Способ авторизации, которым SparkPost стучится в ваш эндпоинт.</summary>
[JsonConverter(typeof(WebhookAuthTypeJsonConverter))]
public enum WebhookAuthType
{
    /// <summary>Значение, которого нет в этом перечислении: SparkPost добавил новый способ.</summary>
    Unknown = 0,

    /// <summary>Без авторизации. Годится только вместе с секретом в <see cref="WebhookRequest.CustomHeaders"/>.</summary>
    None,

    /// <summary>HTTP Basic.</summary>
    Basic,

    /// <summary>OAuth 2.0 — токен запрашивается по <see cref="WebhookRequest.AuthRequestDetails"/>.</summary>
    OAuth2,
}

/// <summary>Создание или изменение вебхука.</summary>
public sealed record WebhookRequest
{
    /// <summary>Имя вебхука.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// URL, куда POST-ом приходят батчи событий. Допустимы только стандартные порты:
    /// 80 для http и 443 для https.
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// Типы событий. Доступные значения — в <see cref="SparkPostEventTypes"/>
    /// или через <see cref="IWebhooks.GetEventsDocumentationAsync"/>.
    /// </summary>
    public required IReadOnlyList<string> Events { get; init; }

    /// <summary>Активен ли вебхук. Выключенный не получает батчи.</summary>
    public bool? Active { get; init; }

    /// <summary>
    /// Дополнительные заголовки запроса к вашему эндпоинту. Здесь же обычно живёт секрет,
    /// по которому вы отличаете настоящий вызов от поддельного.
    /// </summary>
    public IReadOnlyDictionary<string, string>? CustomHeaders { get; init; }

    /// <summary>Субаккаунты, события которых в этот вебхук не попадают. Не более 10.</summary>
    public IReadOnlyList<int>? ExceptionSubaccounts { get; init; }

    /// <summary>Способ авторизации.</summary>
    public WebhookAuthType? AuthType { get; init; }

    /// <summary>Параметры запроса токена. Обязательны при <see cref="WebhookAuthType.OAuth2"/>.</summary>
    public WebhookAuthRequestDetails? AuthRequestDetails { get; init; }

    /// <summary>Учётные данные. Обязательны при <see cref="WebhookAuthType.Basic"/>.</summary>
    public WebhookAuthCredentials? AuthCredentials { get; init; }
}

/// <summary>Вебхук.</summary>
public sealed record Webhook
{
    /// <summary>Идентификатор.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Имя вебхука.</summary>
    public string? Name { get; init; }

    /// <summary>URL, куда приходят батчи событий.</summary>
    public string? Target { get; init; }

    /// <summary>Типы событий.</summary>
    public IReadOnlyList<string>? Events { get; init; }

    /// <summary>Активен ли вебхук.</summary>
    public bool? Active { get; init; }

    /// <summary>Дополнительные заголовки запроса.</summary>
    public IReadOnlyDictionary<string, string>? CustomHeaders { get; init; }

    /// <summary>Субаккаунты, события которых сюда не попадают.</summary>
    public IReadOnlyList<int>? ExceptionSubaccounts { get; init; }

    /// <summary>Способ авторизации.</summary>
    public WebhookAuthType? AuthType { get; init; }

    /// <summary>Параметры запроса токена.</summary>
    public WebhookAuthRequestDetails? AuthRequestDetails { get; init; }

    /// <summary>Учётные данные.</summary>
    public WebhookAuthCredentials? AuthCredentials { get; init; }

    /// <summary>Когда батч в последний раз был доставлен успешно.</summary>
    public string? LastSuccessful { get; init; }

    /// <summary>Когда батч в последний раз доставить не удалось.</summary>
    public string? LastFailure { get; init; }
}

/// <summary>Параметры запроса OAuth-токена.</summary>
public sealed record WebhookAuthRequestDetails
{
    /// <summary>URL, по которому запрашивается токен.</summary>
    public string? Url { get; init; }

    /// <summary>Тело запроса токена: <c>client_id</c>, <c>client_secret</c>, <c>grant_type</c>.</summary>
    public JsonNode? Body { get; init; }

    /// <summary>Дополнительные заголовки запроса токена.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

/// <summary>Учётные данные для авторизации в вашем эндпоинте.</summary>
public sealed record WebhookAuthCredentials
{
    /// <summary>Имя пользователя для Basic-авторизации.</summary>
    public string? Username { get; init; }

    /// <summary>Пароль для Basic-авторизации.</summary>
    public string? Password { get; init; }

    /// <summary>Полученный OAuth-токен.</summary>
    public string? AccessToken { get; init; }

    /// <summary>Срок жизни OAuth-токена в секундах.</summary>
    public int? ExpiresIn { get; init; }
}

/// <summary>Результат проверки вебхука тестовым батчем.</summary>
public sealed record WebhookValidationResult
{
    /// <summary>Сообщение о результате.</summary>
    public string? Msg { get; init; }

    /// <summary>Что ответил ваш эндпоинт.</summary>
    public WebhookTargetResponse? Response { get; init; }
}

/// <summary>Ответ вашего эндпоинта на тестовый батч.</summary>
public sealed record WebhookTargetResponse
{
    /// <summary>HTTP-код ответа.</summary>
    public int? Status { get; init; }

    /// <summary>Заголовки ответа.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Тело ответа.</summary>
    public string? Body { get; init; }
}

/// <summary>
/// Состояние доставки одного батча событий. Хранится 24 часа.
/// </summary>
public sealed record WebhookBatchStatus
{
    /// <summary>Идентификатор батча. Он же приходит в заголовке <c>X-MessageSystems-Batch-ID</c>.</summary>
    public string? BatchId { get; init; }

    /// <summary>Идентификатор вебхука.</summary>
    public string? WebhookId { get; init; }

    /// <summary>Когда батч был создан.</summary>
    public DateTimeOffset? Ts { get; init; }

    /// <summary>Сколько событий было в батче.</summary>
    public int? BatchSize { get; init; }

    /// <summary>Сколько было неудачных попыток до доставки. Ноль, если получилось с первого раза.</summary>
    public int? Attempts { get; init; }

    /// <summary>Код ответа вашего эндпоинта.</summary>
    public int? ResponseCode { get; init; }

    /// <summary>Код ошибки, если доставить не удалось.</summary>
    public int? FailureCode { get; init; }

    /// <summary>Длительность всего запроса в миллисекундах.</summary>
    public int? Latency { get; init; }
}
