namespace SparkPoster.Internal;

/// <summary>Конверт ответа SparkPost: полезная нагрузка лежит в <c>results</c>.</summary>
internal sealed class SparkPostEnvelope<T>
{
    public T? Results { get; set; }
}

/// <summary>Конверт ошибки SparkPost.</summary>
internal sealed class SparkPostErrorEnvelope
{
    public List<SparkPostError>? Errors { get; set; }
}

/// <summary>Ответ на создание ресурса: интересен только идентификатор.</summary>
internal sealed class CreatedResource
{
    public string Id { get; set; } = string.Empty;
}
