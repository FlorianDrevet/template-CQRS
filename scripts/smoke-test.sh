#!/usr/bin/env bash
# Local smoke test of the Aspire stack: gets tokens for two personas and checks the expected HTTP status codes.
# Usage: scripts/smoke-test.sh
#   API_BASE_URL (default https://localhost:7246), SMOKE_ADMIN (default admin), SMOKE_USER (default user)
set -euo pipefail

api="${API_BASE_URL:-https://localhost:7246}"
admin="${SMOKE_ADMIN:-admin}"
user="${SMOKE_USER:-user}"
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
failures=0

status_of() {
  curl --silent --output /dev/null --write-out '%{http_code}' "$@" || true
}

expect() {
  local description="$1" expected="$2" actual="$3"
  if [ "$actual" = "$expected" ]; then
    echo "OK   $description ($actual)"
  else
    echo "FAIL $description: expected $expected, got $actual"
    failures=$((failures + 1))
  fi
}

admin_token="$(bash "$here/get-token.sh" "$admin")"
user_token="$(bash "$here/get-token.sh" "$user")"

expect "GET /me without token" 401 "$(status_of "$api/me")"
expect "GET /me as $admin" 200 "$(status_of -H "Authorization: Bearer $admin_token" "$api/me")"
expect "GET /me as $user" 200 "$(status_of -H "Authorization: Bearer $user_token" "$api/me")"

ping_anonymous="$(status_of "$api/admin/ping")"
if [ "$ping_anonymous" = "404" ]; then
  echo "SKIP /admin/ping (not generated for this authorization model)"
else
  expect "GET /admin/ping without token" 401 "$ping_anonymous"
  expect "GET /admin/ping as $admin" 200 "$(status_of -H "Authorization: Bearer $admin_token" "$api/admin/ping")"
  expect "GET /admin/ping as $user" 403 "$(status_of -H "Authorization: Bearer $user_token" "$api/admin/ping")"
fi

if [ "$failures" -gt 0 ]; then
  echo "smoke-test: $failures check(s) failed" >&2
  exit 1
fi
echo "smoke-test: all checks passed"
