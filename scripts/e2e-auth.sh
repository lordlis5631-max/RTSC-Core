#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"
E2E_EMAIL="${E2E_EMAIL:?E2E_EMAIL is required}"
E2E_PASSWORD="${E2E_PASSWORD:?E2E_PASSWORD is required}"
COOKIE_JAR="$(mktemp)"
BODY_FILE="$(mktemp)"
trap 'rm -f "$COOKIE_JAR" "$BODY_FILE"' EXIT

request_expect() {
  local expected="$1"
  shift
  local code
  code="$(curl --silent --show-error --output "$BODY_FILE" --write-out '%{http_code}' "$@")"
  if [[ "$code" != "$expected" ]]; then
    echo "Expected HTTP $expected, got $code" >&2
    cat "$BODY_FILE" >&2
    exit 1
  fi
}

get_csrf() {
  local response
  response="$(curl --fail --silent --show-error --cookie "$COOKIE_JAR" --cookie-jar "$COOKIE_JAR" "$BASE_URL/api/security/csrf")"
  CSRF_RESPONSE="$response" python3 - <<'PY'
import json
import os
payload = json.loads(os.environ["CSRF_RESPONSE"])
token = payload.get("requestToken")
if not token:
    raise SystemExit("requestToken missing from CSRF response")
print(token)
PY
}

printf 'E2E: register\n'
csrf="$(get_csrf)"
request_expect 201 --cookie "$COOKIE_JAR" --cookie-jar "$COOKIE_JAR" --header 'Content-Type: application/json' --header "X-CSRF-TOKEN: $csrf" --data "{\"displayName\":\"E2E User\",\"email\":\"$E2E_EMAIL\",\"password\":\"$E2E_PASSWORD\"}" "$BASE_URL/api/auth/register"

printf 'E2E: login\n'
csrf="$(get_csrf)"
request_expect 200 --cookie "$COOKIE_JAR" --cookie-jar "$COOKIE_JAR" --header 'Content-Type: application/json' --header "X-CSRF-TOKEN: $csrf" --data "{\"email\":\"$E2E_EMAIL\",\"password\":\"$E2E_PASSWORD\"}" "$BASE_URL/api/auth/login"

printf 'E2E: authenticated session\n'
request_expect 200 --cookie "$COOKIE_JAR" "$BASE_URL/api/auth/me"
if ! grep -q "$E2E_EMAIL" "$BODY_FILE"; then
  echo "Authenticated user payload does not contain expected email" >&2
  cat "$BODY_FILE" >&2
  exit 1
fi

printf 'E2E: authenticated CSRF-protected database write\n'
csrf="$(get_csrf)"
request_expect 201 --cookie "$COOKIE_JAR" --cookie-jar "$COOKIE_JAR" --header 'Content-Type: application/json' --header "X-CSRF-TOKEN: $csrf" --data '{"name":"E2E Community","description":"GitHub Actions verification","logoUrl":null}' "$BASE_URL/api/communities/"
if ! grep -q 'E2E Community' "$BODY_FILE"; then
  echo "Community creation response is unexpected" >&2
  cat "$BODY_FILE" >&2
  exit 1
fi

printf 'E2E: logout\n'
csrf="$(get_csrf)"
request_expect 204 --request POST --cookie "$COOKIE_JAR" --cookie-jar "$COOKIE_JAR" --header "X-CSRF-TOKEN: $csrf" "$BASE_URL/api/auth/logout"

printf 'E2E: session is gone\n'
request_expect 401 --cookie "$COOKIE_JAR" "$BASE_URL/api/auth/me"

echo "Auth/database E2E checks passed"
