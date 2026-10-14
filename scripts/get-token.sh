#!/usr/bin/env bash
# Prints an access token for a local Keycloak persona (local development only).
# Usage: scripts/get-token.sh <persona> [password]
#   KEYCLOAK_URL (default https://localhost:8080), KEYCLOAK_REALM (default template), KEYCLOAK_CLIENT_ID (default template-cli)
set -euo pipefail

persona="${1:?Usage: scripts/get-token.sh <persona> [password]}"
password="${2:-Passw0rd!}"
keycloak_url="${KEYCLOAK_URL:-https://localhost:8080}"
realm="${KEYCLOAK_REALM:-template}"
client_id="${KEYCLOAK_CLIENT_ID:-template-cli}"

case "$persona" in
  *@*) username="$persona" ;;
  *) username="$persona@local.test" ;;
esac

if ! response="$(curl --silent --show-error --fail-with-body \
  --data-urlencode "grant_type=password" \
  --data-urlencode "client_id=$client_id" \
  --data-urlencode "username=$username" \
  --data-urlencode "password=$password" \
  --data-urlencode "scope=openid" \
  "$keycloak_url/realms/$realm/protocol/openid-connect/token")"; then
  echo "get-token: unable to get a token for '$username' from $keycloak_url (realm '$realm'): $response" >&2
  exit 1
fi

token="$(printf '%s' "$response" | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')"
if [ -z "$token" ]; then
  echo "get-token: no access_token in Keycloak response: $response" >&2
  exit 1
fi
printf '%s\n' "$token"
