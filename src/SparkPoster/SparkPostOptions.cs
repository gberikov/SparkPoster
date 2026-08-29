namespace SparkPoster;

/// <summary>Настройки клиента SparkPost.</summary>
public sealed class SparkPostOptions
{
    /// <summary>
    /// API-ключ. Хранить его следует в переменных окружения или secret-хранилище —
    /// не в <c>appsettings.json</c> под контролем версий.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Базовый адрес API. По умолчанию <see cref="SparkPostEndpoints.Us"/>;
    /// для европейского аккаунта — <see cref="SparkPostEndpoints.Eu"/>.
    /// У Enterprise-аккаунтов бывает собственный адрес.
    /// </summary>
    public Uri BaseUrl { get; set; } = SparkPostEndpoints.Us;

    /// <summary>
    /// Субаккаунт по умолчанию для всех запросов клиента. Обычно не задаётся:
    /// точечно область действия удобнее менять через <see cref="ISparkPostClient.ForSubaccount"/>.
    /// </summary>
    public int? SubaccountId { get; set; }
}
