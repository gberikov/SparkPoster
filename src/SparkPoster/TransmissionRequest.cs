using System.Text.Json.Nodes;

namespace SparkPoster;

/// <summary>
/// Запрос на отправку письма. Обычно собирается через <see cref="Transmission.Create"/>,
/// но заполнить его вручную тоже можно — и объект сериализуем, поэтому его можно
/// сохранить или положить в очередь и отправить позже.
/// </summary>
public sealed record TransmissionRequest
{
    /// <summary>Содержимое письма.</summary>
    public required TransmissionContent Content { get; init; }

    /// <summary>Получатели.</summary>
    public required IReadOnlyList<Recipient> Recipients { get; init; }

    /// <summary>
    /// Данные подстановки уровня письма. Данные получателя имеют приоритет над этими.
    /// </summary>
    public JsonNode? SubstitutionData { get; init; }

    /// <summary>
    /// Метаданные уровня письма: доступны в событиях вебхуков и в языке шаблонов.
    /// Ограничение SparkPost — 10 КБ.
    /// </summary>
    public JsonNode? Metadata { get; init; }

    /// <summary>Параметры отправки.</summary>
    public TransmissionOptions? Options { get; init; }

    /// <summary>Идентификатор кампании — по нему группируется статистика и удаляются отложенные письма.</summary>
    public string? CampaignId { get; init; }

    /// <summary>Описание письма для собственного удобства.</summary>
    public string? Description { get; init; }

    /// <summary>Адрес возврата (envelope FROM), куда приходят отбойники.</summary>
    public string? ReturnPath { get; init; }
}

/// <summary>Содержимое письма, заданное напрямую (inline).</summary>
public sealed record TransmissionContent
{
    /// <summary>Отправитель.</summary>
    public Address? From { get; init; }

    /// <summary>Тема письма. Поддерживает язык шаблонов.</summary>
    public string? Subject { get; init; }

    /// <summary>HTML-версия письма.</summary>
    public string? Html { get; init; }

    /// <summary>Текстовая версия письма.</summary>
    public string? Text { get; init; }

    /// <summary>AMP-версия письма.</summary>
    public string? AmpHtml { get; init; }

    /// <summary>Адрес для ответа.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>Дополнительные заголовки письма.</summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }
}

/// <summary>Почтовый адрес.</summary>
public sealed record Address
{
    /// <summary>Адрес электронной почты.</summary>
    public required string Email { get; init; }

    /// <summary>Отображаемое имя.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// Адрес, который получатель увидит в заголовке <c>To</c>. Так реализуются копии:
    /// у CC- и BCC-получателей здесь стоит адрес основного получателя.
    /// </summary>
    public string? HeaderTo { get; init; }
}

/// <summary>Получатель письма.</summary>
public sealed record Recipient
{
    /// <summary>Адрес получателя.</summary>
    public required Address Address { get; init; }

    /// <summary>Данные подстановки для этого получателя. Имеют приоритет над данными уровня письма.</summary>
    public JsonNode? SubstitutionData { get; init; }

    /// <summary>Метаданные получателя. Имеют приоритет над метаданными уровня письма.</summary>
    public JsonNode? Metadata { get; init; }

    /// <summary>Метки получателя.</summary>
    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>Параметры отправки.</summary>
public sealed record TransmissionOptions
{
    /// <summary>Отслеживать открытия.</summary>
    public bool? OpenTracking { get; init; }

    /// <summary>Отслеживать переходы по ссылкам.</summary>
    public bool? ClickTracking { get; init; }

    /// <summary>
    /// Транзакционное письмо. Влияет на проверку списка подавления: транзакционные
    /// письма не блокируются отписками от массовых рассылок.
    /// </summary>
    public bool? Transactional { get; init; }

    /// <summary>
    /// Отправка через sandbox-домен <c>sparkpostbox.com</c>.
    /// Лимит — 5 писем за всё время жизни аккаунта.
    /// </summary>
    public bool? Sandbox { get; init; }

    /// <summary>Не проверять список подавления. Требует отдельного разрешения у SparkPost.</summary>
    public bool? SkipSuppression { get; init; }

    /// <summary>Пул IP-адресов для отправки.</summary>
    public string? IpPool { get; init; }

    /// <summary>Встроить CSS в HTML перед отправкой.</summary>
    public bool? InlineCss { get; init; }

    /// <summary>Выполнять подстановки в содержимом.</summary>
    public bool? PerformSubstitutions { get; init; }

    /// <summary>
    /// Время отправки для отложенного письма. SparkPost не принимает время
    /// более чем на 3 суток вперёд.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }
}
