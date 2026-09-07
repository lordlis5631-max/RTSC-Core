#!/usr/bin/env bash
set -euo pipefail
BASE_URL="${BASE_URL:-http://localhost:8080}"

printf 'Checking liveness\n'
curl --fail --silent --show-error "$BASE_URL/health/live"
echo

printf 'Checking readiness\n'
curl --fail --silent --show-error "$BASE_URL/health/ready"
echo

printf 'Checking system info\n'
info="$(curl --fail --silent --show-error "$BASE_URL/api/system/info")"
echo "$info"
if ! grep -q '"version":"1.0.0"' <<<"${info// /}"; then
  echo "Unexpected RTSC-Core version" >&2
  exit 1
fi

printf 'Checking CSRF bootstrap endpoint\n'
csrf="$(curl --fail --silent --show-error --cookie-jar /tmp/rtsc-cookies.txt "$BASE_URL/api/security/csrf")"
if ! grep -q '"requestToken"' <<<"${csrf// /}"; then
  echo "CSRF token endpoint is not working" >&2
  exit 1
fi

echo "Checking public pages"
for path in / /Events /Communities /Login /Register; do
  headers="$(mktemp)"
  code="$(curl --silent --dump-header "$headers" --output /dev/null --write-out '%{http_code}' "$BASE_URL$path")"
  if [[ "$code" != "200" ]]; then
    echo "$path returned HTTP $code" >&2
    rm -f "$headers"
    exit 1
  fi
  if ! grep -qi '^X-Content-Type-Options: nosniff' "$headers"; then
    echo "$path is missing X-Content-Type-Options" >&2
    rm -f "$headers"
    exit 1
  fi
  if ! grep -qi '^Content-Security-Policy:' "$headers"; then
    echo "$path is missing Content-Security-Policy" >&2
    rm -f "$headers"
    exit 1
  fi
  rm -f "$headers"
  echo "$path -> $code"
done

rm -f /tmp/rtsc-cookies.txt
echo "Smoke checks passed"
