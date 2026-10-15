#!/usr/bin/env bash
# Generates a client project from this template, non-interactively. Used by the init-project skill; usable without AI.
# Usage: eng/init/new-project.sh --name Contoso.Orders [--output ../Contoso.Orders] [--authorization AppRoles]
#        [--aspire true] [--rate-limiting true] [--tests true] [--remote <git url>]
set -euo pipefail

name=""
output=""
authorization="AppRoles"
aspire="true"
rate_limiting="true"
tests="true"
remote=""

usage() {
  sed -n '3,4p' "$0" >&2
  exit "${1:-2}"
}

need_value() {
  [ "$2" -ge 2 ] || { echo "new-project: $1 needs a value" >&2; usage; }
}

while [ $# -gt 0 ]; do
  case "$1" in
    --name) need_value "$1" $#; name="${2:-}"; shift 2 ;;
    --output) need_value "$1" $#; output="${2:-}"; shift 2 ;;
    --authorization) need_value "$1" $#; authorization="${2:-}"; shift 2 ;;
    --aspire) need_value "$1" $#; aspire="${2:-}"; shift 2 ;;
    --rate-limiting) need_value "$1" $#; rate_limiting="${2:-}"; shift 2 ;;
    --tests) need_value "$1" $#; tests="${2:-}"; shift 2 ;;
    --remote) need_value "$1" $#; remote="${2:-}"; shift 2 ;;
    -h|--help) sed -n '3,4p' "$0"; exit 0 ;;
    *) echo "new-project: unknown option '$1'" >&2; usage ;;
  esac
done

[ -n "$name" ] || { echo "new-project: --name is required" >&2; usage; }
if ! [[ "$name" =~ ^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)*$ ]]; then
  echo "new-project: invalid name '$name' (expected PascalCase segments, e.g. Contoso.Orders)" >&2
  exit 2
fi
case "$authorization" in
  Authenticated|AppRoles|Groups|AppRolesAndGroups) ;;
  *) echo "new-project: invalid --authorization '$authorization'" >&2; exit 2 ;;
esac
for flag in "$aspire" "$rate_limiting" "$tests"; do
  case "$flag" in
    true|false) ;;
    *) echo "new-project: --aspire, --rate-limiting and --tests take true or false" >&2; exit 2 ;;
  esac
done

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
output="${output:-$root/../$name}"
if [ -e "$output" ] && [ -n "$(ls -A "$output" 2>/dev/null)" ]; then
  echo "new-project: '$output' already exists and is not empty; nothing was changed" >&2
  exit 1
fi

dotnet new install "$root" --force >/dev/null
dotnet new templatewebcqrs \
  --name "$name" \
  --output "$output" \
  --AuthorizationModel "$authorization" \
  --EnableAspire "$aspire" \
  --EnableRateLimiting "$rate_limiting" \
  --EnableTests "$tests"

git -C "$output" init --quiet
if [ -n "$remote" ]; then
  git -C "$output" remote add origin "$remote"
fi

echo "new-project: created $output"
