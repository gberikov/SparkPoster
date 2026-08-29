using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using SparkPoster.Internal;

namespace SparkPoster.Webhooks;

/// <summary>
/// Событие из батча вебхука.
/// </summary>
/// <remarks>
/// Типизированы наиболее употребимые поля; всё остальное, включая поля, добавленные
/// SparkPost после выхода этой версии библиотеки, лежит в <see cref="Extra"/>.
/// Незнакомый тип события не приводит к исключению — он становится
/// <see cref="UnknownSparkPostEvent"/>.
/// </remarks>
public abstract record SparkPostEvent
{
    /// <summary>Тип события: <c>delivery</c>, <c>bounce</c>, <c>open</c> и так далее.</summary>
    /// <remarks>Известные значения перечислены в <see cref="SparkPostEventTypes"/>.</remarks>
    public string? Type { get; init; }

    /// <summary>Уникальный идентификатор события. Годится для защиты от повторной обработки.</summary>
    public string? EventId { get; init; }

    /// <summary>Момент события.</summary>
    [JsonConverter(typeof(UnixTimestampJsonConverter))]
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Кампания, в рамках которой отправлено письмо.</summary>
    public string? CampaignId { get; init; }

    /// <summary>Письмо, породившее событие.</summary>
    public string? TransmissionId { get; init; }

    /// <summary>Идентификатор сообщения в SparkPost.</summary>
    public string? MessageId { get; init; }

    /// <summary>Адрес получателя в нижнем регистре.</summary>
    public string? RcptTo { get; init; }

    /// <summary>Исходный адрес получателя.</summary>
    public string? RawRcptTo { get; init; }

    /// <summary>Тип получателя: <c>cc</c>, <c>bcc</c> или пусто для основного.</summary>
    public string? RcptType { get; init; }

    /// <summary>Метаданные получателя, переданные при отправке.</summary>
    public JsonNode? RcptMeta { get; init; }

    /// <summary>Метки получателя.</summary>
    public IReadOnlyList<string>? RcptTags { get; init; }

    /// <summary>Субаккаунт, от лица которого отправлено письмо.</summary>
    public string? SubaccountId { get; init; }

    /// <summary>Шаблон, по которому построено письмо.</summary>
    public string? TemplateId { get; init; }

    /// <summary>Версия шаблона.</summary>
    public string? TemplateVersion { get; init; }

    /// <summary>Значение заголовка From исходного письма.</summary>
    public string? FriendlyFrom { get; init; }

    /// <summary>Тема письма.</summary>
    public string? Subject { get; init; }

    /// <summary>Пул IP-адресов, через который отправлено письмо.</summary>
    public string? IpPool { get; init; }

    /// <summary>Было ли письмо помечено транзакционным.</summary>
    public string? Transactional { get; init; }

    /// <summary>
    /// Поля, которых нет среди типизированных, — включая появившиеся в API уже после
    /// выхода этой версии библиотеки. Ничего не теряется.
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>
/// Событие жизненного цикла письма: приём, доставка, отбойник, задержка, жалоба.
/// Категория <c>message_event</c>.
/// </summary>
public sealed record MessageEvent : SparkPostEvent
{
    /// <summary>Классификационный код отбойника.</summary>
    public string? BounceClass { get; init; }

    /// <summary>Код ошибки принимающего сервера.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Приведённый к канонической форме ответ принимающего сервера.</summary>
    public string? Reason { get; init; }

    /// <summary>Дословный ответ принимающего сервера.</summary>
    public string? RawReason { get; init; }

    /// <summary>Сколько попыток доставки не удалось до этой.</summary>
    public string? NumRetries { get; init; }

    /// <summary>IP, с которого отправлено письмо.</summary>
    public string? SendingIp { get; init; }

    /// <summary>IP хоста, которому доставлено письмо.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Домен получателя.</summary>
    public string? RecipientDomain { get; init; }

    /// <summary>Домен, принимающий письмо.</summary>
    public string? RoutingDomain { get; init; }

    /// <summary>Размер письма в байтах.</summary>
    public string? MsgSize { get; init; }

    /// <summary>Протокол доставки.</summary>
    public string? DelvMethod { get; init; }

    /// <summary>Почтовый провайдер получателя.</summary>
    public string? MailboxProvider { get; init; }

    /// <summary>Регион почтового провайдера.</summary>
    public string? MailboxProviderRegion { get; init; }

    /// <summary>Когда письмо было принято в SparkPost.</summary>
    public string? InjectionTime { get; init; }
}

/// <summary>
/// Событие вовлечённости: открытие, переход по ссылке, их AMP-варианты.
/// Категория <c>track_event</c>.
/// </summary>
public sealed record TrackEvent : SparkPostEvent
{
    /// <summary>User-Agent, с которого пришёл запрос.</summary>
    public string? UserAgent { get; init; }

    /// <summary>IP, с которого пришёл запрос.</summary>
    public string? IpAddress { get; init; }

    /// <summary>Адрес ссылки, по которой перешли.</summary>
    public string? TargetLinkUrl { get; init; }

    /// <summary>Имя ссылки, по которой перешли.</summary>
    public string? TargetLinkName { get; init; }

    /// <summary>Геоданные по IP.</summary>
    public JsonNode? GeoIp { get; init; }

    /// <summary>Открытие зафиксировано пикселем начального открытия.</summary>
    public string? InitialPixel { get; init; }
}

/// <summary>
/// Событие формирования письма: не удалось построить или построение отклонено.
/// Категория <c>gen_event</c>.
/// </summary>
public sealed record GenerationEvent : SparkPostEvent
{
    /// <summary>Код ошибки.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Причина отказа.</summary>
    public string? Reason { get; init; }

    /// <summary>Дословная причина отказа.</summary>
    public string? RawReason { get; init; }

    /// <summary>Данные подстановки получателя.</summary>
    public JsonNode? RcptSubs { get; init; }
}

/// <summary>
/// Отписка — по заголовку List-Unsubscribe или по ссылке в письме.
/// Категория <c>unsubscribe_event</c>.
/// </summary>
public sealed record UnsubscribeEvent : SparkPostEvent
{
    /// <summary>Адрес, с которого пришёл запрос на отписку.</summary>
    public string? MailFrom { get; init; }

    /// <summary>User-Agent, с которого пришёл запрос.</summary>
    public string? UserAgent { get; init; }

    /// <summary>IP, с которого пришёл запрос.</summary>
    public string? IpAddress { get; init; }
}

/// <summary>
/// Событие входящей почты (relay). Категория <c>relay_event</c>.
/// </summary>
public sealed record RelayEvent : SparkPostEvent
{
    /// <summary>Идентификатор relay-события.</summary>
    public string? RelayId { get; init; }

    /// <summary>Вебхук, принявший письмо.</summary>
    public string? WebhookId { get; init; }

    /// <summary>Протокол приёма.</summary>
    public string? Protocol { get; init; }

    /// <summary>Содержимое входящего письма.</summary>
    public JsonNode? Content { get; init; }

    /// <summary>Отправитель на уровне SMTP-конверта.</summary>
    public string? MsgFrom { get; init; }

    /// <summary>Причина отказа.</summary>
    public string? Reason { get; init; }

    /// <summary>Код ошибки.</summary>
    public string? ErrorCode { get; init; }
}

/// <summary>
/// Событие из категории, которой библиотека не знает.
/// </summary>
/// <remarks>
/// Существует, чтобы новая категория событий в SparkPost не роняла обработчик:
/// упавший обработчик заставляет SparkPost повторять весь батч, включая уже
/// обработанные события.
/// </remarks>
public sealed record UnknownSparkPostEvent : SparkPostEvent
{
    /// <summary>Имя категории, как она пришла внутри <c>msys</c>.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Тело события целиком, как пришло.</summary>
    public JsonNode? Raw { get; init; }
}
