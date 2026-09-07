# Архитектура RTSC-Core

RTSC-Core — модульный монолит. Старый RTSC-next используется как источник требований и будущих данных для миграции, но не является зависимостью.

```text
Browser / Admin / MAX webhook
              |
              v
         ASP.NET Core
  Razor Pages + Minimal API
              |
              v
          PostgreSQL
```

## Один runtime

В одном `RTSC.Core` находятся пользовательский интерфейс, `/Admin`, JSON API, авторизация, бизнес-модули, фоновой dispatcher и адаптеры внешних сервисов. Отдельные runtime-сервисы вводятся только при доказанной эксплуатационной необходимости.

## Модули

```text
Features/Auth
Features/Access
Features/Communities
Features/Events
Features/CheckIn
Features/Ratings
Features/Moderation
Features/ExternalAccounts
Features/Notifications

Integrations/Max
Integrations/Vk          # позже
Integrations/Telegram    # позже
```

## UI

Razor Pages + обычный CSS. Node/npm не требуется. JavaScript добавляется только там, где server-rendered формы действительно недостаточны.

## Авторизация и внешние аккаунты

Основная web-сессия — cookie authentication. Мессенджер не является отдельным приложением и не хранит бизнес-состояние RTSC.

`ExternalAccount` содержит только связь RTSC пользователя с внешним provider/user-id. Привязка выполняется одноразовым кодом. Исходный link-code в PostgreSQL не сохраняется — только SHA-256 hash и срок действия.

## Уведомления

Отправка отделена от бизнес-транзакций:

```text
Event registration / moderation / check-in / rating
                       |
                       v
                  Notification
                       |
                       v
             NotificationDelivery
                       |
                       v
          NotificationDispatcherWorker
              /         |          \
            MAX        VK          TG
            now       later       later
```

Если MAX недоступен, основной запрос пользователя уже завершён и его бизнес-данные остаются сохранены. Dispatcher повторяет доставку и фиксирует ошибку для администратора.

## MAX

MAX реализован обычным `HttpClient`. Это намеренно: базовые операции RTSC требуют только HTTP API, webhook и отправку текста, поэтому отдельный prerelease SDK не нужен.

Webhook не принимает решения о мероприятиях. Он только:

- узнаёт внешний MAX user id;
- принимает `/link КОД`;
- обновляет last-seen внешнего аккаунта;
- передаёт привязку в `ExternalLinkService`.

## Права

Глобальные роли: `User`, `Admin`, `SuperAdmin`.

Внутри сообщества: `Owner`, `Admin`, `Member`. Управление мероприятием наследуется от прав управления его сообществом. Исполнитель мероприятия — отдельная связь `EventPerformer`, а не глобальная роль пользователя.

## QR check-in

QR содержит защищённый Data Protection token с `eventId`, `userId` и сроком действия. Ключи Data Protection вынесены в постоянный Docker volume, поэтому QR не перестаёт работать после обычного рестарта контейнера. Повторное сканирование уже посещённого участника не создаёт повторное уведомление.

## БД

PostgreSQL + EF Core. Свежая установка создаётся через `db/init/001_initial.sql`. Для перехода v0.2→v0.3 есть отдельный `db/migrations/002_messaging.sql`.

После первого стабильного CI/build схему лучше перевести на штатные EF Core migrations и прекратить ручное расширение bootstrap SQL.
