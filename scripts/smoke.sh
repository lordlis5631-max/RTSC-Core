#!/usr/bin/env bash
set -euo pipefail
BASE_URL="${BASE_URL:-http://localhost:8080}"

printf 'Checking %s/health\n' "$BASE_URL"
curl --fail --silent --show-error "$BASE_URL/health"
echo

printf 'Checking system info\n'
info="$(curl --fail --silent --show-error "$BASE_URL/api/system/info")"
echo "$info"
if ! grep -q '"version":"0.4.0"' <<<"${info// /}"; then
  echo "Unexpected RTSC-Core version" >&2
  exit 1
fi

echo "Checking public pages"
for path in / /Events /Communities /Login /Register; do
  code="$(curl --silent --output /dev/null --write-out '%{http_code}' "$BASE_URL$path")"
  if [[ "$code" != "200" ]]; then
    echo "$path returned HTTP $code" >&2
    exit 1
  fi
  echo "$path -> $code"
done
