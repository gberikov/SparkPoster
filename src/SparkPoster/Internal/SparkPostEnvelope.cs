namespace SparkPoster.Internal;

/// <summary>The SparkPost response envelope: the payload lives under <c>results</c>.</summary>
internal sealed class SparkPostEnvelope<T>
{
    public T? Results { get; set; }
}

/// <summary>The SparkPost error envelope.</summary>
internal sealed class SparkPostErrorEnvelope
{
    public List<SparkPostError>? Errors { get; set; }
}

/// <summary>A creation response where only the identifier matters.</summary>
internal sealed class CreatedResource
{
    public string Id { get; set; } = string.Empty;
}
