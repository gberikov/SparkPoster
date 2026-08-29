using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
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
/// Построитель ничего не отправляет: <see cref="Build"/> возвращает
/// <see cref="TransmissionRequest"/>, который затем передаётся в
/// <see cref="ITransmissions.SendAsync"/>. Готовый запрос можно сохранить, сериализовать
/// или переиспользовать через <c>with</c>.
/// </remarks>
public sealed class TransmissionBuilder
{
    /// <summary>
    /// Данные подстановки сериализуются без политики именования: имена переменных
    /// шаблона должны остаться ровно такими, как их написал вызывающий.
    /// </summary>
    private static readonly JsonSerializerOptions UserDataOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly List<Recipient> _recipients = [];
    private Dictionary<string, string>? _headers;
    private TransmissionOptions _options = new();
    private Address? _from;
    private string? _subject;
    private string? _html;
    private string? _text;
    private string? _ampHtml;
    private string? _replyTo;
    private string? _campaignId;
    private string? _description;
    private string? _returnPath;
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
    /// Не задан отправитель, получатели или содержимое письма.
    /// </exception>
    public TransmissionRequest Build()
    {
        if (_from is null)
        {
            throw new InvalidOperationException("Не задан отправитель: вызовите From().");
        }

        if (_recipients.Count == 0)
        {
            throw new InvalidOperationException("Не задан ни один получатель: вызовите To().");
        }

        if (_html is null && _text is null && _ampHtml is null)
        {
            throw new InvalidOperationException("Не задано содержимое письма: вызовите Html(), Text() или AmpHtml().");
        }

        return new TransmissionRequest
        {
            Content = new TransmissionContent
            {
                From = _from,
                Subject = _subject,
                Html = _html,
                Text = _text,
                AmpHtml = _ampHtml,
                ReplyTo = _replyTo,
                Headers = _headers,
            },
            Recipients = [.. _recipients],
            SubstitutionData = _substitutionData,
            Metadata = _metadata,
            Options = _options == new TransmissionOptions() ? null : _options,
            CampaignId = _campaignId,
            Description = _description,
            ReturnPath = _returnPath,
        };
    }
}
