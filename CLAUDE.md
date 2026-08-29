# SparkPoster

Always respond in Russian, regardless of the language of the prompt. Keep code, identifiers,
commit messages, and technical terms as-is.

.NET-библиотека для SparkPost REST API. Проектные решения и обоснования — в
[`docs/design.md`](docs/design.md), читать перед любой архитектурной правкой.

## Язык

**Весь код — на английском**: XML-доки, тексты исключений, комментарии, имена тестов.
Библиотека международная: XML-доки уезжают в NuGet-пакет и всплывают в IntelliSense
у каждого потребителя, а тексты исключений попадают в чужие логи и баг-репорты.

Русскими остаются `docs/` и этот файл — рабочие документы, в пакет они не входят.
Сообщения коммитов — на русском (описание), тип и scope латиницей.

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

Репозиторий публичный, поэтому защита живёт на стороне GitHub — два активных ruleset'а:

| Ruleset | Цель | Запрещает |
|---------|------|-----------|
| `master` | `refs/heads/master` | удаление, не-fast-forward пуш |
| `version tags` | `refs/tags/**` | удаление, перезапись, не-fast-forward |

Создание тега разрешено, изменение существующего — нет: выпущенная версия неизменяема,
как и пакет на nuget.org. Bypass-акторов у рулсетов нет, они действуют и на владельца.
Если понадобится force-push в `master`, ruleset придётся явно выключить в UI — это
заметное действие, в отличие от тихого `--no-verify`.

Локальный хук `.githooks/pre-push` оставлен вторым рубежом: он ловит ту же ошибку
до сетевого запроса.

```bash
git config core.hooksPath .githooks   # выполнить заново после каждого клонирования
```

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

**`dotnet test` — без `rtk`.** Обёртка (0.45.0) не понимает форму `--solution` из
Microsoft.Testing.Platform: она молча прогоняет **ноль** тестов и возвращает exit 5,
тогда как та же команда напрямую даёт 93 зелёных. Это не косметика вывода — искажается
сам результат, поэтому «зелёный прогон» через `rtk` ничего не доказывает.
`rtk dotnet build` работает нормально.

Интеграционных тестов и переменной `SPARKPOST_API_KEY` **пока не существует** — все тесты
идут через `FakeHttpMessageHandler`, ключ никуда не нужен. Sandbox-домен `sparkpostbox.com`
ограничен **5 письмами за всё время жизни аккаунта**, поэтому реальная отправка остаётся
ручным smoke-тестом и в CI не попадает никогда.

## Что нельзя менять не подумав

- **API-ключ не должен попадать в логи, сообщения исключений и `ToString()`.**
- `Idempotency-Key` генерируется автоматически на каждый `SendAsync` — без него внешний
  retry-handler отправит письмо дважды.
- Неизвестный тип события вебхука **никогда не бросает исключение** — иначе SparkPost
  начнёт ретраить весь батч, включая уже обработанные события.
- `DefaultIgnoreCondition = WhenWritingNull` в JSON: `null` в поле трактуется SparkPost
  как «сбросить», а не «не трогать».
