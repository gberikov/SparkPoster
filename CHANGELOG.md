# Changelog

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). While the version is below
1.0, a minor bump may break the API.

## [0.3.0] - 2026-09-06

An external review against the official documentation and the full sample payloads. Several
changes are breaking on the event model and are marked as such; the version stays below 1.0.

### Security

- `Transmissions.SendAsync` rejects an idempotency key outside `^[A-Za-z0-9._-]{1,255}$` before
  building the request. The header went out through `TryAddWithoutValidation`, so a key built
  from an unchecked business identifier could carry a CR/LF and inject a second header.
- `SparkPostClient` refuses a plain `http://` base URL unless it points at a loopback address:
  the API key travels in a header.

### Fixed

- An unfamiliar `type` under a known webhook category, or a known `type` under the wrong
  category (a `click` inside `message_event`, say), is reported as `UnknownSparkPostEvent`
  instead of being forced into the category's model. Webhooks and the Events API now dispatch
  through the same type table.
- Display names in the `To`, `CC` and `BCC` headers escape `"` and `\`: `Jane "JJ" Doe` used
  to produce an unquoted, invalid header.
- A malformed common event field no longer clears valid identifiers, timestamps or other
  common fields in `UnknownSparkPostEvent`. Unreadable values remain available in `Raw`.
- Unknown fields inside geolocation, parsed User-Agent and A/B test models are preserved in
  each nested model's `Extra` dictionary instead of being discarded.
- Every `open`, `click` and their AMP counterparts arrived as `UnknownSparkPostEvent`:
  `initial_pixel` is a boolean on the wire and was typed as a string. **Breaking:**
  `InitialPixel` is now `bool?`.
- `EventQuery.TransmissionIds` and `MessageIds` went out as `transmission_ids` and
  `message_ids`; SparkPost ignores unknown parameters, so the filter silently widened the search.
  They are now `transmissions` and `messages`.
- `EventQuery.From`/`To` dropped the seconds and sent a separate `timezone`; the documented
  format is `YYYY-MM-DDTHH:MM:ssZ`. A ten-second window used to collapse into `from == to`.
- `EventQuery.Delimiter` now joins the lists it declares; they were always joined with a comma.
- `UnsubscribeEvent.MailFrom` reads `mailfrom`, the real wire name; it used to land in `Extra`.
- `TemplateContent.From` accepts the string form SparkPost documents and stores; a template with
  `"from": "{{ friendly_from }} <team@example.com>"` used to throw `JsonException`. An `Address`
  with only `Email` set is written back as a string.
- A non-string `type` in a webhook event threw `InvalidOperationException` and lost the rest of
  the batch. A body without the `msys` wrapper used to parse as an empty batch and get a 200; it
  now throws `JsonException`, which the endpoint answers with 400.
- `TransmissionBuilder.Build()` copies the headers; without CC the request used to share the
  builder's dictionary, so a `Header()` call after `Build()` changed a request already built.

### Changed

- **Breaking:** fields SparkPost sends for several categories moved to the `SparkPostEvent`
  base: `BounceClass`, `ErrorCode`, `Reason`, `RawReason`, `NumRetries`, `RecipientDomain`,
  `MailboxProvider`, `MailboxProviderRegion`, `SendingIp`, `IpAddress`, `RoutingDomain`,
  `MsgSize`, `DelvMethod`, `InjectionTime`, `MsgFrom`. `InjectionTime` is now a `DateTimeOffset?`.
  Newly typed on the base: `CustomerId`, `RcptHash`, `AbTestId`, `AbTestVersion`, `SendingDomain`,
  `RecvMethod`, `ScheduledTime`, `QueueTime`, `RemoteAddr`, `OutboundTls`, `OpenTracking`,
  `ClickTracking`, `InitialPixel`, `AmpEnabled`.
- **Breaking:** `TrackEvent.GeoIp` is a typed `GeoLocation`; `UserAgentParsed` is a typed
  `UserAgentInfo`. Coordinates and postal codes are read whether they arrive as numbers or strings.
- **Breaking:** `UnknownSparkPostEvent` reports the reason in `ParseError` instead of
  `Extra["sparkposter_parse_error"]`, and now carries the common fields (`EventId`, `Timestamp`,
  `MessageId`, ...) whenever the payload allows, so deduplication works for it too.
- **Breaking:** `IWebhooks.GetEventSamplesAsync` returns `IReadOnlyList<SparkPostEvent>` rather
  than a `JsonNode`; the samples come in the exact shape of a batch.
- `Templates.UpdateAsync` documents that `content` replaces the stored content as a whole.

### Added

- `AbTestEvent` (`ab_test_completed`, `ab_test_cancelled`) and `IngestEvent` (`success`,
  `error`) with their typed payloads; `sms_status` maps to `MessageEvent` from the Events API as
  it already did from webhooks. `SparkPostEventTypes.SmsStatus`, `IngestSuccess`, `IngestError`.
- `EventQuery.EventIds` and `AbTestVersions`.
- `Template.LastUse` and `SubaccountId`; `SparkPostError.Part` and `Line` for template errors.
- `TransmissionBuilder.SubstitutionData(JsonNode)` and `Metadata(JsonNode)`: no reflection, no
  serializer context.
- The full official sample payloads (27 webhook events, 18 Events API events) as test fixtures;
  every one has to parse into its typed model.
- README: which retries are safe and how to scope the resilience handler; handling unknown
  events; Native AOT substitution data.

## [0.2.0] - 2026-08-29

Two rounds of code review. Three changes are breaking — each is marked below — which is what a
minor bump means below 1.0. If you took 0.1.0, update: it lets a webhook endpoint run without any
check when called without options, and it prints webhook credentials in `ToString()`.

### Security

- `MapSparkPostWebhook` no longer accepts calls unchecked. The options argument is now required,
  a half-filled or whitespace-only pair (a header name without its value, a user name without its
  password, either of them blank) throws at startup instead of silently disabling the check, and an
  endpoint that really has no check has to say so through `AllowAnonymous`. **Breaking:**
  `MapSparkPostWebhook(pattern, handler)` no longer compiles.
- `WebhookAuthCredentials`, `WebhookAuthRequestDetails` and `Attachment` mask their contents in
  `ToString()`. The compiler-generated record `ToString()` used to print the Basic auth password,
  the OAuth access token, the `client_secret` inside the token request body, and the whole
  Base64 payload of an attachment — one `logger.LogInformation("{Webhook}", webhook)` away from
  the log.
- `SparkPostClient` rejects an empty API key, and one carrying a line break, at construction
  instead of sending an empty `Authorization` header and reporting SparkPost's 401.
- `DkimSettings` masks the private key in `ToString()`; a `SendingDomainRequest` carrying your own
  key pair used to print it.

### Changed

- `MapSparkPostWebhook` refuses options that configure both the secret header and Basic
  authentication (only the header was ever checked), and options that set `AllowAnonymous` next
  to a configured check (the flag was silently ignored). Configure exactly one.
  **Breaking:** both of those configurations used to start; they now throw at startup.
- `SparkPostClient` reads its `SparkPostOptions` once, at construction; changing the options object
  afterwards no longer affects an existing client (previously the key and subaccount were re-read on
  every request while the base address was not).
  **Breaking:** rotating the key by assigning to `SparkPostOptions.ApiKey` no longer reaches a
  client that already exists — build a new one.

### Fixed

- A base address without a trailing slash no longer loses its last segment. An enterprise
  endpoint written as `https://host/api/v1` used to send every request to `https://host/api/`.
- A webhook body that is not valid JSON is answered 400 rather than 500, so the logs tell
  "they sent garbage" apart from "my handler threw".
- A webhook event whose `timestamp` is unreadable — neither Unix seconds nor ISO 8601, or an epoch
  value outside the range of a date — no longer throws out of the parser (and, through
  `MapSparkPostWebhook`, no longer turns into a 500 that makes SparkPost retry the batch for
  8 hours). It is reported as an `UnknownSparkPostEvent` with the parse error in `Extra`, like any
  other unparsable event.
- Suppression-list search sends `from`/`to` in the format that endpoint documents
  (`YYYY-MM-DDTHH:mm:ssZ`, e.g. `2026-08-01T06:00:00+00:00`) and no longer appends a `timezone`
  parameter the endpoint does not have. Previously the dates went out in the Events-API shape,
  without seconds or offset.
- `start_time` is sent with whole-second precision (`YYYY-MM-DDTHH:MM:SS+-HH:MM`, as SparkPost
  documents it). A `DateTimeOffset` with fractional seconds — `DateTimeOffset.UtcNow.AddHours(2)`,
  say — used to go out with seven fractional digits.
- `UpsertManyAsync` sends only `recipient`, `type`, `description` and `list_id` per entry — the
  fields the bulk endpoint documents. An entry read back through `GetAsync` or `SearchAsync` used
  to go out with `source`, `created`, `updated` and `subaccount_id` attached.
- `PreviewAsync` accepts substitution data that is part of another `JsonNode` tree instead of
  throwing `InvalidOperationException`.
- `total_count` in event and suppression-list pages is read when SparkPost returns it as a string.
- `SparkPostClient` rejects a relative `BaseUrl` at construction with a message naming the option,
  instead of failing on the first request with `InvalidOperationException: This operation is not
  supported for a relative URI`.

### Added

- `AddSparkPost(IConfiguration)` binds the options from a configuration section through the
  configuration-binding source generator — trim- and AOT-safe.
- `new SparkPostClient(options)` — a constructor for console applications, scripts and tests,
  over a shared `HttpClient` whose `PooledConnectionLifetime` keeps DNS from going stale.
- Every request carries a `User-Agent: SparkPoster/<version>` header, appended to whatever the
  application set on the `HttpClient`.
- `SECURITY.md`, this changelog, and Dependabot for NuGet and GitHub Actions, opening its pull
  requests against `develop`. Actions in the workflow are pinned to commit SHAs.

## [0.1.0]

First release. Transmissions, event webhooks, webhook receiving, events, templates, the
suppression list and sending domains.
