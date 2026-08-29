using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SparkPoster;

/// <summary>Точка входа для сборки письма.</summary>
public static class Transmission
{
    /// <summary>Создаёт построитель письма.</summary>
    /// <returns>Новый построитель.</returns>
    public static TransmissionBuilder Create() => new();
}

/// <summary>
/// Построитель письма. Не потокобезопасен: один экземпляр — одно письмо в одном потоке.
/// </summary>
/// <remarks>
/// <para>
/// Построитель ничего не отправляет: <see cref="Build"/> возвращает
/// <see cref="TransmissionRequest"/>, который затем передаётся в
/// <see cref="ITransmissions.SendAsync"/>. Готовый запрос можно сохранить, сериализовать
/// или переиспользовать через <c>with</c>.
/// </para>
/// <para>
/// Содержимое задаётся ровно одним способом: <see cref="Html"/>/<see cref="Text"/>,
/// <see cref="Template"/>, <see cref="AbTest"/> или <see cref="RawRfc822"/>.
/// Смешение способов обнаруживается в <see cref="Build"/>.
/// </para>
/// </remarks>
public sealed class TransmissionBuilder
{
    /// <summary>
    /// Данные подстановки сериализуются без политики именования: имена переменных
    /// шаблона должны остаться ровно такими, как их написал вызывающий.
    /// </summary>
    private static readonly JsonSerializerOptions UserDataOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly List<Recipient> _recipients = [];
    private readonly List<Address> _cc = [];
    private readonly List<Address> _bcc = [];
    private readonly List<Attachment> _attachments = [];
    private readonly List<Attachment> _inlineImages = [];
    private Dictionary<string, string>? _headers;
    private TransmissionOptions _options = new();
    private ContentOverride? _override;
    private Address? _from;
    private string? _subject;
    private string? _html;
    private string? _text;
    private string? _ampHtml;
    private string? _replyTo;
    private string? _templateId;
    private bool? _useDraftTemplate;
    private string? _abTestId;
    private string? _rfc822;
    private string? _recipientListId;
    private string? _campaignId;
    private string? _description;
    private string? _returnPath;
    private string? _trackingDomain;
    private JsonNode? _substitutionData;
    private JsonNode? _metadata;

    /// <summary>Задаёт отправителя.</summary>
    /// <param name="email">Адрес отправителя.</param>
    /// <param name="name">Отображаемое имя.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder From(string email, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        _from = new Address { Email = email, Name = name };
        return this;
    }

    /// <summary>Добавляет получателя.</summary>
    /// <param name="email">Адрес получателя.</param>
    /// <param name="name">Отображаемое имя.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder To(string email, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        _recipients.Add(new Recipient { Address = new Address { Email = email, Name = name } });
        return this;
    }

    /// <summary>Добавляет получателя целиком — с его данными подстановки, метаданными и метками.</summary>
    /// <param name="recipient">Получатель.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder To(Recipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        _recipients.Add(recipient);
        return this;
    }

    /// <summary>Добавляет несколько получателей.</summary>
    /// <param name="recipients">Получатели.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder To(IEnumerable<Recipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        _recipients.AddRange(recipients);
        return this;
    }

    /// <summary>Добавляет получателя копии.</summary>
    /// <param name="email">Адрес получателя копии.</param>
    /// <param name="name">Отображаемое имя.</param>
    /// <returns>Тот же построитель.</returns>
    /// <remarks>
    /// В SparkPost нет отдельного поля для копий: получатель копии добавляется в общий
    /// список с подменённым заголовком <c>To</c>, а его адрес дописывается в заголовок
    /// <c>CC</c>. Построитель делает это за вас в <see cref="Build"/>.
    /// </remarks>
    public TransmissionBuilder Cc(string email, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        _cc.Add(new Address { Email = email, Name = name });
        return this;
    }

    /// <summary>Добавляет скрытого получателя.</summary>
    /// <param name="email">Адрес скрытого получателя.</param>
    /// <param name="name">Отображаемое имя.</param>
    /// <returns>Тот же построитель.</returns>
    /// <remarks>
    /// Скрытый получатель добавляется в общий список с подменённым заголовком <c>To</c>
    /// и, в отличие от копии, нигде в заголовках не упоминается.
    /// </remarks>
    public TransmissionBuilder Bcc(string email, string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        _bcc.Add(new Address { Email = email, Name = name });
        return this;
    }

    /// <summary>Отправляет письмо по сохранённому списку получателей.</summary>
    /// <param name="listId">Идентификатор списка.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder RecipientList(string listId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listId);
        _recipientListId = listId;
        return this;
    }

    /// <summary>Задаёт тему письма. Поддерживает язык шаблонов.</summary>
    /// <param name="subject">Тема.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Subject(string subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        _subject = subject;
        return this;
    }

    /// <summary>Задаёт HTML-версию письма.</summary>
    /// <param name="html">HTML-содержимое.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Html(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        _html = html;
        return this;
    }

    /// <summary>Задаёт текстовую версию письма.</summary>
    /// <param name="text">Текстовое содержимое.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Text(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        return this;
    }

    /// <summary>Задаёт AMP-версию письма.</summary>
    /// <param name="ampHtml">AMP-содержимое.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder AmpHtml(string ampHtml)
    {
        ArgumentNullException.ThrowIfNull(ampHtml);
        _ampHtml = ampHtml;
        return this;
    }

    /// <summary>Отправляет письмо по сохранённому шаблону.</summary>
    /// <param name="templateId">Идентификатор шаблона.</param>
    /// <param name="useDraft">Использовать черновик вместо опубликованной версии.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Template(string templateId, bool useDraft = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        _templateId = templateId;
        _useDraftTemplate = useDraft ? true : null;
        return this;
    }

    /// <summary>Отправляет письмо как A/B-тест.</summary>
    /// <param name="abTestId">Идентификатор A/B-теста.</param>
    /// <returns>Тот же построитель.</returns>
    /// <remarks>A/B-тесты поддерживают только письма с одним получателем.</remarks>
    public TransmissionBuilder AbTest(string abTestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(abTestId);
        _abTestId = abTestId;
        return this;
    }

    /// <summary>Отправляет готовое письмо в формате RFC822.</summary>
    /// <param name="rfc822">Содержимое письма.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder RawRfc822(string rfc822)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rfc822);
        _rfc822 = rfc822;
        return this;
    }

    /// <summary>Добавляет вложение.</summary>
    /// <param name="attachment">Вложение.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Attach(Attachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        _attachments.Add(attachment);
        return this;
    }

    /// <summary>Добавляет встроенное изображение.</summary>
    /// <param name="image">Изображение; на него ссылаются из HTML через <c>cid:</c> с его именем.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder InlineImage(Attachment image)
    {
        ArgumentNullException.ThrowIfNull(image);
        _inlineImages.Add(image);
        return this;
    }

    /// <summary>Задаёт адрес для ответа.</summary>
    /// <param name="replyTo">Адрес для ответа.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder ReplyTo(string replyTo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyTo);
        _replyTo = replyTo;
        return this;
    }

    /// <summary>Добавляет заголовок письма.</summary>
    /// <param name="name">Имя заголовка.</param>
    /// <param name="value">Значение заголовка.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Header(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        _headers ??= [];
        _headers[name] = value;
        return this;
    }

    /// <summary>Переопределяет поля сохранённого шаблона.</summary>
    /// <param name="contentOverride">Переопределяемые поля.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Override(ContentOverride contentOverride)
    {
        ArgumentNullException.ThrowIfNull(contentOverride);
        _override = contentOverride;
        return this;
    }

    /// <summary>Задаёт идентификатор кампании.</summary>
    /// <param name="campaignId">Идентификатор кампании.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder CampaignId(string campaignId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignId);
        _campaignId = campaignId;
        return this;
    }

    /// <summary>Задаёт описание письма.</summary>
    /// <param name="description">Описание.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Description(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        _description = description;
        return this;
    }

    /// <summary>Задаёт адрес возврата (envelope FROM).</summary>
    /// <param name="returnPath">Адрес возврата.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder ReturnPath(string returnPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(returnPath);
        _returnPath = returnPath;
        return this;
    }

    /// <summary>Задаёт домен отслеживания для оборачивания ссылок.</summary>
    /// <param name="trackingDomain">Подтверждённый домен отслеживания.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder TrackingDomain(string trackingDomain)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackingDomain);
        _trackingDomain = trackingDomain;
        return this;
    }

    /// <summary>
    /// Задаёт данные подстановки уровня письма из произвольного объекта.
    /// </summary>
    /// <param name="value">Объект с данными; имена свойств сохраняются как написаны.</param>
    /// <returns>Тот же построитель.</returns>
    /// <remarks>
    /// Использует рефлексию, поэтому в trimmed- и AOT-сборках недоступна.
    /// Для них есть перегрузка с <see cref="JsonTypeInfo{T}"/>.
    /// </remarks>
    [RequiresUnreferencedCode("Сериализация произвольного объекта использует рефлексию. Используйте перегрузку с JsonTypeInfo<T>.")]
    [RequiresDynamicCode("Сериализация произвольного объекта использует рефлексию. Используйте перегрузку с JsonTypeInfo<T>.")]
    public TransmissionBuilder SubstitutionData(object? value)
    {
        _substitutionData = JsonSerializer.SerializeToNode(value, UserDataOptions);
        return this;
    }

    /// <summary>
    /// Задаёт данные подстановки уровня письма. Перегрузка для trimmed- и AOT-сборок.
    /// </summary>
    /// <typeparam name="T">Тип данных.</typeparam>
    /// <param name="value">Данные.</param>
    /// <param name="typeInfo">Метаданные типа из source-gen контекста вызывающего.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder SubstitutionData<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        _substitutionData = JsonSerializer.SerializeToNode(value, typeInfo);
        return this;
    }

    /// <summary>
    /// Задаёт метаданные уровня письма из произвольного объекта.
    /// </summary>
    /// <param name="value">Объект с метаданными.</param>
    /// <returns>Тот же построитель.</returns>
    /// <remarks>
    /// Использует рефлексию, поэтому в trimmed- и AOT-сборках недоступна.
    /// Для них есть перегрузка с <see cref="JsonTypeInfo{T}"/>.
    /// </remarks>
    [RequiresUnreferencedCode("Сериализация произвольного объекта использует рефлексию. Используйте перегрузку с JsonTypeInfo<T>.")]
    [RequiresDynamicCode("Сериализация произвольного объекта использует рефлексию. Используйте перегрузку с JsonTypeInfo<T>.")]
    public TransmissionBuilder Metadata(object? value)
    {
        _metadata = JsonSerializer.SerializeToNode(value, UserDataOptions);
        return this;
    }

    /// <summary>
    /// Задаёт метаданные уровня письма. Перегрузка для trimmed- и AOT-сборок.
    /// </summary>
    /// <typeparam name="T">Тип метаданных.</typeparam>
    /// <param name="value">Метаданные.</param>
    /// <param name="typeInfo">Метаданные типа из source-gen контекста вызывающего.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Metadata<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        _metadata = JsonSerializer.SerializeToNode(value, typeInfo);
        return this;
    }

    /// <summary>Включает отправку через sandbox-домен.</summary>
    /// <param name="sandbox">Признак sandbox-отправки.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Sandbox(bool sandbox = true)
    {
        _options = _options with { Sandbox = sandbox };
        return this;
    }

    /// <summary>Помечает письмо транзакционным.</summary>
    /// <param name="transactional">Признак транзакционного письма.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder Transactional(bool transactional = true)
    {
        _options = _options with { Transactional = transactional };
        return this;
    }

    /// <summary>Управляет отслеживанием открытий.</summary>
    /// <param name="enabled">Включить отслеживание.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder OpenTracking(bool enabled)
    {
        _options = _options with { OpenTracking = enabled };
        return this;
    }

    /// <summary>Управляет отслеживанием переходов по ссылкам.</summary>
    /// <param name="enabled">Включить отслеживание.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder ClickTracking(bool enabled)
    {
        _options = _options with { ClickTracking = enabled };
        return this;
    }

    /// <summary>Задаёт пул IP-адресов для отправки.</summary>
    /// <param name="ipPool">Идентификатор пула.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder IpPool(string ipPool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ipPool);
        _options = _options with { IpPool = ipPool };
        return this;
    }

    /// <summary>Откладывает отправку до указанного момента.</summary>
    /// <param name="startTime">Время отправки; не далее трёх суток вперёд.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder StartTime(DateTimeOffset startTime)
    {
        _options = _options with { StartTime = startTime };
        return this;
    }

    /// <summary>Заменяет параметры отправки целиком.</summary>
    /// <param name="options">Параметры отправки.</param>
    /// <returns>Тот же построитель.</returns>
    public TransmissionBuilder WithOptions(TransmissionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        return this;
    }

    /// <summary>Собирает запрос.</summary>
    /// <returns>Готовый запрос на отправку.</returns>
    /// <exception cref="InvalidOperationException">
    /// Не заданы получатели или содержимое, либо содержимое задано двумя способами сразу.
    /// </exception>
    public TransmissionRequest Build()
    {
        var recipients = BuildRecipients();
        var content = BuildContent();

        return new TransmissionRequest
        {
            Content = content,
            Recipients = recipients,
            SubstitutionData = _substitutionData,
            Metadata = _metadata,
            Options = _options == new TransmissionOptions() ? null : _options,
            Override = _override,
            CampaignId = _campaignId,
            Description = _description,
            ReturnPath = _returnPath,
            TrackingDomain = _trackingDomain,
        };
    }

    private static string FormatAddress(Address address) =>
        string.IsNullOrEmpty(address.Name) ? address.Email : $"\"{address.Name}\" <{address.Email}>";

    private RecipientSet BuildRecipients()
    {
        if (_recipientListId is not null)
        {
            if (_recipients.Count > 0 || _cc.Count > 0 || _bcc.Count > 0)
            {
                throw new InvalidOperationException(
                    "Получатели заданы дважды: и сохранённым списком через RecipientList(), и явно через To()/Cc()/Bcc().");
            }

            return RecipientSet.StoredList(_recipientListId);
        }

        if (_recipients.Count == 0)
        {
            throw new InvalidOperationException("Не задан ни один получатель: вызовите To() или RecipientList().");
        }

        if (_cc.Count == 0 && _bcc.Count == 0)
        {
            return RecipientSet.Inline([.. _recipients]);
        }

        // Копии в SparkPost — это обычные получатели с подменённым заголовком To.
        var headerTo = string.Join(", ", _recipients.Select(recipient => FormatAddress(recipient.Address)));

        var all = new List<Recipient>(_recipients.Count + _cc.Count + _bcc.Count);
        all.AddRange(_recipients);
        all.AddRange(_cc.Concat(_bcc).Select(address => new Recipient
        {
            Address = address with { HeaderTo = headerTo },
        }));

        return RecipientSet.Inline(all);
    }

    private TransmissionContent BuildContent()
    {
        var hasInline = _html is not null || _text is not null || _ampHtml is not null;
        var forms = new List<string>(4);

        if (hasInline)
        {
            forms.Add("inline-содержимое");
        }

        if (_templateId is not null)
        {
            forms.Add("шаблон");
        }

        if (_abTestId is not null)
        {
            forms.Add("A/B-тест");
        }

        if (_rfc822 is not null)
        {
            forms.Add("RFC822");
        }

        if (forms.Count == 0)
        {
            throw new InvalidOperationException(
                "Не задано содержимое письма: вызовите Html()/Text(), Template(), AbTest() или RawRfc822().");
        }

        if (forms.Count > 1)
        {
            throw new InvalidOperationException(
                $"Содержимое задано несколькими способами сразу ({string.Join(" и ", forms)}), а допустим только один.");
        }

        if (hasInline && _from is null)
        {
            throw new InvalidOperationException("Не задан отправитель: вызовите From().");
        }

        var headers = _headers;

        if (_cc.Count > 0)
        {
            headers = headers is null ? [] : new Dictionary<string, string>(headers);
            headers["CC"] = string.Join(", ", _cc.Select(FormatAddress));
        }

        return new TransmissionContent
        {
            From = _from,
            Subject = _subject,
            Html = _html,
            Text = _text,
            AmpHtml = _ampHtml,
            ReplyTo = _replyTo,
            Headers = headers,
            Attachments = _attachments.Count > 0 ? [.. _attachments] : null,
            InlineImages = _inlineImages.Count > 0 ? [.. _inlineImages] : null,
            TemplateId = _templateId,
            UseDraftTemplate = _useDraftTemplate,
            AbTestId = _abTestId,
            EmailRfc822 = _rfc822,
        };
    }
}
