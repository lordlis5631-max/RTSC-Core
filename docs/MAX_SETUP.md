# Настройка MAX в RTSC-Core

## 1. Переменные окружения

В `.env` заполните:

```env
PUBLIC_BASE_URL=https://rtsc.example.ru
MAX_BOT_TOKEN=your-bot-token
MAX_WEBHOOK_SECRET=long-random-secret
MAX_WEBHOOK_URL=https://rtsc.example.ru/api/integrations/max/webhook
MAX_BOT_URL=https://max.ru/your-bot
```

`PUBLIC_BASE_URL` нужен, чтобы относительные ссылки RTSC превращались в полные HTTPS-ссылки для сообщения в MAX.

RTSC-Core намеренно требует для `MAX_WEBHOOK_SECRET` 16–256 символов из `A-Z`, `a-z`, `0-9`, `_`, `-` (это строже минимального требования MAX).

## 2. Требования к endpoint

Production webhook должен быть доступен снаружи по HTTPS. Маршрут RTSC:

```text
POST /api/integrations/max/webhook
```

RTSC проверяет `X-Max-Bot-Api-Secret`, если `MAX_WEBHOOK_SECRET` настроен.

## 3. Зарегистрировать webhook

После запуска войдите SuperAdmin/Admin и откройте:

```text
/Admin/Integrations
```

Нажмите «Зарегистрировать webhook в MAX».

RTSC отправит запрос в MAX `/subscriptions` для событий:

```text
message_created
bot_started
```

## 4. Привязать пользователя

Пользователь:

1. входит в RTSC;
2. открывает `/Profile`;
3. нажимает «Подключить MAX»;
4. получает одноразовый код;
5. пишет боту `/link КОД`.

Код действует 15 минут и после успешного использования больше не принимается.

## 5. Проверить доставку

После регистрации на мероприятие или другой операции, создающей уведомление:

- уведомление должно появиться в `/Profile`;
- MAX delivery должен появиться в `/Admin/Notifications`;
- при успехе статус станет `Sent`;
- временная ошибка станет `Failed` и будет автоматически повторена;
- после 5 неуспешных попыток — `Dead`;
- администратор может нажать «Повторить».

## 6. Диагностика

Проверьте:

```text
/health
/Admin/Integrations
/Admin/Notifications
```

Если MAX API недоступен, это не должно отменять регистрацию, check-in, оценку или модерацию в RTSC: delivery обрабатывается отдельно фоновым worker.

## 7. Перед production

Обязательно:

- используйте длинный случайный `MAX_WEBHOOK_SECRET`;
- не коммитьте `.env`;
- обеспечьте HTTPS reverse proxy;
- проверьте доверие системного CA store сертификатам, необходимым для обращения к MAX API;
- прогоните `dotnet build`/CI;
- протестируйте реальный `bot_started`, `/link` и тестовую доставку на отдельном MAX-пользователе.
