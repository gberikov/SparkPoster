# SparkPoster — проектные решения

Современная .NET-библиотека для SparkPost REST API: fluent для transmissions, вебхуки,
async/await, максимальное покрытие API.

Документ фиксирует решения, принятые в интервью, вместе с обоснованиями — чтобы через
полгода не переспорить их заново.

## 0. Контекст, который надо знать до чтения

**SparkPost стал Bird Email.** Параллельно с легаси-API (`https://api.sparkpost.com/api/v1`)
существует новый Bird API: хост `https://us1.platform.bird.com/v1`, `Authorization: Bearer bk_us1_…`,
`POST /v1/email/messages` вместо `/transmissions`, плоский payload, `202 Accepted`, ID с префиксами
(`em_`, `dom_`, `whk_`, `sup_`), `PATCH` вместо `PUT`, курсорная пагинация, конверт ошибок
`{type, code, message, request_id}`, подпись вебхуков по Standard Webhooks (`webhook-id`,
`webhook-timestamp`, `webhook-signature`). Официальные SDK: TypeScript, Go, Python + CLI. **.NET нет.**

**Дата отключения легаси-API публично не объявлена** (искали — не нашли).

**Решение: пишем против легаси SparkPost v1**, потому что рабочий аккаунт и ключ есть только там.
Слой абстракции «SparkPost или Bird за одним фасадом» не закладываем: если понадобится Bird,
это будет другая библиотека, а не `if` внутри этой.

## 1. Решения

| # | Вопрос | Решение | Почему |
|---|--------|---------|--------|
| 1 | Аудитория | OSS: публичный репозиторий, пакеты на nuget.org | Момент «позже» наступил вместе с первым потребителем (profitday.kz). Обвязку не платили авансом — поставили, когда публикация стала реальной |
| 2 | TFM | Только `net8.0` | Работает у потребителей на net8/9/10, ноль `#if` |
| 3 | Пакеты | `SparkPoster` + `.Extensions.DependencyInjection` + `.AspNetCore` | Выбор пользователя |
| 4 | Объём v1.0 | Transmissions, Event Webhooks (CRUD + приём), Events, Templates, Suppression List, Sending Domains | Инфраструктуру дёшево менять на трёх разделах и дорого на восемнадцати |
| 5 | Форма API | Фасад `SparkPostClient` (+ `ISparkPostClient`) с проперти-ресурсами за интерфейсами; в DI только фасад | 18 регистраций — налог на каждое приложение; интерфейсы окупаются первым юнит-тестом потребителя |
| 6 | Fluent | Билдер независим от клиента, `Build()` → объект, отправка отдельным вызовом | Билдер — чистая функция: тестируется без HTTP, кладётся в очередь |
| 7a | Модель | `TransmissionRequest` — публичный record с `init` | `internal` — тупик: отставание билдера от API на одно поле заставляет форкать библиотеку |
| 7b | Билдер | Мутабельный, возвращает `this`, не thread-safe (в XML-доке) | Сценарий «заготовка + разные получатели» решается через `tx with { … }` бесплатно |
| 8 | Валидация | Только структурные инварианты: null-аргументы + три `required`-поля в `Build()` | Зеркало серверных правил протухает и врёт; регулярка на email ломает легальные адреса |
| 9a | Ошибки | Исключения. `SparkPostException` → `SparkPostApiException` (`StatusCode`, `Errors`, `RawBody`); отдельно `SparkPostRateLimitException` с `RetryAfter` (429/420) | Глубокая иерархия — 6 классов ради `catch … when (e.StatusCode == 401)` |
| 9b | 404 | Кидаем, как и всё остальное | `null` у одних методов и исключение у других — источник тихих багов |
| 10 | Целевой API | Легаси SparkPost v1 | Рабочий ключ есть только там |
| 11a | Ретраи | Своих нет; `AddSparkPost()` возвращает `IHttpClientBuilder`, потребитель вешает `.AddStandardResilienceHandler()` | Backoff, jitter и circuit breaker уже написаны и оттестированы в пакете Microsoft |
| 11b | Идемпотентность | `Idempotency-Key` генерируем автоматически на каждый `SendAsync`, если не задан явно | `DelegatingHandler` повторяет **тот же** `HttpRequestMessage` → внешний ретрай становится безопасным без единой настройки. Без ключа resilience-handler на 5xx отправит письмо дважды |
| 12a | События | Типизированная иерархия + `UnknownSparkPostEvent` + `JsonExtensionData`; неизвестный тип **никогда не бросает** | Новый тип события не должен ронять эндпоинт: SparkPost начнёт ретраить весь батч, включая обработанное |
| 12b | AspNetCore | `app.MapSparkPostWebhook(path, handler, options)` + проверка basic-auth/секретного заголовка; `options` обязателен (см. §8) | Исключение из обработчика → 500 (SparkPost повторит), успех → 200. Буферизация в `Channel` по умолчанию молча превращает at-least-once в at-most-once |
| 13a | JSON | Source-generated `JsonSerializerContext`, `IsAotCompatible` | Дёшево сделать сразу, дорого прикрутить потом. Кастомный конвертер для событий нужен в любом случае: дискриминатор лежит в имени внешнего свойства `msys.message_event` |
| 13b | Enum'ы | `enum` там, где значение придумываем мы; `string` + `const`-константы там, где сервер | Строгий enum падает на первом новом значении и роняет весь батч |
| 14 | Пагинация | И `GetPageAsync(query, cursor, ct)`, и `IAsyncEnumerable` поверх него | Продакшн-сценарий «продолжить с чекпоинта» требует явного курсора; обёртка — ~15 строк без состояния |
| 15a | Хост | Одно свойство `Uri BaseUrl` + константы `SparkPostEndpoints.Us/.Eu` | Два способа задать одно и то же = баг-репорт «поставил Eu, шлёт в US» |
| 15b | Субаккаунты | `client.ForSubaccount(42)` — обёртка над тем же `HttpClient` | Заголовок только в `HttpRequestMessage`, не в `DefaultRequestHeaders` |
| 16a | Тесты | xUnit v3 + голые `Assert` + свой `FakeHttpMessageHandler` | FluentAssertions с v8 платная для коммерческого использования |
| 16b | Эталон контракта | Эталонный JSON запроса (ловит нашу сериализацию) **и** фикстуры ответов из доки (ловят нашу десериализацию) | Разные ошибки: вход контролируем мы, выход — SparkPost |
| 17 | Раскладка | `src/` (3 проекта) + `tests/` (2), `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig` | `TreatWarningsAsErrors` + `GenerateDocumentationFile`: отсутствующий XML-док ломает сборку |
| 18a | substitution_data | `object?` с `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` **и** перегруз с `JsonTypeInfo<T>` | Так это решает сам ASP.NET Core: удобно по умолчанию, AOT-чисто по требованию |
| 18b | Вложения | `byte[]` + `Attachment.FromFile/FromStream` | SparkPost принимает только base64 внутри JSON — потоковой отправки не существует в принципе |
| 19 | Наблюдаемость | Ничего своего; `ActivitySource` — в бэклог | `IHttpClientFactory` и OTel-инструментация `HttpClient` уже дают логи и спаны |
| 20 | Формы контента | Один билдер, взаимоисключающие методы, проверка в `Build()` | Типобезопасный вариант требует рекурсивного дженерика `Builder<TSelf>` в публичных сигнатурах |
| 21a | Namespace | Плоско в `SparkPoster`; события — в `SparkPoster.Webhooks` | `TransmissionRequest` и `SparkPostClient` живут в одной строке кода |
| 21b | Async | Суффикс `Async`, синхронных обёрток нет, `CancellationToken` последним с `= default`, `ConfigureAwait(false)` везде с включённым `CA2007` | Клиент потокобезопасен и рассчитан на singleton |
| 22 | Лицензия/версии | MIT, MinVer (версия из git-тегов) | `LICENSE` — в первом коммите. Теги без префикса: `0.1.0`. `0.x` = право ломать API |
| 23 | Порядок работ | Тончайший вертикальный срез end-to-end | Инфраструктурные решения проверяются только на сквозном сценарии |

## 2. Безопасность — не обсуждается

- API-ключ не попадает ни в сообщения исключений, ни в `ToString()` опций.
- `RawBody` в исключении может содержать PII (адреса получателей в ошибках валидации) —
  в XML-доке прямо написано, что его нельзя вслепую лить в логи.
- У легаси-вебхуков SparkPost **нет HMAC-подписи**. Подлинность обеспечивается только тем,
  что настроено при создании вебхука (basic-auth, OAuth2 или кастомные заголовки). В README:
  эндпоинт обязан быть под HTTPS и под basic-auth или секретным заголовком, иначе кто угодно
  шлёт фальшивые `bounce`-события.
- Сравнение секретов — `CryptographicOperations.FixedTimeEquals`.

## 3. Эскиз публичного API

```csharp
// отправка
var tx = Transmission.Create()
    .From("noreply@example.com", "Example")
    .To("user@example.com")
    .Subject("Hi {{name}}")
    .Html("<p>Hi {{name}}</p>")
    .SubstitutionData(new { name = "Bob" })
    .Build();

var result = await client.Transmissions.SendAsync(tx, ct);
// result.Id, result.TotalAcceptedRecipients, result.IsIdempotentReplay

// субаккаунт
await client.ForSubaccount(42).Transmissions.SendAsync(tx, ct);

// события: страница с явным курсором — или прозрачный перебор
var page = await client.Events.GetPageAsync(query, cursor, ct);
await foreach (var e in client.Events.SearchAsync(query, ct)) { }

// приём вебхуков
app.MapSparkPostWebhook("/hooks/sparkpost", async (batch, ct) => { },
    new SparkPostWebhookOptions
    {
        SecretHeaderName = "X-Webhook-Secret",
        SecretHeaderValue = secret
    });
```

## 4. Порядок работ

Состав v1.0 из решения №4 закрыт целиком: шаги 1–7 сделаны, 93 теста зелёные.
Каждый шаг — своя feature-ветка, влитая в `develop` через `--no-ff`.

1. **Каркас**: `global.json`, `Directory.Build.props`, `Directory.Packages.props`,
   `src/Directory.Build.props`, `.editorconfig`, `LICENSE`, три проекта в `src/`.
   Версионный тег не ставим: по git flow он появляется на `master` при релизе через
   `release/*`, до тех пор MinVer выдаёт `0.0.0-alpha.0.N`.
2. **Вертикальный срез**: `Transmission.Create()…Build()` → `Transmissions.SendAsync()` →
   `TransmissionResponse`. Тест «цепочка даёт ровно этот JSON» (тело из документации) +
   тест маппинга 422 в `SparkPostApiException` с `description` внутри.
   Здесь же заводятся `tests/Directory.Build.props` и оба тест-проекта — вместе с первым
   реальным тестом, а не пустыми заготовками с плейсхолдером.
3. Остальные формы контента: шаблон, A/B, RFC822; вложения, планирование, отмена по `campaign_id`.
4. Event Webhooks: CRUD + validate + batch-status.
5. Приём вебхуков: модели событий, конвертер, `MapSparkPostWebhook`.
6. Events: `GetPageAsync` + `IAsyncEnumerable`.
7. Templates → Suppression List → Sending Domains.
8. **Публикация**: метаданные пакетов (README, Source Link, `.snupkg`), CI на GitHub Actions
   (тесты на push/PR, публикация на теге через Trusted Publishing — долгоживущего ключа
   в секретах нет), рулсеты на `master` и теги. Первый релиз — `0.1.0`, не `1.0.0`:
   решение №22 оставляет `0.x` право ломать API, и миграция profitday.kz этим правом
   почти наверняка воспользуется.

## 5. Что выяснилось по ходу

Закрыто:

- **Формат времени.** В вебхуках `timestamp` — секунды эпохи Unix строкой, в Events API —
  ISO 8601 с миллисекундами. Один конвертер принимает обе формы плюс число; оба случая под тестом.
- **Заголовок повтора.** В легаси-API это `X-Idempotent-Replayed`; `Idempotency-Replay`
  встретился только в документации Bird. Читаются оба — стоит это ничего.
- **Числа-строки.** SparkPost отдаёт одни и те же числовые поля то числами, то строками
  (`response_code` в статусе батча), а `code` в ошибке — наоборот, то строкой, то числом.
  Лечится `AllowReadingFromString` в контексте и отдельным конвертером для `code`.
  Без второго разбор тела ошибки падал молча — вскрылось тестом.
- **Форма `links`.** У курсорной пагинации встречается и объект с `next`, и массив `rel`/`href`.
  Читаются обе.
- **Формат `start_time`.** Документирован как `YYYY-MM-DDTHH:MM:SS+-HH:MM` — целые секунды с
  офсетом. Обычный `DateTimeOffset` сериализуется с дробной частью (у `DateTimeOffset.UtcNow`
  она есть всегда), поэтому на свойстве стоит отдельный конвертер: он отбрасывает доли секунды
  (именно отбрасывает, не округляет) и сохраняет офсет вызывающего. Проверка на живом аккаунте
  всё ещё не сделана, но повода отвергнуть запрос библиотека серверу больше не даёт.

Осталось проверить на живом аккаунте:

- Полный список полей по категориям событий — брать из `GET /webhooks/events/documentation`
  и `GET /webhooks/events/samples`; типизировано только употребимое, остальное лежит в `Extra`.
- Поведение 409 с кодами `1600` (тот же ключ, другое тело — ошибка вызывающего) и
  `1601` (запрос ещё выполняется — повторяемо).

## 6. Отложено сознательно

| Что | Когда добавлять |
|-----|-----------------|
| `samples/` с компилируемыми примерами | Примеров в README станет больше 3–4 |
| Approval-тесты публичного API (PublicApiGenerator) | Вместе с тегом `1.0.0`. На `0.x` право ломать API зарезервировано осознанно и первый потребитель им пользуется — до стабилизации эти тесты дают только шум на каждом коммите |
| Иконка пакета | Появится реальный png; до тех пор nuget.org рисует заглушку |
| Мультитаргет `net8.0;net10.0` | Появится net10-only API или замеренная разница в перфе |
| `netstandard2.0` / .NET Framework | Появится конкретный потребитель на Framework |
| `ActivitySource` со спанами (`sparkpost.transmission_id`, число принятых получателей) | После рабочей библиотеки; публичный API не меняет |
| Свой retry-handler | Если `AddStandardResilienceHandler` окажется недостаточен |
| Metrics, A/B Testing, Snippets (`/api/labs`), Recipient Lists, Subaccounts, API Keys, IP Pools, Sending IPs, Inbound Domains, Relay Webhooks, Tracking Domains, DKIM Keys, Data Privacy | По разделу за раз после v1.0. Metrics и A/B Testing — последними: огромная поверхность параметров при низком спросе |
| Буферизация вебхуков через `Channel` | Никогда по умолчанию — это осознанный выбор потребителя |

**Не делаем:** абстракцию поверх SparkPost и Bird одновременно; полное зеркало серверной
валидации; глубокую иерархию исключений; тихую трансляцию `X-MSYS-SUBACCOUNT` в query-параметр
`subaccounts` для Metrics/Events.

## 7. Ограничения окружения

- Sandbox-домен `sparkpostbox.com` — **5 писем за всё время жизни аккаунта**. Интеграционные
  тесты гоняем на неотправляющих эндпоинтах; реальная отправка — один ручной smoke-тест, не в CI.
- Интеграционный проект целиком пропускается без переменной `SPARKPOST_API_KEY`.

## 8. Ревью перед 0.2.0

Найдено и закрыто. Всё, кроме последнего пункта, — правки в уже написанном коде,
а не новые решения.

| Что | Почему так |
|-----|------------|
| `MapSparkPostWebhook` требует `options`, полупустая пара падает на старте, явный `AllowAnonymous` | Было `options = null` → приёмник без проверок по умолчанию, а `HasAnyCheck` смотрел только на `SecretHeaderName` и `BasicAuthUsername`: конфиг с заполненным `SecretHeaderValue` и забытым именем **молча** пускал всех. Это ровно тот отказ, который не замечают, пока фальшивые `bounce` уже в базе. Ломает API — решение №22 это на `0.x` разрешает |
| `PrintMembers` у `WebhookAuthCredentials`, `WebhookAuthRequestDetails`, `DkimSettings`, `Attachment` | Сгенерированный `ToString()` записи печатает все свойства. `Webhook`, прочитанный через `GetAsync`, уносил в логи собственный пароль и `access_token`, а `Body` (это `JsonNode`, он печатает JSON, в отличие от словаря) — `client_secret`. §2 запрещает это для API-ключа; тот же класс проблемы, другой секрет. `DkimSettings` печатает приватный ключ DKIM, который пользователь принёс сам, — секрет того же класса. `Attachment` — не секрет, а мегабайты base64 в логе |
| Проверка `ApiKey` в конструкторе `SparkPostClient` | Пустой ключ уезжал пустым заголовком и возвращался невнятным 401. Проверка в конструкторе, а не `ValidateOnStart`: последний живёт в `Microsoft.Extensions.Hosting.Abstractions` и стоит лишней зависимости, а срабатывает всё равно при первом резолве типизированного клиента. Заодно отсекается `\n`: ключ уходит через `TryAddWithoutValidation`, который по определению ничего не валидирует |
| Нормализация `BaseUrl` | `new Uri(base, "transmissions")` при базе без слэша на конце съедает последний сегмент: enterprise-эндпоинт `https://host/api/v1` слал бы всё на `https://host/api/`. Встроенные константы слэш имеют, введённый руками — нет |
| Битое тело вебхука → 400 | Было 500, неотличимое в логах от «упал мой хендлер». Ретраить SparkPost всё равно будет — это про диагностику, не про ретраи |
| `AddSparkPost(IConfiguration)` | README был вынужден писать `Configuration["SparkPost:ApiKey"]!` с null-forgiving — сам по себе признак нехватки перегрузки. Стоит зависимости `Microsoft.Extensions.Options.ConfigurationExtensions` (8.0.x, та же линия) и пары атрибутов `RequiresUnreferencedCode`, как у `SubstitutionData(object?)` |
| `new SparkPostClient(options)` | Вне DI пользователь был обязан завести `HttpClient` и знать про его lifetime. Статический общий клиент с `PooledConnectionLifetime = 2 мин` — иначе застрявший DNS, единственная реальная опасность статического `HttpClient` |
| `User-Agent: SparkPoster/{version}` | Норма для API-клиента и первое, что спросит их поддержка |
| Экшены в CI по SHA + Dependabot, `SECURITY.md`, `CHANGELOG.md` | В джобе `publish` лежит OIDC-токен, который nuget.org меняет на ключ публикации: сдвинутый тег экшена = сдвинутый пакет |

**Сознательно не тронуто:** `TransmissionRequest` и `Recipient` печатают в `ToString()` тело
письма и данные подстановок. Это PII, а не секреты, и маскировать их — значит ломать
отладку ради лога, который никто не пишет. Маскируются только записи, где секрет
доказуемо есть.

