namespace SparkPoster;

/// <summary>
/// Одна ошибка из тела ответа SparkPost.
/// </summary>
/// <remarks>
/// Полезное обычно лежит в <see cref="Description"/> («content object or template_id required»),
/// тогда как <see cref="Message"/> общий («required field is missing»).
/// </remarks>
public sealed record SparkPostError
{
    /// <summary>Краткая формулировка ошибки.</summary>
    public string? Message { get; init; }

    /// <summary>Подробное описание — как правило, самое информативное поле ответа.</summary>
    public string? Description { get; init; }

    /// <summary>Код ошибки SparkPost (например, <c>1600</c> — повторное использование ключа идемпотентности).</summary>
    public string? Code { get; init; }
}
