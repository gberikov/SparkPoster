using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SparkPoster.AspNetCore;
using SparkPoster.Webhooks;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Receiving SparkPost webhooks.</summary>
public static class SparkPostWebhookEndpointExtensions
{
    /// <summary>
    /// Maps an endpoint that accepts SparkPost event batches.
    /// </summary>
    /// <param name="endpoints">The application's endpoint routes.</param>
    /// <param name="pattern">The endpoint path.</param>
    /// <param name="handler">The batch handler.</param>
    /// <param name="options">
    /// How to prove the call genuine. Required: SparkPost webhooks carry no signature, so an
    /// endpoint with nothing configured accepts forged events from anyone who learns its URL.
    /// Set <see cref="SparkPostWebhookOptions.AllowAnonymous"/> to say that out loud.
    /// </param>
    /// <returns>The endpoint convention builder.</returns>
    /// <remarks>
    /// <para>
    /// The response semantics follow how SparkPost retries: a successful handler answers 200,
    /// while an exception from <paramref name="handler"/> is deliberately not swallowed and
    /// surfaces as a 500 — which makes SparkPost resend the batch. Retries run for 8 hours,
    /// after which the batch is discarded, so swallowing exceptions here is not an option:
    /// it silently turns at-least-once delivery into at-most-once.
    /// </para>
    /// <para>
    /// The handler has to finish within 10 seconds, which is how long SparkPost waits for the
    /// response. If your processing takes longer, queue the batch and handle it separately —
    /// but note that from then on its safekeeping is your responsibility, not SparkPost's.
    /// </para>
    /// <para>
    /// Batches arrive unordered and may repeat: deduplicate on
    /// <see cref="SparkPostEventBatch.BatchId"/> or <see cref="SparkPostEvent.EventId"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="endpoints"/>, <paramref name="handler"/> or <paramref name="options"/>
    /// is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="options"/> configures no check, configures one only half way — a
    /// header name without its value, a user name without its password — or configures more
    /// than one: both the secret header and Basic authentication, or a check next to
    /// <see cref="SparkPostWebhookOptions.AllowAnonymous"/>.
    /// </exception>
    public static IEndpointConventionBuilder MapSparkPostWebhook(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<SparkPostEventBatch, CancellationToken, Task> handler,
        SparkPostWebhookOptions options)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        return endpoints.MapPost(pattern, async context =>
        {
            if (!IsAuthorized(context.Request, options))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            IReadOnlyList<SparkPostEvent> events;

            try
            {
                events = await SparkPostWebhookParser
                    .ParseAsync(context.Request.Body, context.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (JsonException)
            {
                // A body that will never parse is answered 400 rather than 500, so the logs tell
                // "they sent garbage" apart from "my handler threw". SparkPost retries either way.
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

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
        if (options.SecretHeaderName is { } headerName)
        {
            return FixedTimeEquals(request.Headers[headerName], options.SecretHeaderValue);
        }

        if (options.BasicAuthUsername is not null)
        {
            var expected = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{options.BasicAuthUsername}:{options.BasicAuthPassword}"));

            var actual = request.Headers.Authorization.ToString();

            return actual.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)
                && FixedTimeEquals(actual["Basic ".Length..], expected);
        }

        return options.AllowAnonymous;
    }

    /// <summary>
    /// A constant-time comparison: ordinary string comparison stops at the first mismatching
    /// character and thereby leaks the secret through response timing.
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
