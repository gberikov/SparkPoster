using System.Text.Json.Nodes;
using SparkPoster.Internal;

namespace SparkPoster.Webhooks;

/// <summary>
/// Разбор батча событий, который SparkPost присылает на ваш эндпоинт.
/// </summary>
/// <remarks>
/// <para>
/// У вебхуков SparkPost <b>нет подписи</b>: подлинность вызова подтверждается только тем,
/// что вы настроили при создании вебхука — Basic-авторизацией, OAuth или секретным
/// заголовком. Эндпоинт обязан работать по HTTPS и проверять этот секрет, иначе кто угодно
/// сможет присылать вам поддельные события об отбойниках и отписках.
/// </para>
/// <para>
/// Доставка «хотя бы один раз» и без гарантии порядка: батч без ответа 200 повторяется
/// в течение 8 часов. Защищаться от повторов следует по
/// <see cref="SparkPostEventBatch.BatchId"/> или по <see cref="SparkPostEvent.EventId"/>.
/// </para>
/// </remarks>
public static class SparkPostWebhookParser
{
    /// <summary>Имя заголовка, в котором приходит идентификатор батча.</summary>
    public const string BatchIdHeader = "X-MessageSystems-Batch-ID";

    /// <summary>Разбирает батч из строки.</summary>
    /// <param name="json">Тело запроса.</param>
    /// <returns>События батча.</returns>
    /// <exception cref="System.Text.Json.JsonException">Тело не является корректным JSON.</exception>
    public static IReadOnlyList<SparkPostEvent> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return SparkPostEventReader.Read(JsonNode.Parse(json));
    }

    /// <summary>Разбирает батч из потока.</summary>
    /// <param name="stream">Тело запроса.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>События батча.</returns>
    /// <exception cref="System.Text.Json.JsonException">Тело не является корректным JSON.</exception>
    public static async Task<IReadOnlyList<SparkPostEvent>> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        return SparkPostEventReader.Read(node);
    }
}

/// <summary>Батч событий вместе с его идентификатором.</summary>
public sealed record SparkPostEventBatch
{
    /// <summary>
    /// Идентификатор батча из заголовка <see cref="SparkPostWebhookParser.BatchIdHeader"/>.
    /// По нему отсекаются повторные доставки одного и того же батча.
    /// </summary>
    public string? BatchId { get; init; }

    /// <summary>События батча.</summary>
    public required IReadOnlyList<SparkPostEvent> Events { get; init; }
}
