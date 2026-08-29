namespace SparkPoster.AspNetCore;

/// <summary>
/// Проверка подлинности вызова вебхука.
/// </summary>
/// <remarks>
/// У вебхуков SparkPost нет подписи, поэтому подлинность подтверждается только тем,
/// что настроено при создании вебхука. Задайте либо Basic-авторизацию, либо секретный
/// заголовок — и то же самое укажите в <see cref="WebhookRequest"/>.
/// </remarks>
public sealed class SparkPostWebhookOptions
{
    /// <summary>Имя пользователя для Basic-авторизации.</summary>
    public string? BasicAuthUsername { get; set; }

    /// <summary>Пароль для Basic-авторизации.</summary>
    public string? BasicAuthPassword { get; set; }

    /// <summary>
    /// Имя секретного заголовка, например <c>X-Webhook-Secret</c>.
    /// Должно совпадать с ключом в <see cref="WebhookRequest.CustomHeaders"/>.
    /// </summary>
    public string? SecretHeaderName { get; set; }

    /// <summary>Ожидаемое значение секретного заголовка.</summary>
    public string? SecretHeaderValue { get; set; }

    /// <summary>Настроена ли хоть какая-то проверка.</summary>
    internal bool HasAnyCheck =>
        BasicAuthUsername is not null || SecretHeaderName is not null;
}
