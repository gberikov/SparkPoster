# Security policy

## Reporting a vulnerability

Report privately through GitHub's
[security advisory form](https://github.com/gberikov/SparkPoster/security/advisories/new),
not through a public issue. Expect a first reply within a week.

Please include the package version, the .NET version, and the smallest code that shows the
problem. Do not include a real API key or real recipient addresses.

## Supported versions

The library is pre-1.0: only the latest released version gets fixes.

## Known issues in released versions

- **0.1.0** — `MapSparkPostWebhook(pattern, handler)` called without options accepted every call
  unchecked, so anyone who learned the endpoint's URL could feed it forged bounce and unsubscribe
  events; and the record `ToString()` of `Webhook`, `WebhookAuthCredentials` and
  `WebhookAuthRequestDetails` printed the Basic auth password, the OAuth access token and the
  `client_secret`, one `logger.LogInformation("{Webhook}", webhook)` away from the log.
  Fixed in 0.2.0 — update, and rotate any webhook credential that may have reached a log.

## What this library expects of you

Three things it cannot do on your behalf:

- **The API key belongs in an environment variable or a secret store**, never in an
  `appsettings.json` that is under version control. The library never logs it, never puts it in
  an exception message, and `SparkPostOptions` has no `ToString()` that would reveal it.
- **SparkPost webhooks carry no signature.** Authenticity rests entirely on what you configured
  when creating the webhook, so `MapSparkPostWebhook` requires either Basic authentication or a
  secret header — or an explicit `AllowAnonymous = true` if a gateway in front already checks.
  Serve the endpoint over HTTPS.
- **`SparkPostApiException.RawBody` can hold personal data**: validation errors echo recipient
  addresses back. Think before dumping it into logs.
