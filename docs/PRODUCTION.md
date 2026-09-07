# RTSC-Core v1.0 — production checklist

## 1. HTTPS и reverse proxy

Production cookie имеют `Secure` и `__Host-` prefix. Публичный трафик должен приходить только по HTTPS через reverse proxy (Nginx, Caddy, Traefik или ingress). Не публикуйте порт PostgreSQL наружу.

Рекомендуемая схема:

```text
Internet -> HTTPS reverse proxy -> RTSC.Core:8080 -> PostgreSQL
```

## 2. Секреты

Не храните реальные токены и пароли в git. До запуска задайте как минимум:

- `POSTGRES_PASSWORD`;
- bootstrap admin email/password только для первого создания администратора;
- `MAX_BOT_TOKEN` и `MAX_WEBHOOK_SECRET`, если MAX включён;
- `TELEGRAM_BOT_TOKEN` и `TELEGRAM_WEBHOOK_SECRET`, если Telegram включён;
- `VK_BOT_TOKEN` и `VK_CALLBACK_SECRET`, если VK включён;
- `PUBLIC_BASE_URL` с HTTPS URL приложения.

После создания первого администратора bootstrap password рекомендуется убрать из окружения.

## 3. Data Protection

Volume `rtsc-keys` должен быть постоянным. Потеря ключей инвалидирует cookie-сессии и подписанные данные, включая QR/check-in токены.

В backup policy включайте и PostgreSQL, и Data Protection keys.

## 4. Обновление базы

Для базы, созданной до v0.9, последовательно примените migration scripts до `005_personalization.sql`. v1.0 не меняет схему БД.

Всегда делайте backup перед migration.

## 5. Health probes

- `GET /health/live` — процесс жив; БД не проверяется.
- `GET /health/ready` — приложение готово обслуживать трафик и может подключиться к PostgreSQL.
- `GET /health` — alias readiness для обратной совместимости.

Load balancer/orchestrator должен использовать `/health/ready` для readiness и `/health/live` для liveness.

## 6. CSRF для JSON API

Razor Pages используют штатную antiforgery-защиту. Для mutating JSON API (`POST`, `PUT`, `PATCH`, `DELETE`) клиент должен:

1. вызвать `GET /api/security/csrf` с сохранением cookie;
2. взять `requestToken` из JSON;
3. передать его в `X-CSRF-TOKEN` вместе с cookie в mutating запросе.

Webhook endpoints MAX/Telegram/VK исключены из browser antiforgery, потому что проверяют собственные секреты.

## 7. Rate limiting

Встроены отдельные fixed-window профили:

- auth: 12 запросов/мин на клиента;
- webhook: 300/мин;
- API: 180/мин;
- обычный web: 360/мин;
- health: 600/мин.

При превышении возвращается HTTP 429 с `Retry-After`.

## 8. Security headers

Приложение выставляет CSP, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` и `Cross-Origin-Opener-Policy`. Kestrel server header отключён.

Если в будущем добавляется новый внешний JS/CSS/image provider, сначала обновите CSP осознанно, а не включайте `*` или `unsafe-inline` для scripts.

## 9. CI gate

Перед merge выполняются:

```text
dotnet restore
dotnet build -c Release
dotnet test
dotnet package list --vulnerable --include-transitive
```

NuGet audit warnings `NU1901..NU1904` считаются ошибками.

## 10. Smoke check

После deploy:

```bash
BASE_URL=https://your-rtsc.example ./scripts/smoke.sh
```

Smoke script проверяет liveness, readiness, version, CSRF bootstrap endpoint, основные публичные страницы и security headers.
