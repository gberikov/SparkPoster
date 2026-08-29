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

    /// <summary>Получатели: перечисленные явно либо сохранённый список.</summary>
    public required RecipientSet Recipients { get; init; }

    /// <summary>
    /// Данные подстановки уровня письма. Данные получателя имеют приоритет над этими.
    /// Ограничение SparkPost — 100 КБ.
    /// </summary>
    public JsonNode? SubstitutionData { get; init; }

    /// <summary>
    /// Метаданные уровня письма: доступны в событиях вебхуков и в языке шаблонов.
    /// Ограничение SparkPost — 10 КБ.
    /// </summary>
    public JsonNode? Metadata { get; init; }

    /// <summary>Параметры отправки.</summary>
    public TransmissionOptions? Options { get; init; }

    /// <summary>
    /// Переопределение полей сохранённого шаблона. Подстановки здесь не поддерживаются.
    /// </summary>
    public ContentOverride? Override { get; init; }

    /// <summary>Идентификатор кампании. Не длиннее 64 байт.</summary>
    public string? CampaignId { get; init; }

    /// <summary>Описание письма. Не длиннее 1024 байт.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// Адрес возврата (envelope FROM), куда приходят отбойники.
    /// Домен должен быть CNAME-подтверждённым доменом отправки.
    /// </summary>
    public string? ReturnPath { get; init; }

    /// <summary>
    /// Домен отслеживания для оборачивания ссылок. Должен быть подтверждён,
    /// иначе запрос отклоняется с кодом 400.
    /// </summary>
    public string? TrackingDomain { get; init; }
}

/// <summary>
/// Содержимое письма: заданное напрямую, сохранённый шаблон, A/B-тест или сырой RFC822.
/// Способы взаимоисключающие.
/// </summary>
public sealed record TransmissionContent
{
    /// <summary>Отправитель. Домен должен быть подтверждённым доменом отправки.</summary>
    public Address? From { get; init; }

    /// <summary>Тема письма. Поддерживает язык шаблонов.</summary>
    public string? Subject { get; init; }

    /// <summary>HTML-версия письма.</summary>
    public string? Html { get; init; }

    /// <summary>Текстовая версия письма.</summary>
    public string? Text { get; init; }

    /// <summary>AMP-версия письма. Требует, чтобы был задан ещё html или text.</summary>
    public string? AmpHtml { get; init; }

    /// <summary>Адрес для ответа.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>
    /// Дополнительные заголовки. <c>Subject</c>, <c>From</c>, <c>To</c>, <c>Reply-To</c>,
    /// <c>Content-Type</c> и <c>Content-Transfer-Encoding</c> задавать нельзя —
    /// они формируются автоматически.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>Вложения. Общий размер содержимого письма — не более 20 МБ.</summary>
    public IReadOnlyList<Attachment>? Attachments { get; init; }

    /// <summary>
    /// Встроенные изображения. Вставляются в HTML через <c>cid:</c> с именем изображения.
    /// </summary>
    public IReadOnlyList<Attachment>? InlineImages { get; init; }

    /// <summary>Идентификатор сохранённого шаблона.</summary>
    public string? TemplateId { get; init; }

    /// <summary>Использовать черновик шаблона вместо опубликованной версии.</summary>
    public bool? UseDraftTemplate { get; init; }

    /// <summary>Идентификатор A/B-теста. A/B-тесты работают только с одним получателем.</summary>
    public string? AbTestId { get; init; }

    /// <summary>Готовое письмо в формате RFC822.</summary>
    public string? EmailRfc822 { get; init; }
}

/// <summary>Переопределение полей сохранённого шаблона.</summary>
public sealed record ContentOverride
{
    /// <summary>Отправитель.</summary>
    public Address? From { get; init; }

    /// <summary>Адрес для ответа.</summary>
    public string? ReplyTo { get; init; }

    /// <summary>Значение заголовка <c>List-Unsubscribe</c>.</summary>
    public string? ListId { get; init; }
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

    /// <summary>
    /// Переопределение отслеживания для этого получателя. Игнорируется, если
    /// используется сохранённый список получателей.
    /// </summary>
    public RecipientOptions? Options { get; init; }

    /// <summary>Индивидуальный адрес возврата (VERP).</summary>
    public string? ReturnPath { get; init; }
}

/// <summary>Переопределение отслеживания на уровне получателя.</summary>
public sealed record RecipientOptions
{
    /// <summary>Отслеживать открытия.</summary>
    public bool? OpenTracking { get; init; }

    /// <summary>Отслеживать переходы по ссылкам.</summary>
    public bool? ClickTracking { get; init; }

    /// <summary>Использовать пиксель начального открытия.</summary>
    public bool? InitialOpen { get; init; }
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
    /// Лимит — 5 писем за всё время жизни аккаунта, и работает только с шаблоном
    /// <c>my-first-email</c>.
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

    /// <summary>Идентификатор DKIM-ключа для подписи.</summary>
    public string? DkimKey { get; init; }

    /// <summary>
    /// Время отправки для отложенного письма. SparkPost не принимает время
    /// более чем на 3 суток вперёд.
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }
}
