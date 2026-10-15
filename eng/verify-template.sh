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

for excluded in .idea .superpowers eng docs site Directory.Build.props .github/workflows/template-ci.yml .github/workflows/docs.yml mkdocs.yml README.generated.md .git .vs .cache .pytest_cache build src/Ci.Generated.AppHost/RealmVariants; do
  [ ! -e "$excluded" ] || fail "'$excluded' leaked into the generated project."
done
[ -f README.md ] || fail "the generated project has no README.md."
grep -q "Ci.Generated" README.md || fail "README.md is not the generated project's README."
if grep -q "dotnet new install" README.md; then
  fail "README.md is the template repository README."
fi
for required in AGENTS.md CLAUDE.md .github/copilot-instructions.md \
  .claude/skills/add-feature/SKILL.md .claude/skills/manage-access/SKILL.md \
  .claude/agents/reviewer.md .github/agents/reviewer.agent.md; do
  [ -f "$required" ] || fail "harness file '$required' is missing."
done
for leaked in .claude/skills/init-project AGENTS.generated.md eng/init; do
  [ ! -e "$leaked" ] || fail "'$leaked' leaked into the generated project."
done
grep -q "Ci.Generated" AGENTS.md || fail "AGENTS.md is not the generated project's instructions."
if grep -q "init-project" AGENTS.md; then
  fail "AGENTS.md is the template maintainers' file."
fi
for doc in AGENTS.md .github/copilot-instructions.md; do
  if grep -q "<!--#" "$doc"; then
    fail "$doc contains unprocessed template directives."
  fi
done
for entry in .claude/*; do
  case "$(basename "$entry")" in
    skills|agents) ;;
    *) fail "unexpected '$entry' in the generated .claude/ folder." ;;
  esac
done
for skill in .claude/skills/*/SKILL.md; do
  head -1 "$skill" | grep -q '^---$' || fail "$skill has no YAML frontmatter."
  grep -q '^name: ' "$skill" || fail "$skill has no name."
  grep -q '^description: ' "$skill" || fail "$skill has no description."
done
if [ "$aspire" = "true" ]; then
  for required in .claude/skills/run-local/SKILL.md .mcp.json .vscode/mcp.json scripts/smoke-test.sh scripts/smoke-test.ps1; do
    [ -f "$required" ] || fail "harness file '$required' is missing."
  done
  python3 -m json.tool .mcp.json > /dev/null 2>&1 || python -m json.tool .mcp.json > /dev/null || fail ".mcp.json is not valid JSON."
  python3 -m json.tool .vscode/mcp.json > /dev/null 2>&1 || python -m json.tool .vscode/mcp.json > /dev/null || fail ".vscode/mcp.json is not valid JSON."
else
  for leaked in .claude/skills/run-local .mcp.json .vscode/mcp.json scripts; do
    [ ! -e "$leaked" ] || fail "'$leaked' must not be generated without Aspire."
  done
  if grep -qiE "aspire|keycloak" AGENTS.md; then
    fail "AGENTS.md mentions Aspire or Keycloak in a project generated without Aspire."
  fi
  if grep -qiE "aspire|keycloak|7246" .github/copilot-instructions.md; then
    fail "copilot-instructions.md mentions Aspire, Keycloak or 7246 in a project generated without Aspire."
  fi
fi
py=""
for candidate in python3 python; do
  if command -v "$candidate" >/dev/null 2>&1 && "$candidate" -c "import yaml" >/dev/null 2>&1; then
    py="$candidate"
    break
  fi
done
if [ -n "$py" ]; then
  for file in .claude/skills/*/SKILL.md .claude/agents/*.md .github/agents/*.agent.md; do
    "$py" -c "import sys,yaml; yaml.safe_load(open(sys.argv[1],encoding='utf-8').read().split('---')[1])" "$file" \
      || fail "$file has invalid YAML frontmatter."
  done
else
  echo "verify-template: PyYAML not available, skipping frontmatter YAML parsing"
fi
if find . -name TestResults -type d | grep -q .; then
  fail "TestResults folders leaked into the generated project."
fi
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
