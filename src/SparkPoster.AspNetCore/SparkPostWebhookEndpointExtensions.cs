using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SparkPoster.AspNetCore;
using SparkPoster.Webhooks;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Приём вебхуков SparkPost.</summary>
public static class SparkPostWebhookEndpointExtensions
{
    /// <summary>
    /// Заводит эндпоинт, который принимает батчи событий SparkPost.
    /// </summary>
    /// <param name="endpoints">Маршруты приложения.</param>
    /// <param name="pattern">Путь эндпоинта.</param>
    /// <param name="handler">Обработчик батча.</param>
    /// <param name="options">Проверка подлинности вызова.</param>
    /// <returns>Построитель маршрута.</returns>
    /// <remarks>
    /// <para>
    /// Семантика ответов подчинена тому, как SparkPost повторяет доставку: успешная
    /// обработка отвечает 200, а исключение из <paramref name="handler"/> не подавляется
    /// и превращается в 500 — тогда SparkPost повторит батч. Повторы идут в течение
    /// 8 часов, после чего батч отбрасывается, поэтому глотать исключения здесь нельзя:
    /// это молча превратит доставку «хотя бы один раз» в «не более одного раза».
    /// </para>
    /// <para>
    /// Обработчик должен укладываться в 10 секунд — столько SparkPost ждёт ответа.
    /// Если обработка дольше, складывайте батч в очередь и разбирайте отдельно, но
    /// помните, что тогда за сохранность отвечаете вы, а не SparkPost.
    /// </para>
    /// <para>
    /// Батчи приходят без гарантии порядка и могут повторяться: отсекайте повторы
    /// по <see cref="SparkPostEventBatch.BatchId"/> или <see cref="SparkPostEvent.EventId"/>.
    /// </para>
    /// </remarks>
    public static IEndpointConventionBuilder MapSparkPostWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<SparkPostEventBatch, CancellationToken, Task> handler,
        SparkPostWebhookOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(handler);

        return endpoints.MapPost(pattern, async context =>
        {
            if (options is not null && !IsAuthorized(context.Request, options))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            var events = await SparkPostWebhookParser
                .ParseAsync(context.Request.Body, context.RequestAborted)
                .ConfigureAwait(false);

            var batch = new SparkPostEventBatch
            {
                BatchId = context.Request.Headers[SparkPostWebhookParser.BatchIdHeader],
                Events = events,
            };

            await handler(batch, context.RequestAborted).ConfigureAwait(false);

            context.Response.StatusCode = (int)HttpStatusCode.OK;
        });
    }

    private static bool IsAuthorized(HttpRequest request, SparkPostWebhookOptions options)
    {
        if (!options.HasAnyCheck)
        {
            return true;
        }

        if (options.SecretHeaderName is { } headerName)
        {
            return FixedTimeEquals(request.Headers[headerName], options.SecretHeaderValue);
        }

        var expected = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.BasicAuthUsername}:{options.BasicAuthPassword}"));

        var actual = request.Headers.Authorization.ToString();

        return actual.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)
            && FixedTimeEquals(actual["Basic ".Length..], expected);
    }

    /// <summary>
    /// Сравнение за постоянное время: обычное сравнение строк завершается на первом
    /// несовпавшем символе и тем самым выдаёт секрет по времени ответа.
    /// </summary>
    private static bool FixedTimeEquals(string? actual, string? expected)
    {
        if (actual is null || expected is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(actual),
            Encoding.UTF8.GetBytes(expected));
    }
}
