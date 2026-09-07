# VK setup — RTSC-Core v0.4

VK подключён через Callback API и `messages.send`. Сторонний SDK не требуется.

## Переменные окружения

```env
PUBLIC_BASE_URL=https://rtsc.example.ru
VK_BOT_TOKEN=community-access-token
VK_GROUP_ID=123456789
VK_API_VERSION=5.199
VK_CALLBACK_SECRET=replace-with-long-random-secret
VK_CONFIRMATION_CODE=confirmation-code-from-vk
VK_CALLBACK_URL=https://rtsc.example.ru/api/integrations/vk/callback
VK_BOT_URL=https://vk.me/your_community
```

Версия API вынесена в `VK_API_VERSION`, поэтому её можно обновлять конфигурацией без изменения кода.

## Callback API

В настройках сообщества VK добавьте Callback-сервер:

```text
https://rtsc.example.ru/api/integrations/vk/callback
```

Укажите такой же secret, как в `VK_CALLBACK_SECRET`. Confirmation code из интерфейса VK поместите в `VK_CONFIRMATION_CODE`.

RTSC обрабатывает:

- `confirmation` — возвращает confirmation code;
- `message_new` — принимает `/link КОД`;
- остальные события — возвращает `ok` без бизнес-обработки.

## Привязка пользователя

1. Пользователь открывает `/Profile`.
2. Нажимает «Подключить VK».
3. Получает одноразовый код на 15 минут.
4. Отправляет сообществу `/link КОД`.
5. RTSC создаёт `ExternalAccount(Vk)`.
6. Уведомления доставляются тем же dispatcher, что MAX и Telegram.
