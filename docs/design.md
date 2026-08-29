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
| 1 | Аудитория | Внутренняя сейчас, OSS позже | Не платим за OSS-обвязку до первого рабочего эндпоинта, но и не закапываемся |
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
| 12b | AspNetCore | `app.MapSparkPostWebhook(path, handler)` + проверка basic-auth/секретного заголовка | Исключение из обработчика → 500 (SparkPost повторит), успех → 200. Буферизация в `Channel` по умолчанию молча превращает at-least-once в at-most-once |
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
app.MapSparkPostWebhook("/hooks/sparkpost", async (batch, ct) => { });
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

Осталось проверить на живом аккаунте:

- Полный список полей по категориям событий — брать из `GET /webhooks/events/documentation`
  и `GET /webhooks/events/samples`; типизировано только употребимое, остальное лежит в `Extra`.
- Поведение 409 с кодами `1600` (тот же ключ, другое тело — ошибка вызывающего) и
  `1601` (запрос ещё выполняется — повторяемо).
- Формат `start_time` у отложенной транзакции: отправляем ISO 8601 с офсетом.

## 6. Отложено сознательно

| Что | Когда добавлять |
|-----|-----------------|
| `samples/` с компилируемыми примерами | Примеров в README станет больше 3–4 |
| Approval-тесты публичного API (PublicApiGenerator) | В момент первой публикации на NuGet |
| OSS-обвязка: SourceLink, детерминированный билд, иконка, CI + release по тегу | Решение реально публиковать |
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
