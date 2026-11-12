# События Made4Love: настройка и эксплуатация

Раздел `/events` — публичная афиша реальных мероприятий. События попадают в базу
из настроенных источников (RSS/Atom, JSON API) или создаются вручную, после чего
проходят модерацию и только затем публикуются.

## 1. Миграция базы

Автоматических миграций нет. Перед запуском новой версии примените миграцию вручную:

```powershell
dotnet ef database update --project LP.Entity --startup-project LP.Server
```

Миграция `20261111192627_AddEventsMvp`:

- создаёт таблицу `EventSources`;
- расширяет `Events` полями `Slug`, `Status`, `StartsAt`, `EndsAt`, `City`,
  `Venue`, `Address`, `ImageUrl`, `SourceUrl`, `Tags`, `SourceId`,
  `RejectionReason`, `PublishedAt`, `UpdatedAt`;
- переводит существующие события в `Published` и выдаёт им slug `event-<id>`.

Новые события из источников создаются как `Draft` и не видны публично до модерации.

## 2. Конфигурация

Секция `Events` в `appsettings.json` / `appsettings.Production.json`:

```json
"Events": {
  "AdminKey": "",
  "SiteBaseUrl": "https://made4love.ru",
  "ImportEnabled": true,
  "ImportCheckIntervalMinutes": 15,
  "MaxItemsPerSync": 100
}
```

- `AdminKey` — ключ для админ-API и веб-админки. Пустое значение полностью
  отключает админ-API (ответ `503`). Ключ передаётся заголовком `X-Admin-Key`.
- `SiteBaseUrl` — база для абсолютных ссылок в `sitemap.xml`.
- `ImportEnabled` — глобальный выключатель фонового импорта.
- `ImportCheckIntervalMinutes` — как часто фоновый сервис проверяет источники.
- `MaxItemsPerSync` — максимум элементов за одну синхронизацию.

Задавайте ключ через переменную окружения, а не в репозитории:

```powershell
$env:Events__AdminKey = "длинный-случайный-ключ"
```

## 3. Источники событий

Типы источников (`EventSourceType`):

| Тип | Описание |
| --- | --- |
| `Rss` | RSS 2.0 или Atom-лента |
| `Json` | произвольный JSON API с маппингом полей |
| `Manual` | источник без загрузки: события создаются вручную |

Общие поля источника: `Name`, `Type`, `Url`, `City`, `DefaultTags`,
`SyncIntervalMinutes`, `IsEnabled`, `ConfigJson`.

### RSS / Atom

Обязателен `Url`. Заголовок, описание, ссылка и дата берутся из стандартных
полей ленты. Город и теги по умолчанию добавляются из настроек источника.

### JSON API

`ConfigJson` описывает, где искать элементы и как называются поля:

```json
{
  "itemsPath": "results",
  "title": "name",
  "summary": "short_description",
  "description": "description",
  "startsAt": "date",
  "endsAt": "end_date",
  "city": "city.name",
  "venue": "place.title",
  "address": "place.address",
  "image": "image.url",
  "url": "link",
  "externalId": "id",
  "tags": "categories",
  "usePublishedAsStart": false
}
```

- `itemsPath` — путь до массива (пусто = корень ответа, поддерживается и массив).
- Пути к полям — через точку (`place.title`).
- `externalId` по умолчанию ищется в `externalId`, `external_id`, `id`, `uuid`,
  `guid`, `slug`.
- `tags` по умолчанию ищется в `tags`, `categories`, `topics`, `genres`.
- `usePublishedAsStart: true` — если нет даты начала, использовать дату публикации.

### Дедупликация

Порядок проверки:

1. `SourceId + ExternalId`;
2. `SourceId + SourceUrl`;
3. SHA256 от `title + startsAt + sourceId`.

Повторный импорт обновляет существующую запись, а не создаёт дубль.

## 4. Модерация

Веб-админка: `/admin/events` (закрыта от индексации, ключ хранится в
`sessionStorage` вкладки).

1. Введите `AdminKey`.
2. Вкладка «Источники»: создайте источник, нажмите «Проверить» (тестовый разбор
   без записи в БД), затем «Импорт» для ручной синхронизации.
3. Вкладка «События»: выберите «Черновики», проверьте текст, дату и место.
4. «Опубликовать» — событие появляется на `/events` и в `sitemap.xml`.
5. «Снять с публикации» — событие возвращается в черновики и исчезает с сайта.
6. «Отклонить» — событие скрывается, причина сохраняется в `RejectionReason`.
7. «Создать вручную» — для мероприятий без источника.

Фоновый импорт (`EventImportBackgroundService`) запускается через 20 секунд
после старта и далее по `ImportCheckIntervalMinutes`. Он только создаёт и
обновляет черновики, но никогда не публикует их автоматически.

## 5. Публичный API

| Метод | Маршрут | Доступ |
| --- | --- | --- |
| `GET` | `/Events/public?city=&tag=&page=&pageSize=` | анонимно |
| `GET` | `/Events/public/{slug}` | анонимно, `404` для черновиков |
| `GET` | `/Events/sitemap.xml` | анонимно |
| `GET` | `/Events/list` | авторизованные (старый список) |
| `POST` | `/Events/seen` | авторизованные |

Админ-API (`X-Admin-Key`):

```
GET    /Events/admin/sources
POST   /Events/admin/sources
PUT    /Events/admin/sources/{id}
DELETE /Events/admin/sources/{id}
POST   /Events/admin/sources/{id}/sync
POST   /Events/admin/sources/{id}/test
GET    /Events/admin/events?status=Draft|Published|Rejected|all
POST   /Events/admin/events
PUT    /Events/admin/events/{id}
POST   /Events/admin/events/{id}/publish
POST   /Events/admin/events/{id}/unpublish
POST   /Events/admin/events/{id}/reject
DELETE /Events/admin/events/{id}
```

## 6. SEO и маршруты

- `/events` и `/events/:slug` отдают SPA; мета-теги, canonical, Open Graph и
  JSON-LD (`ItemList`, `Event`) выставляет клиент после загрузки данных.
  Поэтому у маршрутов стоит `dynamicSeo: true` — глобальный обработчик в
  `app.ts` не перезаписывает их.
- `/events` имеет статическую SEO-оболочку (`generate-seo-pages.mjs`), которая
  отдаётся nginx до загрузки приложения.
- `/events-sitemap.xml` проксируется nginx на `/Events/sitemap.xml` и содержит
  только опубликованные события.
- `/admin` закрыт в `robots.txt` и получает `X-Robots-Tag: noindex, nofollow`.
- Черновики и отклонённые события недоступны публично и не попадают в sitemap.

### Ограничение SPA-индексации

Страницы событий рендерятся на клиенте. Google и Яндекс выполняют JavaScript,
но для стабильной индексации большого каталога в будущем потребуется SSR или
пререндер. Для MVP это осознанное ограничение.

## 7. Проверка после деплоя

```powershell
# Публичный список
curl https://made4love.ru/api/Events/public

# Sitemap событий
curl https://made4love.ru/events-sitemap.xml

# Админ-API с ключом
curl -H "X-Admin-Key: <ключ>" https://made4love.ru/api/Events/admin/sources
```

Если админ-API отвечает `503`, ключ не задан. Если `401` — ключ не совпал.
