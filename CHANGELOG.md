# Changelog

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). While the version is below
1.0, a minor bump may break the API.

## [Unreleased]

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
