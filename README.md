# SparkPoster

A modern .NET client for the [SparkPost](https://developers.sparkpost.com/api/) REST API:
fluent transmission building, webhook receiving, async/await throughout, trimming- and
AOT-friendly.

> **Which API is this?** SparkPost is now Bird Email, and Bird ships a newer API at
> `platform.bird.com` with official SDKs for TypeScript, Go and Python. This library targets
> the **legacy SparkPost v1 API** at `api.sparkpost.com/api/v1`, which is what existing
> SparkPost accounts and keys work against. Neither API has an official .NET SDK.

## Install

```bash
dotnet add package SparkPoster
dotnet add package SparkPoster.Extensions.DependencyInjection   # ASP.NET Core / generic host
dotnet add package SparkPoster.AspNetCore                       # receiving webhooks
```

Targets `net8.0`, so it runs on .NET 8, 9 and 10.

## Send a message

```csharp
var client = new SparkPostClient(new SparkPostOptions { ApiKey = apiKey });

var transmission = Transmission.Create()
    .From("noreply@example.com", "Example")
    .To("user@example.com")
    .Cc("boss@example.com")
    .Subject("Hi {{name}}")
    .Html("<p>Hi {{name}}</p>")
    .SubstitutionData(new { name = "Bob" })
    .Transactional()
    .Build();

var result = await client.Transmissions.SendAsync(transmission, cancellationToken: ct);
// result.Id, result.TotalAcceptedRecipients, result.IsIdempotentReplay
```

That constructor is for console applications, scripts and tests: it uses one shared
`HttpClient` for the process. Inside a host, register the client instead — see below.

The builder never sends anything: `Build()` hands back a serializable `TransmissionRequest`,
so a message can be assembled now, queued, and sent later.

Content can be given in any one of the four forms SparkPost supports — inline, a stored
template, an A/B test, or raw RFC822 — and mixing them is caught in `Build()`:

```csharp
Transmission.Create().To("user@example.com").Template("welcome", useDraft: true).Build();
Transmission.Create().To("user@example.com").AbTest("subject-test").Build();
Transmission.Create().To("user@example.com").RawRfc822(mime).Build();
Transmission.Create().RecipientList("christmas-2026").Template("promo").Build();
```

Attachments are Base64 inside the JSON body — that is all SparkPost accepts, so there is no
streaming to be had, and the 20 MB content cap applies:

```csharp
.Attach(await Attachment.FromFileAsync("invoice.pdf", "application/pdf", cancellationToken: ct))
```

## Register it in DI

```csharp
builder.Services
    .AddSparkPost(builder.Configuration.GetSection("SparkPost"))
    .AddStandardResilienceHandler();               // Microsoft.Extensions.Http.Resilience

// or in code, when the key comes from somewhere configuration cannot reach:
builder.Services.AddSparkPost(options =>
{
    options.ApiKey = apiKey;
    options.BaseUrl = SparkPostEndpoints.Eu;       // defaults to the US service
});
```

A missing or empty `ApiKey` fails when the client is built, with a message saying so — not
later, as SparkPost's 401 to a request that carried an empty `Authorization` header.

`AddSparkPost` returns the `IHttpClientBuilder`, so retries, timeouts and circuit breaking
are configured with the standard Microsoft handler rather than a home-grown one.

**Retries are safe by construction.** Every send carries an `Idempotency-Key` header, generated
automatically unless you pass your own. A retry inside a `DelegatingHandler` replays the very
same request with the very same key, and SparkPost returns the original result instead of
sending a second message — `IsIdempotentReplay` tells the two apart. When your own code
retries a send, pass a key derived from a business identifier:

```csharp
await client.Transmissions.SendAsync(transmission, idempotencyKey: $"order-{orderId}", ct);
```

## Subaccounts

```csharp
await client.ForSubaccount(42).Transmissions.SendAsync(transmission, cancellationToken: ct);
```

Note that Metrics and Events ignore the subaccount header; they filter through the
`subaccounts` query parameter instead, which `EventQuery.Subaccounts` exposes.

## Webhooks

Create and manage them:

```csharp
var id = await client.Webhooks.CreateAsync(
    new WebhookRequest
    {
        Name = "Delivery events",
        Target = "https://app.example.com/hooks/sparkpost",
        Events = [SparkPostEventTypes.Delivery, SparkPostEventTypes.Bounce],
        AuthType = WebhookAuthType.Basic,
        AuthCredentials = new WebhookAuthCredentials { Username = "hook", Password = secret },
    },
    ct);
```

Receive them:

```csharp
app.MapSparkPostWebhook(
    "/hooks/sparkpost",
    async (batch, ct) =>
    {
        foreach (var @event in batch.Events)
        {
            if (@event is MessageEvent { Type: SparkPostEventTypes.Bounce } bounce)
            {
                await suppress.RecordAsync(bounce.RcptTo!, bounce.Reason, ct);
            }
        }
    },
    new SparkPostWebhookOptions { BasicAuthUsername = "hook", BasicAuthPassword = secret });
```

The options argument is **required**. SparkPost webhooks carry no signature, so an endpoint
with nothing configured would accept forged events from anyone who learns its URL; a half-filled
pair — a header name without its value — throws at startup rather than quietly letting everyone
through. If a gateway in front already does the checking, say so with `AllowAnonymous = true`.

Outside ASP.NET Core, `SparkPostWebhookParser.ParseAsync(stream, ct)` does the parsing on its
own.

Three things about webhook delivery that the design of this API forces on you:

- **Batches repeat.** Delivery is at-least-once and unordered, and a batch that does not get a
  200 is retried for 8 hours. Deduplicate on `batch.BatchId` or `event.EventId`.
- **Exceptions are not swallowed.** A handler that throws produces a 500, which is what makes
  SparkPost resend. Catching everything and answering 200 silently turns at-least-once delivery
  into at-most-once.
- **Ten seconds.** That is how long SparkPost waits for your response. If processing takes
  longer, queue the batch — but then its safekeeping is yours, not SparkPost's.

## Events

Two ways to read them, because two different jobs need them:

```csharp
// Walk everything; pages are fetched lazily as you go.
await foreach (var @event in client.Events.SearchAsync(new EventQuery { Campaigns = ["blackfriday"] }, ct))
{
    Console.WriteLine($"{@event.Timestamp:u} {@event.Type} {@event.RcptTo}");
}

// Or drive the cursor yourself, when it has to survive a restart.
var page = await client.Events.GetPageAsync(query, cursor: savedCursor, ct);
await checkpoints.SaveAsync(page.NextCursor, ct);
```

Event data is retained for 10 days.

## Templates, suppression list, sending domains

```csharp
await client.Templates.CreateAsync(new TemplateRequest { Id = "welcome", Content = content }, ct);
await client.Templates.PublishAsync("welcome", ct);           // draft -> published

await client.SuppressionList.UpsertAsync(
    new SuppressionEntry { Recipient = "user@example.com", Type = SuppressionTypes.NonTransactional },
    ct);

var domain = await client.SendingDomains.CreateAsync(new SendingDomainRequest { Domain = "example.com" }, ct);
// publish domain.Dkim in DNS, then:
var status = await client.SendingDomains.VerifyAsync("example.com", cancellationToken: ct);
```

## Errors

Everything non-2xx becomes a `SparkPostApiException` carrying `StatusCode`, the parsed
`Errors` and the raw body. Rate limiting (429) and the sending limit (420) come back as
`SparkPostRateLimitException`, which adds `RetryAfter`.

```csharp
catch (SparkPostRateLimitException e) { await Task.Delay(e.RetryAfter ?? TimeSpan.FromSeconds(5), ct); }
catch (SparkPostApiException e) when (e.StatusCode == HttpStatusCode.UnprocessableEntity)
{
    logger.LogWarning("SparkPost rejected the message: {Reason}", e.Errors.FirstOrDefault()?.Description);
}
```

## Security

- **SparkPost webhooks carry no signature.** Authenticity rests entirely on what you configured
  when creating the webhook. Serve the endpoint over HTTPS; `MapSparkPostWebhook` refuses to
  start without either Basic authentication or a secret header, and compares them in constant
  time.
- **Keep the API key out of configuration files.** Environment variables or a secret store; the
  library never logs it, and never puts it in an exception message.
- **Secrets are masked in `ToString()`.** A record normally prints every property, so
  `WebhookAuthCredentials`, `WebhookAuthRequestDetails` and `Attachment` override that — a
  webhook read back from the API can otherwise carry its own password into your logs.
- **`SparkPostApiException.RawBody` can hold personal data** — validation errors echo recipient
  addresses back. Think before dumping it into logs.

## What is covered

| Section | Status |
|---|---|
| Transmissions | Send (all four content forms), attachments, inline images, CC/BCC, scheduling, stored recipient lists, cancel by campaign |
| Event webhooks | CRUD, validate, batch status, event documentation and samples |
| Webhook receiving | Typed events for all five categories, unknown types preserved, ASP.NET Core endpoint |
| Events | Cursor paging and lazy enumeration, the full documented filter set |
| Templates | CRUD, drafts and publishing, preview |
| Suppression list | Upsert, bulk upsert, search, delete, summary |
| Sending domains | CRUD and verification |
| Metrics, A/B testing, snippets, recipient lists, subaccounts, API keys, IP pools, sending IPs, inbound domains, relay webhooks, tracking domains, DKIM keys, data privacy | Not yet |

Unknown fields are never dropped: every event exposes them through `Extra`, and unknown event
types arrive as `UnknownSparkPostEvent` rather than breaking the batch.

## Requirements

.NET 8 or later. No dependencies in the core package; the DI package adds
`Microsoft.Extensions.Http`.

## License

MIT.
