# RTSC-Core

RTSC-Core — новый RTSC, переписанный с нуля как простой модульный монолит. Текущая версия: **1.0.0**.

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
- роли `User / Admin / SuperAdmin`;
- блокировка пользователей;
- bootstrap `SuperAdmin`;
- `/My` — рабочий личный кабинет;
- профиль и `ExternalAccount` для MAX/Telegram/VK;
- одноразовые безопасные коды привязки;
- усиленная валидация API auth;
- автоматический rehash пароля при необходимости.

### Мой RTSC и персонализация
- ближайшие регистрации и QR-билеты;
- история участия и статистика;
- организаторские задачи и назначения исполнителем;
- `/My/Interests`;
- явный выбор категорий и тегов;
- прозрачная персональная подборка без скрытых признаков;
- объяснение причины рекомендации.

### Сообщества и мероприятия
- сообщества, роли `Owner / Admin / Member`, модерация;
- управление администраторами;
- CRUD и модерация мероприятий;
- категории и до 8 тегов;
- поиск и фильтры;
- календарь и карта;
- регистрация, подтверждение, QR-ticket и check-in;
- исполнители, завершение, CSV-экспорт;
- аналитика регистрации и посещаемости.

### Рейтинги, уведомления и интеграции
- рейтинг исполнителей по последним 20 событиям;
- рейтинг участников организатором;
- комментарии и модерация;
- outbox-style `NotificationDelivery` с retry/dead;
- MAX, Telegram и VK;
- webhook/callback secret validation;
- напоминания за 24 часа и 2 часа.

### Production hardening v1.0
- antiforgery/CSRF для mutating browser API;
- `/api/security/csrf` для JSON-клиентов с cookie auth;
- отдельное исключение для secret-protected MAX/TG/VK webhook;
- per-client rate limiting для auth/API/webhook/web/health;
- secure `__Host-` cookie в Production;
- CSP и security headers;
- отключён Kestrel Server header;
- лимит request body;
- `/health/live` и `/health/ready`;
- unit/regression tests;
- NuGet vulnerability audit как CI gate;
- расширенный smoke-check;
- production checklist: `docs/PRODUCTION.md`.

## Основные страницы

```text
/My                             Мой RTSC + рекомендации
/My/Interests                   категории и теги интересов
/Events                         афиша + поиск и фильтры
/Events/Calendar                календарь
/Events/Map                     карта
/Events/{id}                    карточка мероприятия
/Events/{id}/Manage             управление
/Events/{id}/Analytics          аналитика мероприятия
/Communities                    сообщества
/Profile                        профиль, мессенджеры и уведомления
/Admin                          админ-панель
/Admin/Reports                  общая отчётность
/Admin/Notifications            очередь доставок
/Admin/Integrations             интеграции
```

## Запуск через Docker

```bash
cp .env.example .env
docker compose up -d --build
./scripts/smoke.sh
```

Минимальные переменные:

```env
POSTGRES_PASSWORD=change-me
BOOTSTRAP_ADMIN_EMAIL=admin@example.com
BOOTSTRAP_ADMIN_PASSWORD=strong-password
BOOTSTRAP_ADMIN_NAME=Super Admin
```

Для production используйте HTTPS reverse proxy. Подробный checklist: `docs/PRODUCTION.md`.

## Обновление существующей базы

Для базы, созданной на ранних snapshot, scripts применяются последовательно:

```bash
psql "$CONNECTION_STRING" -f db/migrations/002_messaging.sql
psql "$CONNECTION_STRING" -f db/migrations/003_integrations_reminders.sql
psql "$CONNECTION_STRING" -f db/migrations/004_event_location.sql
psql "$CONNECTION_STRING" -f db/migrations/005_personalization.sql
```

**v1.0 не меняет схему PostgreSQL**, отдельная migration для production-hardening не требуется.

Для свежей установки `db/init/001_initial.sql` содержит актуальную bootstrap schema.

## CSRF для JSON API

Для `POST/PUT/PATCH/DELETE` под `/api` браузерный клиент сначала получает токен:

```text
GET /api/security/csrf
```

затем передаёт `requestToken` в `X-CSRF-TOKEN` вместе с antiforgery cookie. Внешние MAX/Telegram/VK webhook защищаются собственными secret headers/payload secrets и не требуют browser CSRF token.

## Health

```text
GET /health/live   процесс жив
GET /health/ready  PostgreSQL доступен и приложение готово
GET /health        alias readiness
```

## CI

`.github/workflows/ci.yml` выполняет:

```text
dotnet restore RTSC-Core.slnx
dotnet build RTSC-Core.slnx -c Release
dotnet test
dotnet package list --vulnerable --include-transitive
```

NuGet audit предупреждения об известных уязвимостях считаются ошибками.

## Следующая очередь после v1.0

1. production deployment на сервер и проверка smoke/health;
2. перенос данных из RTSC-next;
3. переход от bootstrap SQL к штатным EF Core migrations;
4. управление каталогом тегов через Admin;
5. расширение автоматизированных integration/e2e tests.
