namespace SparkPoster;

/// <summary>
/// Вложение или встроенное изображение.
/// </summary>
/// <remarks>
/// SparkPost принимает содержимое только как Base64 внутри JSON, поэтому потоковой
/// отправки здесь не существует в принципе: файл целиком оказывается в памяти,
/// причём в Base64 он примерно на треть больше исходного. Общий предел содержимого
/// письма — 20 МБ.
/// </remarks>
public sealed record Attachment
{
    /// <summary>
    /// Имя файла для <c>Content-Disposition</c>, а для встроенного изображения —
    /// значение <c>Content-ID</c>, по которому на него ссылается HTML. Не длиннее 255 байт.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// MIME-тип. Подставляется в заголовок <c>Content-Type</c> как есть,
    /// при необходимости вместе с параметром charset.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>Содержимое, закодированное в Base64, без переносов строк.</summary>
    public required string Data { get; init; }

    /// <summary>Создаёт вложение из массива байтов.</summary>
    /// <param name="name">Имя файла.</param>
    /// <param name="type">MIME-тип.</param>
    /// <param name="content">Содержимое.</param>
    /// <returns>Вложение.</returns>
    public static Attachment FromBytes(string name, string type, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        return new Attachment
        {
            Name = name,
            Type = type,
            Data = Convert.ToBase64String(content),
        };
    }

    /// <summary>Читает файл целиком и создаёт из него вложение.</summary>
    /// <param name="path">Путь к файлу.</param>
    /// <param name="type">MIME-тип.</param>
    /// <param name="name">Имя файла в письме. По умолчанию берётся из пути.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Вложение.</returns>
    public static async Task<Attachment> FromFileAsync(
        string path,
        string type,
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return FromBytes(name ?? Path.GetFileName(path), type, content);
    }

    /// <summary>Вычитывает поток целиком и создаёт из него вложение.</summary>
    /// <param name="stream">Поток с содержимым.</param>
    /// <param name="name">Имя файла.</param>
    /// <param name="type">MIME-тип.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Вложение.</returns>
    public static async Task<Attachment> FromStreamAsync(
        Stream stream,
        string name,
        string type,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        return FromBytes(name, type, buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }
}
