# Changelog

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). While the version is below
1.0, a minor bump may break the API.

## [Unreleased]

### Security

- `MapSparkPostWebhook` no longer accepts calls unchecked. The options argument is now required,
  a half-filled pair (a header name without its value, a user name without its password) throws
  at startup instead of silently disabling the check, and an endpoint that really has no check
  has to say so through `AllowAnonymous`. **Breaking:** `MapSparkPostWebhook(pattern, handler)`
  no longer compiles.
- `WebhookAuthCredentials`, `WebhookAuthRequestDetails` and `Attachment` mask their contents in
  `ToString()`. The compiler-generated record `ToString()` used to print the Basic auth password,
  the OAuth access token, the `client_secret` inside the token request body, and the whole
  Base64 payload of an attachment — one `logger.LogInformation("{Webhook}", webhook)` away from
  the log.
- `SparkPostClient` rejects an empty API key, and one carrying a line break, at construction
  instead of sending an empty `Authorization` header and reporting SparkPost's 401.

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

### Added

- `AddSparkPost(IConfiguration)` binds the options from a configuration section.
- `new SparkPostClient(options)` — a constructor for console applications, scripts and tests,
  over a shared `HttpClient` whose `PooledConnectionLifetime` keeps DNS from going stale.
- Every request carries a `User-Agent: SparkPoster/<version>` header.
- `SECURITY.md`, this changelog, and Dependabot for NuGet and GitHub Actions. Actions in the
  workflow are pinned to commit SHAs.

## [0.1.0]

First release. Transmissions, event webhooks, webhook receiving, events, templates, the
suppression list and sending domains.
