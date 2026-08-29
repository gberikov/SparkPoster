using System.Net;

namespace SparkPoster;

/// <summary>Базовое исключение библиотеки.</summary>
public class SparkPostException : Exception
{
    /// <summary>Создаёт исключение с сообщением.</summary>
    /// <param name="message">Текст сообщения.</param>
    public SparkPostException(string message) : base(message)
    {
    }

    /// <summary>Создаёт исключение с сообщением и внутренней ошибкой.</summary>
    /// <param name="message">Текст сообщения.</param>
    /// <param name="innerException">Внутреннее исключение.</param>
    public SparkPostException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Ответ SparkPost с кодом ошибки. Различать частные случаи следует по <see cref="StatusCode"/>.
/// </summary>
public class SparkPostApiException : SparkPostException
{
    /// <summary>Создаёт исключение по ответу API.</summary>
    /// <param name="statusCode">HTTP-код ответа.</param>
    /// <param name="errors">Разобранные ошибки из тела ответа.</param>
    /// <param name="rawBody">Тело ответа как есть.</param>
    public SparkPostApiException(HttpStatusCode statusCode, IReadOnlyList<SparkPostError> errors, string? rawBody)
        : base(BuildMessage(statusCode, errors))
    {
        StatusCode = statusCode;
        Errors = errors;
        RawBody = rawBody;
    }

    /// <summary>HTTP-код ответа.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Ошибки из тела ответа. Пустой список, если тело не было разобрано.</summary>
    public IReadOnlyList<SparkPostError> Errors { get; }

    /// <summary>
    /// Тело ответа как есть — пригодится, когда сервер вернул не JSON (заглушка прокси, HTML-страница).
    /// </summary>
    /// <remarks>
    /// Может содержать персональные данные: в ошибках валидации SparkPost повторяет адреса получателей.
    /// Не выгружайте это поле в логи не подумав.
    /// </remarks>
    public string? RawBody { get; }

    private static string BuildMessage(HttpStatusCode statusCode, IReadOnlyList<SparkPostError> errors)
    {
        var first = errors.Count > 0 ? errors[0] : null;
        var detail = first?.Description ?? first?.Message;

        return detail is null
            ? $"SparkPost вернул {(int)statusCode} {statusCode}."
            : $"SparkPost вернул {(int)statusCode} {statusCode}: {detail}";
    }
}

/// <summary>
/// Превышен лимит запросов (429) или лимит отправки (420).
/// </summary>
/// <remarks>
/// Выделено в отдельный тип ради <see cref="RetryAfter"/>: этих данных нет больше нигде,
/// и именно на них опираются политики повторов.
/// </remarks>
public sealed class SparkPostRateLimitException : SparkPostApiException
{
    /// <summary>Создаёт исключение по ответу API.</summary>
    /// <param name="statusCode">HTTP-код ответа (429 или 420).</param>
    /// <param name="errors">Разобранные ошибки из тела ответа.</param>
    /// <param name="rawBody">Тело ответа как есть.</param>
    /// <param name="retryAfter">Значение заголовка <c>Retry-After</c>, если он был.</param>
    public SparkPostRateLimitException(
        HttpStatusCode statusCode,
        IReadOnlyList<SparkPostError> errors,
        string? rawBody,
        TimeSpan? retryAfter)
        : base(statusCode, errors, rawBody)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>Сколько ждать до следующей попытки, если сервер это сообщил.</summary>
    public TimeSpan? RetryAfter { get; }
}
