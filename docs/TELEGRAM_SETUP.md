# Telegram setup — RTSC-Core v0.4

Telegram используется только как шлюз привязки аккаунта и доставки уведомлений. Основная работа пользователя остаётся в web RTSC-Core.

## Переменные окружения

```env
PUBLIC_BASE_URL=https://rtsc.example.ru
TELEGRAM_BOT_TOKEN=123456:token
TELEGRAM_WEBHOOK_SECRET=replace-with-long-random-secret
TELEGRAM_WEBHOOK_URL=https://rtsc.example.ru/api/integrations/telegram/webhook
TELEGRAM_BOT_URL=https://t.me/your_bot
```

`TELEGRAM_WEBHOOK_SECRET` в RTSC должен содержать 16–256 символов `A-Z a-z 0-9 _ -`.

## Включение webhook

1. Запустите RTSC-Core с HTTPS-публичным адресом.
2. Войдите под Admin/SuperAdmin.
3. Откройте `/Admin/Integrations`.
4. Нажмите «Зарегистрировать webhook Telegram».
5. RTSC вызовет Bot API `setWebhook` с `secret_token` и `allowed_updates=["message"]`.

Входящий endpoint проверяет `X-Telegram-Bot-Api-Secret-Token` и при неверном secret отвечает 401.

## Привязка пользователя

1. Пользователь открывает `/Profile`.
2. Нажимает «Подключить Telegram».
3. Получает одноразовый код на 15 минут.
4. Отправляет боту `/link КОД` или `/start КОД`.
5. RTSC создаёт `ExternalAccount(Telegram)`.
6. Уведомления дальше идут через общую `NotificationDelivery` очередь.
