# Changelog

## 0.4.0 — Telegram + VK + event reminders

- добавлен Telegram sender через Bot API без стороннего SDK;
- добавлен Telegram webhook с secret header;
- добавлены `/link КОД` и `/start КОД` для Telegram;
- добавлена настройка Telegram webhook из `/Admin/Integrations`;
- добавлен VK sender через `messages.send`;
- добавлен VK Callback endpoint с `confirmation` и `message_new`;
- добавлена привязка VK через `/link КОД`;
- профиль теперь создаёт link-code для MAX, Telegram и VK;
- все три канала используют общие `ExternalAccount`, `Notification` и `NotificationDelivery`;
- добавлен `EventReminderWorker`;
- добавлены идемпотентные напоминания до 24 часов и до 2 часов;
- добавлена таблица `event_reminders` и migration `003_integrations_reminders.sql`;
- расширены Docker/.env настройки для Telegram и VK;
- добавлены `docs/TELEGRAM_SETUP.md` и `docs/VK_SETUP.md`.

## 0.3.0 — External accounts + notifications + MAX

- добавлена страница профиля;
- `ExternalAccount` стал общей моделью для мессенджеров;
- добавлены одноразовые link-codes с SHA-256 hash и TTL 15 минут;
- добавлена web-история уведомлений;
- добавлены `NotificationDelivery` и фоновый dispatcher;
- retry до 5 попыток, backoff, `Dead` и восстановление зависших доставок;
- добавлена админ-диагностика доставок и ручной retry;
- MAX реализован через обычный `HttpClient` без prerelease bot SDK;
- добавлен MAX webhook с проверкой secret;
- добавлены `/link КОД` и обработка `bot_started`;
- добавлена страница `/Admin/Integrations` для регистрации MAX webhook;
- уведомления подключены к регистрации, отмене участия, статусам, check-in, завершению, назначению исполнителя, рейтингам и модерации;
- повторный QR check-in не создаёт дублирующее уведомление;
- добавлен upgrade script `db/migrations/002_messaging.sql`;
- upgrade script диагностирует дубли внешних аккаунтов до добавления уникального индекса;
- добавлена документация `docs/MAX_SETUP.md`.

## 0.2.0 — Core event lifecycle

- добавлены права управления сообществом и мероприятием;
- формы создания и редактирования сообществ;
- формы создания и редактирования мероприятий;
- модерация сообществ и мероприятий;
- управление участниками и исполнителями;
- регистрация, отмена регистрации и статусы посещения;
- подписанный персональный QR-билет;
- check-in организатором;
- постоянные Data Protection keys в Docker volume;
- завершение мероприятия;
- оценка исполнителей участниками;
- оценка участников организатором;
- публичные профили рейтинга исполнителя и участника;
- комментарии к оценкам и первичная автоматическая модерация;
- административная модерация комментариев;
- управление пользователями в `/Admin`;
- CI workflow и smoke script;
- усилены связи и индексы PostgreSQL bootstrap schema.

## 0.1.0 — Initial skeleton

- один ASP.NET Core проект;
- PostgreSQL + EF Core;
- cookie auth;
- базовые доменные сущности;
- первый `/Admin` dashboard;
- Docker Compose.
