using System.Text;

namespace SparkPoster;

/// <summary>
/// An attachment or an inline image.
/// </summary>
/// <remarks>
/// SparkPost only accepts content as Base64 inside JSON, so streaming simply does not
/// exist here: the whole file ends up in memory, and Base64 makes it roughly a third
/// larger. The message content as a whole is capped at 20 MB.
/// </remarks>
public sealed record Attachment
{
    /// <summary>
    /// The file name for <c>Content-Disposition</c>; for an inline image, the
    /// <c>Content-ID</c> the HTML refers to. At most 255 bytes.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The MIME type. Applied to the <c>Content-Type</c> header as-is, including a charset
    /// parameter when one is needed.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>The content, Base64 encoded, without line breaks.</summary>
    public required string Data { get; init; }

    /// <summary>Creates an attachment from a byte array.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="type">The MIME type.</param>
    /// <param name="content">The content.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="type"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="type"/> is empty or whitespace.</exception>
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

    /// <summary>Reads a file in full and turns it into an attachment.</summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="type">The MIME type.</param>
    /// <param name="name">The file name to use in the message. Defaults to the name from the path.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="type"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> or <paramref name="type"/> is empty or whitespace.</exception>
    /// <exception cref="FileNotFoundException">There is no file at <paramref name="path"/>.</exception>
    /// <exception cref="DirectoryNotFoundException">Part of <paramref name="path"/> does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    /// <exception cref="IOException">Reading the file failed.</exception>
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

    /// <summary>Drains a stream in full and turns it into an attachment.</summary>
    /// <param name="stream">The stream holding the content.</param>
    /// <param name="name">The file name.</param>
    /// <param name="type">The MIME type.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="stream"/>, <paramref name="name"/> or <paramref name="type"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> or <paramref name="type"/> is empty or whitespace.</exception>
    /// <exception cref="IOException">Reading the stream failed.</exception>
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

    /// <summary>
    /// The generated record <c>ToString()</c> would print the whole Base64 payload — megabytes
    /// of it, and whatever personal data the file holds. Only its size is printed.
    /// </summary>
    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Name = ").Append(Name)
            .Append(", Type = ").Append(Type)
            .Append(", Data = ").Append(Data.Length).Append(" base64 chars");

        return true;
    }
}
