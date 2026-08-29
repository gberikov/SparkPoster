# SparkPoster

Always respond in Russian, regardless of the language of the prompt. Keep code, identifiers,
commit messages, and technical terms as-is.

.NET-библиотека для SparkPost REST API. Проектные решения и обоснования — в
[`docs/design.md`](docs/design.md), читать перед любой архитектурной правкой.

## Git flow

Репозиторий работает по git flow (как в `C:\Develop\ProfitDay\profitday.kz`):

| Ветка | Назначение |
|-------|------------|
| `master` | Стабильные релизы, только merge из `release/*` и `hotfix/*` |
| `develop` | Интеграционная ветка, база для всех фич |
| `feature/*` | Новая функциональность, ответвляется от `develop` |
| `bugfix/*` | Исправления в `develop` |
| `release/*` | Подготовка релиза: `develop` → `master` |
| `hotfix/*` | Срочные правки от `master` |

- **Прямые коммиты в `master` и `develop` запрещены** — только через ветки и merge.
- Каждая ветка пушится на `origin` (`https://github.com/gberikov/SparkPoster`, приватный).
- Теги версий — **без префикса**: `0.1.0`, а не `v0.1.0` (совпадает с дефолтом MinVer,
  который берёт версию пакета из тега). Тег ставится на `master` при релизе через `release/*`.
- Конфигурация лежит в `.git/config` (`gitflow.*`), команды `git flow feature start <name>` и т.п.
  работают из коробки; вручную то же самое — `git checkout -b feature/<name> develop`.

### Защита master и тегов

GitHub-side protection (rulesets и классический branch protection) для **приватных**
репозиториев требует GitHub Pro — API отвечает `403 Upgrade to GitHub Pro or make this
repository public`. Поэтому защита локальная, хуком `.githooks/pre-push`: он запрещает
удаление и не-fast-forward пуш для `master` и всех тегов.

```bash
git config core.hooksPath .githooks   # выполнить заново после каждого клонирования
```

Осознанный обход — `git push --no-verify`. Когда репозиторий станет публичным
(решение №1: OSS позже), защиту надо перенести на сторону GitHub — там она бесплатна
и, в отличие от хука, действует на всех.

## Коммиты

Conventional commits, тип и scope латиницей, описание на русском:

```
feat(transmissions): fluent-билдер для inline-контента
fix(webhooks): не падать на неизвестном типе события
docs(design): решение по идемпотентности
refactor(json): вынести конвертер событий
test(transmissions): эталонный JSON запроса
```

## Build & Test

```bash
rtk dotnet build SparkPoster.slnx
dotnet test --solution SparkPoster.slnx     # xUnit v3 на Microsoft.Testing.Platform
```

Интеграционные тесты целиком пропускаются без переменной окружения `SPARKPOST_API_KEY`.
Sandbox-домен `sparkpostbox.com` ограничен **5 письмами за всё время жизни аккаунта** —
реальную отправку в CI не гонять.

## Что нельзя менять не подумав

- **API-ключ не должен попадать в логи, сообщения исключений и `ToString()`.**
- `Idempotency-Key` генерируется автоматически на каждый `SendAsync` — без него внешний
  retry-handler отправит письмо дважды.
- Неизвестный тип события вебхука **никогда не бросает исключение** — иначе SparkPost
  начнёт ретраить весь батч, включая уже обработанные события.
- `DefaultIgnoreCondition = WhenWritingNull` в JSON: `null` в поле трактуется SparkPost
  как «сбросить», а не «не трогать».
