# Аудит утечек памяти — 27.11.2026

Статус: **только аудит**. Код не изменялся, коммиты/деплой не выполнялись.

Охват: `LP.Client/src` (Angular), `LP.Server`, `LP.Entity`, `LP.Common`, `LP.ThrustLoader`, конфигурации. Частично затронуты `LP.Chat`, `LP.Journal`, `LP.Server/OAuth` — без детального разбора.

---

## CRITICAL

### 1. `LP.Server/Program.cs:60-73` — второй DI-контейнер и дублирующий `ConnectionMultiplexer`

```csharp
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config =
        builder.Configuration.GetConnectionString(builder.Environment.IsDevelopment() ? "RedisLocal" : "Redis");
    return ConnectionMultiplexer.Connect(config);
});
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.ConnectionMultiplexerFactory = () =>
        Task.FromResult(builder.Services
            .BuildServiceProvider()
            .GetRequiredService<IConnectionMultiplexer>());
    options.InstanceName = "LP_";
});
```

- `BuildServiceProvider()` создаёт отдельный DI-контейнер, который никто не вызывает `Dispose()`.
- Внутри него создаётся **второй** `IConnectionMultiplexer`, не совпадающий с синглтоном из строки 60.
- Замыкание на `builder.Services` удерживает граф сервисов до конца процесса.
- При повторном создании `RedisCache` / вызове фабрики число неосвобождаемых провайдеров и соединений может расти.

**Исправление:** не строить новый провайдер, а брать уже зарегистрированный синглтон через `sp`:

```csharp
builder.Services.AddSingleton<IDistributedCache>(sp =>
    new RedisCache(new RedisCacheOptions
    {
        InstanceName = "LP_",
        ConnectionMultiplexerFactory = () =>
            Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>())
    }));
```

Либо вообще убрать `ConnectionMultiplexerFactory` и задать `options.Configuration = config`.

---

## HIGH — сервер

### 2. `LP.Server/Controllers/PhotosController.cs:24,517` — неограниченный static-словарь семафоров

```csharp
private static readonly ConcurrentDictionary<string, SemaphoreSlim> OptimizationLocks = new(StringComparer.OrdinalIgnoreCase);
...
var gate = OptimizationLocks.GetOrAdd(cachePath, _ => new SemaphoreSlim(1, 1));
```

- Ключ — путь к `.cache`-файлу конкретного изображения.
- Записи никогда не удаляются, семафоры не освобождаются (`Dispose()` нет).
- Рост памяти пропорционален числу уникальных фото за время жизни процесса.

**Исправление:** удалять запись в `finally` после завершения оптимизации, либо `IMemoryCache` с TTL и `PostEvictionCallback`, вызывающим `Dispose()`, либо общий пул семафоров с ограниченным размером.

### 3. `LP.ThrustLoader/HttpLoadModels.cs:43-91`, `Form1.cs:61,123-136,153` — неограниченный `ConcurrentBag<long> Latencies`

- `Latencies` получает запись на **каждый** запрос нагрузочного теста.
- При `DurationSeconds <= 0` (`HttpLoadRunner.cs:82`) тест бесконечный → память растёт линейно до OOM.
- `Snapshot()` на каждом тике таймера копирует и сортирует весь массив: O(n log n) и временные пики памяти.

**Исправление:** streaming-статистика (`Interlocked` счётчик/сумма/min/max, гистограмма с фиксированным числом бакетов для перцентилей) или скользящее окно через `ConcurrentQueue<long>` с жёстким лимитом. Это инструмент нагрузки, не прод.

### 4. `LP.Entity/CityLoader.cs:31,36,56-64,128-134` — `HttpClient` в scoped-сервисе без `Dispose`

```csharp
private readonly HttpClient _httpClient = new HttpClient { ... };
```

- Класс зарегистрирован `AddScoped` (`Program.cs:201`), но не реализует `IDisposable` → DI не вызовет dispose.
- `var response = await _httpClient.GetAsync(url);` — без `using`.
- `await _context.Cities.ToArrayAsync()` — весь справочник городов в память на каждый вызов.

**Исправление:** инъецировать `IHttpClientFactory` (уже зарегистрирован в `Program.cs:190`) и создавать клиент через `_factory.CreateClient()`; `using var response = ...`; добавить пагинацию/`IAsyncEnumerable`/`Take`.

---

## MEDIUM — сервер

| Место | Замечание | Исправление |
|---|---|---|
| `LP.Server/Controllers/PhotosController.cs:89-90` | `new HttpClient` на запрос + `HttpResponseMessage` без `using` → socket churn и утечка response. | `IHttpClientFactory` + `using var response`. |
| `LP.Server/Services/LocalAIService.cs:63-67` | Singleton-сервис, `HttpResponseMessage` без `using`. | `using var response = await _httpClient.PostAsJsonAsync(...)`. |
| `LP.Server/Services/EmailService.cs:37,71` | `MailMessage` без `using` (`SmtpClient` уже через `using var`). | `using var message = ...`. |
| `LP.Server/Services/AuthService.cs:30` | `private readonly MD5 _md5 = MD5.Create();` в scoped-сервисе без `IDisposable`. | `MD5.HashData(...)` (net9) или реализовать `IDisposable`. |
| `LP.Server/Controllers/RedisController .cs:16` | `SemaphoreSlim` в transient-контроллере без dispose. Практический риск невелик. | Убрать семафор или реализовать `IDisposable` на контроллере. |
| `LP.Server/Controllers/RedisController .cs:136-151` | Fire-and-forget `Task.Run`. Задача конечная (1 с + TTL), утечкой не является. | Перевести на `await` / `BackgroundService`, чтобы не терять исключения и не держать ссылки на `HttpContext`. |

Заметка: `LP.Server/Services/AuthService.cs:33` `static bool _adminInitialized` — не растёт, это нормально.

---

## LOW — не утечки, но рост пиковой памяти

| Файл | Строки | Что |
|---|---|---|
| `LP.Server/Controllers/VotesController.cs` | `241-267` | `SqlQueryRaw...ToListAsync()` — все матчи пользователя, затем in-memory `Skip/Take`. |
| `LP.Server/Controllers/ChatsController.cs` | `196,222,265` | Вся история сообщений чата без лимита. |
| `LP.Server/Controllers/CityController.cs` | `51-53` | Все города. |
| `LP.Server/Controllers/EventsController.cs` | `161-184` | Все события в `StringBuilder`. |
| `LP.Server/Controllers/JournalController.cs` | `128-154` | Все записи журнала в `StringBuilder`. |
| `LP.Server/Services/Rating/RatingService.cs` | `226-233` | Все чаты пользователя для расчёта рейтинга (кэш при этом с TTL). |
| `LP.Server/Services/Events/EventImportService.cs` | `46-57` | Загружает все источники, затем применяет `Take(10)`. |

**Рекомендация:** серверный `Skip/Take`, `IAsyncEnumerable`, `ExecuteDeleteAsync`, лимиты на размер истории.

---

## Кэши и TTL

Явных записей в Redis/InMemory без TTL в активном коде **не найдено**:

- `LP.Server/Controllers/AuthController.cs:151` — TTL на коде подтверждения.
- `LP.Server/Controllers/RedisController .cs:54-61` — TTL 3 с; `141-145` — TTL по умолчанию 24 ч.
- `LP.Server/Services/Rating/RatingService.cs:176-182` — `MemoryCacheEntryOptions` с абсолютным TTL.
- `LP.Server/Helpers/SlugHelper.cs:11` — фиксированный static-словарь, не растёт.
- В некомпилируемых файлах TTL тоже есть: `LP.Server/OAuth/VkProvider.cs:191`, `LP.Server/OAuth/Vkauthcontroller.cs:39`.

Единственная реальная static-коллекция без очистки в активном коде — `OptimizationLocks` (п. 2).

---

## Мёртвый код (не влияет на рантайм)

Исключены из компиляции в `LP.Server/LP.Server.csproj:11-16`:

- `OAuth/GoogleProvider.cs`
- `OAuth/MailRU.cs`
- `OAuth/MailruProvider.cs`
- `OAuth/TelegramController.cs`
- `OAuth/Vkauthcontroller.cs`
- `OAuth/VkProvider.cs`

В `GoogleOAuthController .cs:118-146` есть тот же паттерн `new HttpClient` без dispose. Если файлы вернут в сборку — находка станет актуальной.

Также исключён `LP.Common/LP.Common.csproj:10` → `Services/LikeRestrictionService.cs`.

---

## P1 — реальные утечки / циклы: Angular-клиент

### 5. `LP.Client/src/app/vote/vote.component.ts` + `vote.service.ts`

- `vote.component.ts:282-287`: если `.active-card` не найден, `setTimeout(() => this.setupSwipeGestures(), 100)` перезапускает сам себя без лимита попыток и без проверки уничтожения компонента. При пустом списке / прямом переходе с `userId` цикл живёт вечно.
- `vote.component.ts:369-389`: `addEventListener` на `touchstart`, `touchmove`, `touchend`, `touchcancel`, `mousedown` элемента и — главное — на `document` (`mousemove`, `mouseup`). `removeEventListener` нет.
- `vote.component.ts:201-216`: `ngOnDestroy` клонирует только `#activePhotoContainer`; `document`-слушатели остаются.
- `vote.component.ts:218-225` и `246-258`: `setupSwipeGestures()` вызывается дважды → обработчики дублируются.
- `vote.component.ts:247-257`: `setInterval` сам очищается, но `setTimeout(..., 5000)` не отменяется при destroy.
- `vote.service.ts:110-115` + `vote.component.ts:145-152`: `effect()` читает `profiles().length` и при `< 4` вызывает `loadMoreProfiles()`. Сервис всегда делает `profiles.update(...)` и возвращает новый массив, даже если `uniqueNew` пуст → сигнал меняется → effect снова → новый запрос. `lastLoadEmpty` не спасает при непустом, но дублирующемся ответе сервера.

**Исправление:** хранить ссылки на обработчики и снимать их через `DestroyRef.onDestroy`/`ngOnDestroy`; в retry проверять флаг уничтожения и лимит попыток; не обновлять сигнал при `uniqueNew.length === 0`; выставлять `lastLoadEmpty` по фактическому приросту; отменять все таймеры.

### 6. `LP.Client/src/app/match/match.component.ts:228-237,117-127`

- `attachScrollListener()` вешает `galleryContainer.addEventListener('scroll', this.onGalleryScroll.bind(this))` — каждый раз новый анонимный обработчик.
- Метод вызывается при каждой(initial) загрузке и смене вкладок: `114`, `262`, `456`.
- `ngOnDestroy` удаляет `this.scrollListener`, но это поле (`67`) **никогда не присваивается** в `attachScrollListener`.
- Итог: обработчики остаются на элементе, удерживают компонент и умножают вызовы `loadMoreImages()` (`415-427`).

**Исправление:** сохранять точную ссылку на функцию и элемент, проверять дубликаты, удалять в `ngOnDestroy`; отменять `setTimeout(..., 0)` из `229`.

### 7. `LP.Client/src/app/search/search.component.ts:370-397,178-189`

- Слушатель вешается на `scrollContainer = galleryContainer.closest('.gallery-wrapper')` (`373`, `393`), а в `ngOnDestroy` (`181-185`) удаляется с `galleryContainerRef().nativeElement` — другой элемент.
- `383`: fallback добавляет `window.addEventListener('scroll', this.onWindowScroll.bind(this))` — новый bound-объект, который нельзя снять через `188` (`onWindowScrollBound` из `168`).
- Базовый window-слушатель (`170-176`) снят корректно.
- Таймеры `233`, `322`, `371` не отменяются при destroy.

**Исправление:** хранить `{target, handler}` и снимать именно их; вместо `.bind(this)` использовать сохранённый обработчик; очищать таймеры.

### 8. `LP.Client/src/app/auth/auth.component.ts:395-416,83-86`

- Слушатель `window.message` удаляется только внутри веток `TG_AUTH_SUCCESS` / `TG_AUTH_ERROR` (`407`, `412`).
- `ngOnDestroy` только сбрасывает `vkInitialized`.
- Если пользователь ушёл со страницы, popup закрыл `window.close()` или сработал блокировщик (`423-425`) — слушатель остаётся на время жизни приложения и замыкает компонент. Повторный вход добавляет новые слушатели.

**Исправление:** хранить `handleMessage` в поле и снимать в `ngOnDestroy`; лучше `fromEvent(window, 'message').pipe(takeUntilDestroyed(...))`.

### 9. `LP.Client/src/app/services/AuthService.ts:117-160` — `socialLogin` без teardown

- Внутри `new Observable` вешаются `window.addEventListener('message', messageHandler)` (`148`) и `setInterval(..., 1000)` (`150`).
- Функция teardown не возвращается: при `unsubscribe()` слушатель и интервал остаются.
- Интервал останавливается только если `popup.closed` (`150-156`).
- Сейчас вызовы закомментированы (`auth.component.ts:252-263`), поэтому приоритет ниже.

**Исправление:** вернуть из Observable функцию, очищающую `clearInterval` и `removeEventListener`.

---

## P2 — средние / потенциальные: Angular-клиент

| Место | Замечание | Исправление |
|---|---|---|
| `SignalRConnectionManager.ts:231-249` | Рекурсивный reconnect без лимита попыток, задержка до 30 с. Не классическая утечка: `disconnect()` (`307-316`) чистит таймер, `reconnectAttempts` сбрасывается на успехе (`173`, `186`, `202`). | Добавить `MAX_RECONNECT_ATTEMPTS` / expiring backoff и состояние «offline». |
| `auth.callback.ts:17-22` | `BroadcastChannel` создаётся, шлёт сообщение и не закрывается.Popup обычно сразу закрывается, риск невелик. | `channel.close()` в `ngOnDestroy` или сразу после `postMessage`. |
| `ChatService.ts:57,78-81,577-599` | `messageIds`, `readReceiptsSent`, `joinedChatIds`, `joiningChats` и `messages` растут без лимита; чистка только в `removeChatMessages` и `reset()` при logout. | Ограничить историю (cap/окно), чистить `readReceiptsSent` после подтверждения, пересобирать ID-наборы из актуальных сообщений. |
| `UserStatusService.ts:15,121-137` | `Map` статусов пополняется и offline-записями без вытеснения. Очистка только на reconnect и `reset()`. | Хранить только онлайн-статусы или вытеснять старые `lastSeen`. |
| `user-location.ts:42-47` | `watchPosition()` без отписки; кнопку можно нажать многократно. Сам `LocationService.watchPosition()` (`172-190`) корректно возвращает teardown. | `takeUntilDestroyed(this.destroyRef)` или хранить `Subscription` и отписываться. |
| `loadable.component.ts:53-72,85-92` | `setInterval` не чистится в `catch`. Класс абстрактный и в `src` нет наследников — латентная проблема. | `finally { clearInterval(progressInterval); }`. |
| `toast.service.ts:31-86` | При `duration=0` таймер не создаётся, слушатели `mouseenter/mouseleave` висят до ручного закрытия. Вызовов с `0` нет. | Всегда создавать таймер или снимать слушатели при закрытии. |
| `TelegramService.ts:66-99`, `telegram.component.ts` | Глобальный `window.onTelegramAuth`, script/iframe остаются послеdestroy компонента. | Очищать обработчик и DOM в `ngOnDestroy`. |
| `PageStateService.ts:7-24` | `Map` с массивами изображений; TTL проверяется только в `getState()`, непрочитанные ключи остаются. Ключи фиксированные (`search`, `match`) — низкий риск. | Периодическая чистка по TTL. |
| `user-rating.component.ts:148,195-207`, `match.component.ts:130-142` | Локальные `Map`-кэши; не синглтоны, ограничены временем жизни компонента. | Оставлять как есть. |

---

## Вне детального scope

Обнаружено широким grep, не разбиралось:

- `LP.Chat/ChatHub .cs:27` — static `_connections`; `318,332,344` — `Task.Run` без отслеживания.
- `LP.Chat/MessageBotService.cs:19-20` — static `_activeDialogs` / `_dialogLocks`.
- `LP.Chat/ChatWebSocketManager.cs:45` — вложенные `List<WebSocket>`.
- `LP.Chat/BufferedMessageStore .cs:35` — `Task.Run`.
- `LP.Journal/Services/Journal/ArticleFetcher.cs:119` и `JournalHttp.cs:20` — `while (true)`.
- `LP.Journal/Services/Journal/JournalCrawlBackgroundService.cs` — корректный cancellation.

Рекомендуется отдельный аудит этих проектов.

---

## Где утечек нет / всё корректно

### Angular

- `app.ts:83-85,93-105,111-122` — `router.events`, `BroadcastChannel`-listener и `userId$` корректно снимаются в `ngOnDestroy` (`131-138`).
- `bottom-nav.component.ts:56-61` — `takeUntil` + `destroy$.next()/complete()`.
- `chat-list.component.ts:36-41`, `chat-layout.component.ts:90-95`, `chat-component.ts:85-95` — `takeUntilDestroyed`; интервалы clears через `DestroyRef.onDestroy`.
- `events/event-detail.component.ts:32-81`, `journal/journal-detail.component.ts`, `admin/admin-events.component.ts`, `admin/admin-journal.component.ts` — подписки через `takeUntil(this.destroy$)`.
- `base.component.ts:123` — `firstValueFrom(result.pipe(takeUntil(this.destroy$)))`; наследники используют этот механизм.
- Одноразовые `HttpClient`-подписки сами `complete()` — утечкой не являются.
- `AsyncPipe` в шаблонах и `@HostListener` снимаются Angular автоматически.
- `splash.component.ts:80-100` — `effect(onCleanup)` корректно очищает `setTimeout`.
- `promo/so-pulse.component.ts:30-45` — timeout и Web Animation отменяются; `coming-soon.component.ts:26-37` — интервал очищается.
- `services/LocationService.ts:93-129` — `watchId` и таймер очищаются в `finish()`.
- `services/NavigationService.ts:10-18` — подписка на `router.events` в синглтоне живет всё время приложения, но `history` ограничен 10 URL.
- `services/SeoService.ts:59-83` — canonical переиспользуется, JSON-LD удаляется по id.
- `auth.guard.ts:12-13` — `take(1)`.
- Нет `ResizeObserver`, `MutationObserver`, `IntersectionObserver`, `MediaQueryList.addListener`, прямых `WebSocket`/`EventSource`; `matchMedia` в `vote.component.ts:277` — только синхронное чтение `matches`. `toSignal` не используется.

### .NET

- `Program.cs:83-105` — `ApplicationContext` зарегистрирован `Scoped`; retry-политика настроена.
- `Program.cs:189-190` — `AddMemoryCache()` и `AddHttpClient()` доступны для исправлений.
- `Services/EmptyChatCleanupService.cs:27-52,57-64` — корректный `BackgroundService`: `while (!stoppingToken.IsCancellationRequested)`, `Task.Delay(..., stoppingToken)`, `using var scope`, `ExecuteDeleteAsync`.
- `Services/Events/EventImportBackgroundService.cs:22-72` — тот же корректный образец.
- `Program.cs:208,209+` — hosted services подключены через `AddHostedService`.
- `LP.ThrustLoader/DbLoadRunner.cs:44,74-102` — `await using var db`, `Take(top)`, `CancellationToken`.
- `LP.ThrustLoader/HttpLoadRunner.cs:36-37,191-192,232-236` — `using var handler/client`, `await using var stream`, `using var JsonDocument`, `RecentErrors` ограничен 200 записями.
- `LP.ThrustLoader/Form1.Designer.cs:66` — `Timer(components)` освобождается контейнером; `Form1.cs:88-89,239-240` — `CancellationTokenSource` в `finally`, таймер останавливается, проверки `IsDisposed` перед UI.
- `Services/RssEventSourceProvider.cs:34-41`, `Services/JsonEventSourceProvider.cs:52-59` — `using response / stream / JsonDocument`.
- `Controllers/PaymentsController.cs:28-31` — QR-объекты через `using`.

---

## Рекомендуемый порядок исправлений

1. `LP.Server/Program.cs:60-73` — убрать `BuildServiceProvider()` из Redis-factory (CRITICAL).
2. `vote.component.ts` + `vote.service.ts` — effect/дубли/слушатели/retry/timers (P1).
3. `match.component.ts` и `search.component.ts` — слушатели скролла и таймеры (P1).
4. `auth.component.ts` и `AuthService.socialLogin` — `window.message` + teardown.
5. `PhotosController.OptimizationLocks`, `CityLoader`, `LocalAIService`, `EmailService`, `AuthService.MD5` — `IDisposable`/`IHttpClientFactory`.
6. `ChatService`, `UserStatusService`, `Latencies` — ограничение роста коллекций.
7. Жадные materializations — пагинация/лимиты.
8. Отдельный аудит `LP.Chat` и `LP.Journal`.

---

## Итоговая оценка

- **CRITICAL:** 1 (Redis/DI).
- **HIGH:** 3 сервер + 5 клиент.
- **MEDIUM:** 6 сервер + ~10 клиент.
- **LOW / не утечки, но рост памяти:** 7 мест на сервере.
- Реально растущий footprint в долгоживущем процессе дают: `Program.cs`, `OptimizationLocks`, `Latencies`, клиентский `vote`-эффект и не снятые слушатели `match`/`search`/`vote`.
- Остальное — точечная дисциплина `IDisposable` и потенциальные проблемы при долгой работе/повторном включении мёртвого кода.
