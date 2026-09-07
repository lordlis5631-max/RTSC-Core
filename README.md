# RTSC-Core

Новый RTSC, переписанный с нуля как простой модульный монолит. Текущий snapshot: **0.4.0**.

## Зачем отдельный проект

RTSC-Core не зависит от RTSC-next. Старый проект остаётся рабочей системой и источником требований/данных для миграции. Новый проект не переносит историческую архитектурную сложность: отдельные WASM host'ы, SharedWasm/SharedWeb, Material3 wrapper, обязательную npm/Tailwind сборку и dev reverse proxy.

```text
Browser / Admin / MAX / Telegram / VK
          |
          v
      RTSC.Core
Razor Pages + API + Auth
          |
          v
      PostgreSQL
```

## Что уже работает в коде

- регистрация и вход;
- cookie authentication;
- `User`, глобальные роли и блокировка;
- bootstrap `SuperAdmin`;
- профиль пользователя;
- `ExternalAccount` как единая модель связанного мессенджера;
- безопасный одноразовый код привязки MAX/Telegram/VK (в БД хранится только SHA-256 hash);
- создание/редактирование и модерация сообществ;
- владелец и администратор сообщества;
- создание/редактирование и модерация мероприятий;
- публичная афиша;
- регистрация и отмена участия;
- вместимость и окно регистрации;
- управление участниками;
- назначение исполнителей;
- персональный подписанный QR-билет;
- check-in организатором через обычную камеру телефона;
- завершение мероприятия;
- оценка исполнителей участниками;
- рейтинг исполнителя по последним 20 мероприятиям;
- оценка участника организатором;
- комментарии к оценкам и ручная модерация;
- web-история уведомлений;
- единая очередь `NotificationDelivery` с retry и dead-state;
- MAX sender через обычный `HttpClient`, без prerelease SDK;
- MAX webhook для `/link КОД` и `bot_started`;
- Telegram Bot API sender + webhook для `/link КОД` и `/start КОД`;
- VK Callback API + sender через `messages.send`;
- автоматические напоминания до 24 часов и до 2 часов до мероприятия;
- мониторинг доставок и ручной retry в `/Admin/Notifications`;
- настройка MAX webhook в `/Admin/Integrations`;
- единая `/Admin` панель;
- Dockerfile + Docker Compose;
- PostgreSQL bootstrap schema + upgrade SQL `v0.2 -> v0.3 -> v0.4`;
- постоянный volume для Data Protection keys;
- GitHub Actions CI;
- smoke-check script.

Полная карта: `docs/FUNCTIONAL_MAP.md`. Настройки: `docs/MAX_SETUP.md`, `docs/TELEGRAM_SETUP.md`, `docs/VK_SETUP.md`.

## Структура

```text
RTSC-Core/
├── RTSC.Core/
│   ├── Data/
│   ├── Domain/
│   ├── Features/
│   │   ├── Access/
│   │   ├── Auth/
│   │   ├── CheckIn/
│   │   ├── Communities/
│   │   ├── Events/
│   │   ├── ExternalAccounts/
│   │   ├── Moderation/
│   │   ├── Notifications/
│   │   └── Ratings/
│   ├── Integrations/
│   │   ├── Max/
│   │   ├── Telegram/
│   │   └── Vk/
│   ├── Pages/
│   │   ├── Admin/
│   │   ├── CheckIn/
│   │   ├── Communities/
│   │   ├── Events/
│   │   ├── Profile/
│   │   └── Ratings/
│   ├── Program.cs
│   └── RTSC.Core.csproj
├── db/init/
├── db/migrations/
├── docs/
├── scripts/
├── .github/workflows/ci.yml
└── compose.yml
```

## Запуск через Docker

```bash
cp .env.example .env
```

Минимум:

```env
POSTGRES_PASSWORD=change-me
BOOTSTRAP_ADMIN_EMAIL=admin@example.com
BOOTSTRAP_ADMIN_PASSWORD=strong-password
BOOTSTRAP_ADMIN_NAME=Super Admin
```

Для мессенджеров дополнительно используйте переменные из `.env.example`. Подробные инструкции находятся в `docs/MAX_SETUP.md`, `docs/TELEGRAM_SETUP.md` и `docs/VK_SETUP.md`.

Запуск:

```bash
docker compose up -d --build
./scripts/smoke.sh
```

По умолчанию приложение доступно на `http://localhost:8080`.

## Обновление базы

Для существующей базы v0.2 перед запуском нового приложения выполните:

```bash
psql "$CONNECTION_STRING" -f db/migrations/002_messaging.sql
```

Миграция специально останавливается с понятной ошибкой, если в `external_accounts` уже есть несколько записей одного провайдера для одного пользователя. Сначала устраните такие дубли, затем повторите migration script.

После обновления v0.3 -> v0.4 выполните:

```bash
psql "$CONNECTION_STRING" -f db/migrations/003_integrations_reminders.sql
```

Для свежей установки ничего отдельно запускать не нужно: `db/init/001_initial.sql` уже содержит v0.4 schema.

## Как устроены уведомления

```text
Business action
     |
     v
 Notification  ----------> Web profile history
     |
     +--> NotificationDelivery(MAX) --> dispatcher --> MAX API
     |
     +--> NotificationDelivery(VK)  --> dispatcher --> VK API
     |
     +--> NotificationDelivery(TG)  --> dispatcher --> Telegram Bot API
```

Бизнес-операция не отправляет запрос в мессенджер напрямую. Она записывает уведомление и delivery в PostgreSQL. Фоновый worker делает доставку, повторяет временные ошибки до 5 раз и переводит окончательно неуспешную доставку в `Dead`. Поэтому недоступность MAX не должна ломать регистрацию, check-in или модерацию мероприятия.

## Основной пользовательский сценарий

1. Пользователь регистрируется.
2. Создаёт сообщество и отправляет его на модерацию.
3. Администратор публикует сообщество.
4. Владелец создаёт мероприятие и назначает исполнителей.
5. Администратор публикует мероприятие.
6. Участник регистрируется и получает web-уведомление; при подключённом MAX/Telegram/VK создаётся delivery.
7. Участник показывает QR-билет.
8. Организатор сканирует его обычной камерой телефона.
9. RTSC проверяет подпись QR и права организатора, отмечает `Attended` и уведомляет участника.
10. После завершения мероприятия участник оценивает исполнителей, а организатор — участников.
11. Комментарии с риск-словами попадают на модерацию.

## Привязка MAX

1. Авторизованный пользователь открывает `/Profile`.
2. Нажимает «Подключить MAX».
3. RTSC генерирует одноразовый код на 15 минут; в БД сохраняется только его hash.
4. Пользователь отправляет боту `/link КОД`.
5. MAX webhook передаёт сообщение RTSC.
6. RTSC связывает `ExternalAccount(Max)` с текущим web-пользователем.
7. Последующие системные уведомления могут доставляться в MAX.

## CI и текущее ограничение

Workflow `.github/workflows/ci.yml` выполняет `dotnet restore` и `dotnet build` для .NET 10.

В текущей рабочей среде нет .NET SDK/Docker CLI и внешний DNS недоступен, поэтому этот snapshot **не был фактически собран `dotnet build` локально**. Перед production deployment обязательно прогоните GitHub Actions или локальный .NET 10 build. Это ограничение не скрывается.

## Следующая очередь разработки

1. календарь и карта мероприятий;
2. экспорт участников и отчёты;
3. управление ролями/составом сообществ через UI;
4. перенос данных из RTSC-next;
5. переход от SQL bootstrap к штатным EF Core migrations после первого стабильного build.
