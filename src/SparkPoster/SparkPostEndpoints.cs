namespace SparkPoster;

/// <summary>
/// Базовые адреса SparkPost. Аккаунты в US и EU независимы: ключ от одного не работает в другом.
/// </summary>
public static class SparkPostEndpoints
{
    /// <summary>Основной сервис SparkPost (US).</summary>
    public static Uri Us { get; } = new("https://api.sparkpost.com/api/v1/");

    /// <summary>SparkPost EU — тот же сервис, размещённый в Западной Европе.</summary>
    public static Uri Eu { get; } = new("https://api.eu.sparkpost.com/api/v1/");
}
