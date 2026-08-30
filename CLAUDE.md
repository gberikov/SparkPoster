# SparkPoster

A .NET client library for the SparkPost REST API. Design decisions and their rationale live in
[`docs/design.md`](docs/design.md) — read it before any architectural change.

## Language

**Everything is in English**: code, XML docs, exception messages, comments, test names,
documentation, commit messages. The library is international: XML docs ship in the NuGet package
and surface in every consumer's IntelliSense, exception messages end up in other people's logs
and bug reports, and `git log` is documentation too.

## Git flow

The repository follows git flow:

| Branch | Purpose |
|--------|---------|
| `master` | Stable releases; merges from `release/*` and `hotfix/*` only |
| `develop` | Integration branch; base for all features |
| `feature/*` | New functionality; branches off `develop` |
| `bugfix/*` | Fixes in `develop` |
| `release/*` | Release preparation: `develop` → `master` |
| `hotfix/*` | Urgent fixes off `master` |

- **No direct commits to `master` or `develop`** — branches and merges only.
- Branches are pushed to `origin` (`https://github.com/gberikov/SparkPoster`) and land in
  `develop` through a pull request.
- Version tags have **no prefix**: `0.1.0`, not `v0.1.0` (matches the MinVer default, which
  derives the package version from the tag). The tag goes on `master` at release time via
  `release/*`.
- The git-flow extension with default settings (`master`/`develop`, `feature/`, `bugfix/`,
  `release/`, `hotfix/`, empty tag prefix) matches this layout, so `git flow feature start <name>`
  works; by hand it is `git checkout -b feature/<name> develop`.

### Protecting master and tags

The repository is public, so protection lives on the GitHub side — two active rulesets:

| Ruleset | Target | Forbids |
|---------|--------|---------|
| `master` | `refs/heads/master` | deletion, non-fast-forward push |
| `version tags` | `refs/tags/**` | deletion, update, non-fast-forward |

Creating a tag is allowed; changing an existing one is not: a released version is immutable,
like the package on nuget.org. The rulesets have no bypass actors — they apply to the owner too.
If a force-push to `master` is ever needed, the ruleset has to be explicitly disabled in the UI —
a visible action, unlike a quiet `--no-verify`.

The local `.githooks/pre-push` hook remains as a second line of defence: it catches the same
mistake before the network round-trip.

```bash
git config core.hooksPath .githooks   # re-run after every clone
```

## Commits

Conventional commits, in English:

```
feat(transmissions): fluent builder for inline content
fix(webhooks): do not fail on an unknown event type
docs(design): decision on idempotency
refactor(json): extract the event converter
test(transmissions): golden JSON for the request
```

## Build & Test

```bash
dotnet build SparkPoster.slnx
dotnet test --solution SparkPoster.slnx     # xUnit v3 on Microsoft.Testing.Platform
```

There are **no integration tests and no `SPARKPOST_API_KEY` variable yet** — every test goes
through `FakeHttpMessageHandler`, no key is needed anywhere. The sandbox domain
`sparkpostbox.com` is limited to **5 messages for the lifetime of the account**, so a real send
stays a manual smoke test and never runs in CI.

## Do not change without thinking

- **The API key must never appear in logs, exception messages, or `ToString()`.**
- `Idempotency-Key` is generated automatically on every `SendAsync` — without it an external
  retry handler sends the message twice.
- An unknown webhook event type **never throws** — otherwise SparkPost starts retrying the whole
  batch, including events that were already processed.
- `DefaultIgnoreCondition = WhenWritingNull` in JSON: SparkPost treats `null` in a field as
  "reset", not "leave alone".
