#!/usr/bin/env bash
# Generates a project from this template with the given options, then checks, builds and tests it.
# Usage: eng/verify-template.sh [AuthorizationModel] [EnableAspire] [EnableRateLimiting] [EnableTests]
set -euo pipefail

auth="${1:-AppRoles}"
aspire="${2:-true}"
rate_limiting="${3:-true}"
tests="${4:-true}"

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "==> Generate AuthorizationModel=$auth EnableAspire=$aspire EnableRateLimiting=$rate_limiting EnableTests=$tests"
dotnet new install "$root" --force
dotnet new templatewebcqrs \
  --name Ci.Generated \
  --output "$work/Ci.Generated" \
  --AuthorizationModel "$auth" \
  --EnableAspire "$aspire" \
  --EnableRateLimiting "$rate_limiting" \
  --EnableTests "$tests"

cd "$work/Ci.Generated"

fail() {
  echo "verify-template: $1" >&2
  exit 1
}

for excluded in .idea .superpowers eng docs Directory.Build.props .github/workflows/template-ci.yml src/Ci.Generated.AppHost/RealmVariants; do
  [ ! -e "$excluded" ] || fail "'$excluded' leaked into the generated project."
done
if find . \( -name bin -o -name obj \) -type d | grep -q .; then
  fail "bin/obj folders leaked into the generated project."
fi
if grep -rq "Web.Template.CQRS" --include='*.cs' --include='*.csproj' --include='*.slnx' --include='*.json' .; then
  fail "the source name 'Web.Template.CQRS' was not replaced everywhere."
fi
if [ "$tests" = "false" ] && [ -e tests ]; then
  fail "tests/ must not be generated when EnableTests=false."
fi
if [ "$aspire" = "true" ] && [ ! -f src/Ci.Generated.AppHost/Realms/template-realm.json ]; then
  fail "the Keycloak realm is missing."
fi

dotnet build Ci.Generated.slnx
if [ "$tests" = "true" ]; then
  dotnet test --solution Ci.Generated.slnx
fi
echo "verify-template: OK ($auth, aspire=$aspire, rateLimiting=$rate_limiting, tests=$tests)"
