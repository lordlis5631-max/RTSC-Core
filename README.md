# RTSC-Core

RTSC-Core — новый RTSC, переписанный с нуля как простой модульный монолит. Текущая версия: **0.7.0**.

## Архитектура

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

Один ASP.NET Core runtime, одна PostgreSQL, одна админка. Нет отдельных WASM-host'ов, обязательной npm/Tailwind-сборки, dev reverse proxy или отдельного микросервиса на каждый мессенджер.

## Что уже реализовано

### Пользователи и доступ
- регистрация и вход;
- cookie authentication;
- глобальные роли `User / Admin / SuperAdmin`;
- блокировка пользователей;
- bootstrap `SuperAdmin`;
- профиль пользователя;
- `ExternalAccount` для MAX/Telegram/VK;
- одноразовые безопасные коды привязки внешних аккаунтов.

### Сообщества
- создание, редактирование и модерация;
- роли `Owner / Admin / Member`;
- управление администраторами сообщества через UI;
- назначение администратора по email;
- защита владельца от случайного удаления/понижения.

### Мероприятия
- создание, редактирование и модерация;
- публичная афиша;
- поиск и фильтры по тексту, сообществу, месту и датам;
- публичный календарь;
- публичная карта мероприятий;
- необязательные координаты `Latitude / Longitude` с проверкой диапазонов;
- вместимость и окно регистрации;
- регистрация и отмена участия;
- управление участниками;
- назначение исполнителей;
- персональный подписанный QR-билет;
- check-in организатором;
- завершение мероприятия;
- CSV-экспорт участников;
- аналитика конкретного мероприятия: воронка, явка, отмены, заполнение лимита, рейтинги, регистрации по дням.

### Рейтинги и модерация
- оценка исполнителей участниками;
- рейтинг исполнителя по последним 20 мероприятиям;
- оценка участника организатором;
- комментарии к оценкам;
- первичная фильтрация риск-слов;
- ручная модерация комментариев.

### Уведомления и мессенджеры
- web-история уведомлений;
- единая очередь `NotificationDelivery` с retry/dead-state;
- MAX через обычный `HttpClient`;
- MAX webhook;
- Telegram Bot API sender + webhook;
- VK Callback API + `messages.send`;
- автоматические напоминания до 24 часов и до 2 часов до мероприятия;
- мониторинг ошибок доставки и ручной retry.

### Администрирование и отчёты
- единая `/Admin` панель;
- модерация сообществ, мероприятий и комментариев;
- управление пользователями;
- административная отчётность;
- статистика регистраций и посещаемости;
- список популярных мероприятий.

### Эксплуатация
- Dockerfile + Docker Compose;
- PostgreSQL bootstrap schema;
- upgrade SQL migrations;
- постоянный volume для Data Protection keys;
- GitHub Actions CI;
- smoke-check script.

## Основные страницы

```text
/Events                         афиша + поиск и фильтры
/Events/Calendar                календарь
/Events/Map                     карта
/Events/{id}                    карточка мероприятия
/Events/{id}/Manage             управление
/Events/{id}/Analytics          аналитика мероприятия
/Communities                    сообщества
/Profile                        профиль и внешние аккаунты
/Admin                          админ-панель
/Admin/Reports                  общая отчётность
/Admin/Notifications            очередь доставок
/Admin/Integrations             интеграции
```

## Структура проекта

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
│   │   ├── Ratings/
│   │   ├── Reminders/
│   │   └── Reports/
│   ├── Integrations/
│   │   ├── Max/
│   │   ├── Telegram/
│   │   └── Vk/
│   ├── Pages/
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

Минимальные переменные:

```env
POSTGRES_PASSWORD=change-me
BOOTSTRAP_ADMIN_EMAIL=admin@example.com
BOOTSTRAP_ADMIN_PASSWORD=strong-password
BOOTSTRAP_ADMIN_NAME=Super Admin
```

Запуск:

```bash
docker compose up -d --build
./scripts/smoke.sh
```

По умолчанию приложение доступно на `http://localhost:8080`.

## Обновление существующей базы

Последовательно применяйте upgrade scripts, если база создана на более раннем snapshot:

```bash
psql "$CONNECTION_STRING" -f db/migrations/002_messaging.sql
psql "$CONNECTION_STRING" -f db/migrations/003_integrations_reminders.sql
psql "$CONNECTION_STRING" -f db/migrations/004_event_location.sql
```

Для свежей установки отдельные migration scripts не нужны: `db/init/001_initial.sql` содержит актуальную bootstrap schema.

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

Бизнес-операция не делает сетевой вызов в мессенджер напрямую. Она сохраняет уведомление и delivery в PostgreSQL, а фоновый worker выполняет доставку с retry. Недоступность MAX/VK/Telegram не должна ломать регистрацию, check-in или модерацию.

## CI

Workflow `.github/workflows/ci.yml` выполняет:

```text
dotnet restore
dotnet build -c Release
```

Начиная с импорта проекта в GitHub, изменения проходят реальный .NET 10 build в Pull Request перед merge в `main`.

## Следующая очередь разработки

1. перенос данных из RTSC-next;
2. переход от bootstrap SQL к штатным EF Core migrations;
3. категории/теги мероприятий и более точная персонализация афиши;
4. дополнительные отчёты и экспорт;
5. production hardening: rate limiting, antiforgery strategy для cookie-auth API, security headers и тесты.
