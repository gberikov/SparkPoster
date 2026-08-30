# SparkPoster — design decisions

A modern .NET library for the SparkPost REST API: fluent transmissions, webhooks, async/await,
maximum API coverage.

This document records the decisions made during the design interview, together with their
rationale — so that six months from now they are not argued all over again.

## 0. Context you need before reading

**SparkPost became Bird Email.** Alongside the legacy API (`https://api.sparkpost.com/api/v1`)
there is a new Bird API: host `https://us1.platform.bird.com/v1`, `Authorization: Bearer bk_us1_…`,
`POST /v1/email/messages` instead of `/transmissions`, a flat payload, `202 Accepted`, prefixed IDs
(`em_`, `dom_`, `whk_`, `sup_`), `PATCH` instead of `PUT`, cursor pagination, an error envelope
`{type, code, message, request_id}`, webhook signatures per Standard Webhooks (`webhook-id`,
`webhook-timestamp`, `webhook-signature`). Official SDKs: TypeScript, Go, Python + a CLI. **No .NET.**

**No shutdown date for the legacy API has been announced publicly** (we looked — found nothing).

**Decision: we build against legacy SparkPost v1**, because that is the only place a working
account and key exist. No "SparkPost or Bird behind one facade" abstraction layer: if Bird is ever
needed, it will be a different library, not an `if` inside this one.

## 1. Decisions

| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Audience | OSS: public repository, packages on nuget.org | "Later" arrived together with the first consumer (a downstream project). The publishing scaffolding was not paid for up front — it went in when publication became real |
| 2 | TFM | `net8.0` only | Runs for consumers on net8/9/10, zero `#if` |
| 3 | Packages | `SparkPoster` + `.Extensions.DependencyInjection` + `.AspNetCore` | Maintainer's call |
| 4 | v1.0 scope | Transmissions, Event Webhooks (CRUD + receiving), Events, Templates, Suppression List, Sending Domains | Infrastructure is cheap to change across three sections and expensive across eighteen |
| 5 | API shape | A `SparkPostClient` facade (+ `ISparkPostClient`) with resource properties behind interfaces; only the facade is registered in DI | 18 registrations are a tax on every application; the interfaces pay for themselves with the consumer's first unit test |
| 6 | Fluent | The builder is independent of the client, `Build()` → object, sending is a separate call | The builder is a pure function: testable without HTTP, can be put on a queue |
| 7a | Model | `TransmissionRequest` is a public record with `init` | `internal` is a dead end: the builder lagging one field behind the API forces a fork of the library |
| 7b | Builder | Mutable, returns `this`, not thread-safe (stated in the XML doc) | The "template + different recipients" scenario is solved by `tx with { … }` for free |
| 8 | Validation | Structural invariants only: null arguments + three `required` fields in `Build()` | A mirror of server rules goes stale and lies; an email regex breaks legitimate addresses |
| 9a | Errors | Exceptions. `SparkPostException` → `SparkPostApiException` (`StatusCode`, `Errors`, `RawBody`); a separate `SparkPostRateLimitException` with `RetryAfter` (429/420) | A deep hierarchy is 6 classes for the sake of `catch … when (e.StatusCode == 401)` |
| 9b | 404 | Throws, like everything else | `null` from some methods and an exception from others is a source of silent bugs |
| 10 | Target API | Legacy SparkPost v1 | The only place with a working key |
| 11a | Retries | None of our own; `AddSparkPost()` returns `IHttpClientBuilder`, the consumer attaches `.AddStandardResilienceHandler()` | Backoff, jitter and circuit breaker are already written and tested in Microsoft's package |
| 11b | Idempotency | `Idempotency-Key` is generated automatically on every `SendAsync` unless set explicitly | A `DelegatingHandler` replays the **same** `HttpRequestMessage` → an external retry becomes safe without a single setting. Without the key, a resilience handler sends the message twice on a 5xx |
| 12a | Events | A typed hierarchy + `UnknownSparkPostEvent` + `JsonExtensionData`; an unknown type **never throws** | A new event type must not take the endpoint down: SparkPost starts retrying the whole batch, including what was already processed |
| 12b | AspNetCore | `app.MapSparkPostWebhook(path, handler, options)` + basic-auth / secret-header check; `options` is required (see §8) | An exception from the handler → 500 (SparkPost retries), success → 200. Buffering through a `Channel` by default silently turns at-least-once into at-most-once |
| 13a | JSON | Source-generated `JsonSerializerContext`, `IsAotCompatible` | Cheap to do up front, expensive to bolt on later. A custom converter for events is needed either way: the discriminator sits in the name of the outer property, `msys.message_event` |
| 13b | Enums | `enum` where we invent the value; `string` + `const` constants where the server does | A strict enum fails on the first new value and takes the whole batch down |
| 14 | Pagination | Both `GetPageAsync(query, cursor, ct)` and `IAsyncEnumerable` on top of it | The production "resume from a checkpoint" scenario needs an explicit cursor; the wrapper is ~15 stateless lines |
| 15a | Host | A single `Uri BaseUrl` property + `SparkPostEndpoints.Us/.Eu` constants | Two ways to set the same thing = a bug report "set Eu, sends to US" |
| 15b | Subaccounts | `client.ForSubaccount(42)` — a wrapper over the same `HttpClient` | The header goes only on the `HttpRequestMessage`, never into `DefaultRequestHeaders` |
| 16a | Tests | xUnit v3 + bare `Assert` + our own `FakeHttpMessageHandler` | FluentAssertions is paid for commercial use since v8 |
| 16b | Contract reference | A golden request JSON (catches our serialization) **and** response fixtures from the docs (catch our deserialization) | Different failure modes: we control the input, SparkPost controls the output |
| 17 | Layout | `src/` (3 projects) + `tests/` (1), `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` | `TreatWarningsAsErrors` + `GenerateDocumentationFile`: a missing XML doc breaks the build |
| 18a | substitution_data | `object?` with `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` **and** an overload taking `JsonTypeInfo<T>` | That is how ASP.NET Core itself solves it: convenient by default, AOT-clean on demand |
| 18b | Attachments | `byte[]` + `Attachment.FromFile/FromStream` | SparkPost accepts only base64 inside JSON — streaming upload does not exist at all |
| 19 | Observability | Nothing of our own; `ActivitySource` goes to the backlog | `IHttpClientFactory` and the OTel `HttpClient` instrumentation already give logs and spans |
| 20 | Content forms | One builder, mutually exclusive methods, checked in `Build()` | The type-safe variant needs a recursive generic `Builder<TSelf>` in public signatures |
| 21a | Namespace | Flat in `SparkPoster`; events in `SparkPoster.Webhooks` | `TransmissionRequest` and `SparkPostClient` live on the same line of code |
| 21b | Async | `Async` suffix, no synchronous wrappers, `CancellationToken` last with `= default`, `ConfigureAwait(false)` everywhere with `CA2007` enabled | The client is thread-safe and meant to be a singleton |
| 22 | License / versions | MIT, MinVer (version from git tags) | `LICENSE` is in the first commit. Tags without a prefix: `0.1.0`. `0.x` = the right to break the API |
| 23 | Order of work | The thinnest possible vertical slice end to end | Infrastructure decisions are only validated by an end-to-end scenario |

## 2. Security — not up for debate

- The API key never appears in exception messages or in the options' `ToString()`.
- `RawBody` in an exception may contain PII (recipient addresses in validation errors) — the XML
  doc says outright that it must not be dumped into logs blindly.
- Legacy SparkPost webhooks **have no HMAC signature**. Authenticity rests solely on what was
  configured when the webhook was created (basic auth, OAuth2 or custom headers). README: the
  endpoint must sit behind HTTPS and behind basic auth or a secret header, otherwise anyone can
  send fake `bounce` events.
- Secrets are compared with `CryptographicOperations.FixedTimeEquals`.

## 3. Public API sketch

```csharp
// sending
var tx = Transmission.Create()
    .From("noreply@example.com", "Example")
    .To("user@example.com")
    .Subject("Hi {{name}}")
    .Html("<p>Hi {{name}}</p>")
    .SubstitutionData(new { name = "Bob" })
    .Build();

var result = await client.Transmissions.SendAsync(tx, ct);
// result.Id, result.TotalAcceptedRecipients, result.IsIdempotentReplay

// subaccount
await client.ForSubaccount(42).Transmissions.SendAsync(tx, ct);

// events: a page with an explicit cursor — or transparent enumeration
var page = await client.Events.GetPageAsync(query, cursor, ct);
await foreach (var e in client.Events.SearchAsync(query, ct)) { }

// receiving webhooks
app.MapSparkPostWebhook("/hooks/sparkpost", async (batch, ct) => { },
    new SparkPostWebhookOptions
    {
        SecretHeaderName = "X-Webhook-Secret",
        SecretHeaderValue = secret
    });
```

## 4. Order of work

The v1.0 scope from decision #4 is fully closed: steps 1–7 are done, 93 tests green.
Each step was its own feature branch, merged into `develop` with `--no-ff`.

1. **Skeleton**: `global.json`, `Directory.Build.props`, `Directory.Packages.props`,
   `src/Directory.Build.props`, `.editorconfig`, `LICENSE`, three projects in `src/`.
   No version tag: under git flow it appears on `master` at release time via `release/*`;
   until then MinVer produces `0.0.0-alpha.0.N`.
2. **Vertical slice**: `Transmission.Create()…Build()` → `Transmissions.SendAsync()` →
   `TransmissionResponse`. A test "the chain produces exactly this JSON" (body from the
   documentation) + a test mapping a 422 to `SparkPostApiException` with the `description` inside.
   `tests/Directory.Build.props` and the test project are created here — together with the first
   real test, not as empty scaffolding with a placeholder.
3. The remaining content forms: template, A/B, RFC822; attachments, scheduling, cancellation by
   `campaign_id`.
4. Event Webhooks: CRUD + validate + batch status.
5. Receiving webhooks: event models, the converter, `MapSparkPostWebhook`.
6. Events: `GetPageAsync` + `IAsyncEnumerable`.
7. Templates → Suppression List → Sending Domains.
8. **Publishing**: package metadata (README, Source Link, `.snupkg`), CI on GitHub Actions
   (tests on push/PR, publishing on tag via Trusted Publishing — no long-lived key in secrets),
   rulesets on `master` and tags. The first release is `0.1.0`, not `1.0.0`: decision #22
   reserves the right to break the API on `0.x`, and the first consumer's migration will almost
   certainly use it.

## 5. What came up along the way

Closed:

- **Time format.** In webhooks `timestamp` is Unix epoch seconds as a string; in the Events API
  it is ISO 8601 with milliseconds. One converter accepts both forms plus a number; both cases
  are under test.
- **Replay header.** In the legacy API it is `X-Idempotent-Replayed`; `Idempotency-Replay` only
  showed up in the Bird documentation. Both are read — it costs nothing.
- **Numbers as strings.** SparkPost returns the same numeric fields sometimes as numbers,
  sometimes as strings (`response_code` in the batch status), while `code` in an error goes the
  other way — sometimes a string, sometimes a number. Fixed by `AllowReadingFromString` in the
  context and a dedicated converter for `code`. Without the latter, parsing the error body failed
  silently — surfaced by a test.
- **Shape of `links`.** Cursor pagination comes with either an object with `next` or an array of
  `rel`/`href`. Both are read.
- **`start_time` format.** Documented as `YYYY-MM-DDTHH:MM:SS+-HH:MM` — whole seconds with an
  offset. A plain `DateTimeOffset` serializes with a fractional part (`DateTimeOffset.UtcNow`
  always has one), so the property carries a dedicated converter: it drops the fraction of a
  second (drops, not rounds) and preserves the caller's offset. Not yet verified against a live
  account, but the library no longer gives the server a reason to reject the request.

Still to verify against a live account:

- The full list of fields per event category — take it from `GET /webhooks/events/documentation`
  and `GET /webhooks/events/samples`; only what is actually used is typed, the rest lands in `Extra`.
- The behaviour of 409 with codes `1600` (same key, different body — caller's error) and
  `1601` (request still in flight — retryable).

## 6. Deliberately deferred

| What | When to add |
|------|-------------|
| `samples/` with compilable examples | When README has more than 3–4 examples |
| Public API approval tests (PublicApiGenerator) | Together with the `1.0.0` tag. On `0.x` the right to break the API is reserved deliberately and the first consumer uses it — until stabilization these tests only produce noise on every commit |
| Package icon | When a real PNG exists; until then nuget.org draws a placeholder |
| Multi-targeting `net8.0;net10.0` | When a net10-only API or a measured performance difference appears |
| `netstandard2.0` / .NET Framework | When a concrete consumer on Framework appears |
| `ActivitySource` with spans (`sparkpost.transmission_id`, number of accepted recipients) | After the library works; does not change the public API |
| Our own retry handler | If `AddStandardResilienceHandler` turns out to be insufficient |
| Metrics, A/B Testing, Snippets (`/api/labs`), Recipient Lists, Subaccounts, API Keys, IP Pools, Sending IPs, Inbound Domains, Relay Webhooks, Tracking Domains, DKIM Keys, Data Privacy | One section at a time after v1.0. Metrics and A/B Testing last: a huge parameter surface with low demand |
| Webhook buffering through a `Channel` | Never by default — it is the consumer's deliberate choice |

**Not doing:** an abstraction over SparkPost and Bird at once; a full mirror of server-side
validation; a deep exception hierarchy; silently translating `X-MSYS-SUBACCOUNT` into the
`subaccounts` query parameter for Metrics/Events.

## 7. Environment constraints

- The sandbox domain `sparkpostbox.com` allows **5 messages for the lifetime of the account**.
  A real send is a single manual smoke test, never in CI.
- There is no integration test project yet. When one appears, it runs only against non-sending
  endpoints and is skipped entirely without the `SPARKPOST_API_KEY` variable.

## 8. Review before 0.2.0

Found and closed. Everything except the last item is a fix to code already written,
not a new decision.

| What | Why |
|------|-----|
| `MapSparkPostWebhook` requires `options`, a half-filled pair fails at startup, explicit `AllowAnonymous` | It used to be `options = null` → a receiver with no checks by default, and `HasAnyCheck` looked only at `SecretHeaderName` and `BasicAuthUsername`: a config with `SecretHeaderValue` filled in and the name forgotten **silently** let everyone through. That is exactly the failure nobody notices until fake `bounce`s are already in the database. Breaks the API — decision #22 allows that on `0.x` |
| `PrintMembers` on `WebhookAuthCredentials`, `WebhookAuthRequestDetails`, `DkimSettings`, `Attachment` | A record's generated `ToString()` prints every property. A `Webhook` read via `GetAsync` carried its own password and `access_token` into the logs, and `Body` (a `JsonNode`, which prints JSON, unlike a dictionary) — the `client_secret`. §2 forbids this for the API key; same class of problem, different secret. `DkimSettings` prints the private DKIM key the user supplied — a secret of the same class. `Attachment` is not a secret, but megabytes of base64 in a log |
| `ApiKey` check in the `SparkPostClient` constructor | An empty key went out as an empty header and came back as an unhelpful 401. Checked in the constructor rather than via `ValidateOnStart`: the latter lives in `Microsoft.Extensions.Hosting.Abstractions` and costs an extra dependency, yet fires at the first resolve of the typed client anyway. `\n` is rejected at the same time: the key goes through `TryAddWithoutValidation`, which by definition validates nothing |
| `BaseUrl` normalization | `new Uri(base, "transmissions")` with a base lacking a trailing slash eats the last segment: an enterprise endpoint `https://host/api/v1` would send everything to `https://host/api/`. The built-in constants have the slash; a hand-typed one may not |
| Malformed webhook body → 400 | It was 500, indistinguishable in logs from "my handler crashed". SparkPost retries either way — this is about diagnostics, not retries |
| `AddSparkPost(IConfiguration)` | README was forced to write `Configuration["SparkPost:ApiKey"]!` with a null-forgiving operator — in itself a sign of a missing overload. Costs a dependency on `Microsoft.Extensions.Options.ConfigurationExtensions` (8.0.x, same line) and `EnableConfigurationBindingGenerator` in the DI project. Binding goes through the generator, no reflection needed — so no `Requires*` attributes either: the comparison with `SubstitutionData(object?)` no longer holds here |
| `new SparkPostClient(options)` | Outside DI the user had to create an `HttpClient` and know about its lifetime. A static shared client with `PooledConnectionLifetime = 2 min` — otherwise stale DNS, the only real danger of a static `HttpClient` |
| `User-Agent: SparkPoster/{version}` | Standard for an API client and the first thing their support asks for |
| CI actions pinned by SHA + Dependabot, `SECURITY.md`, `CHANGELOG.md` | The `publish` job holds an OIDC token that nuget.org exchanges for a publishing key: a moved action tag = a moved package |

**Deliberately left alone:** `TransmissionRequest` and `Recipient` print the message body and
substitution data in `ToString()`. That is PII, not secrets, and masking it means breaking
debugging for the sake of a log nobody writes. Only records that provably contain a secret
are masked.
