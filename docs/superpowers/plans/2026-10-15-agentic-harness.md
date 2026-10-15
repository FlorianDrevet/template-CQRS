# Harnais agentique — Plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Livrer un harnais agentique (Claude Code + GitHub Copilot) : un skill d'initialisation dans le dépôt du template, et dans chaque projet généré des consignes (`AGENTS.md`), trois skills de développement, un agent de revue, la configuration MCP Aspire et un script de smoke test — vérifiés par des agents neufs sur des projets réellement générés.

**Architecture:** Fichiers Markdown/JSON/scripts versionnés dans le template, filtrés par `template.json` (le skill d'init et l'`AGENTS.md` mainteneur restent dans le dépôt template ; `AGENTS.generated.md` devient l'`AGENTS.md` des projets). Skills uniques dans `.claude/skills/` (lus par Claude Code et Copilot), agent de revue en double format, MCP via `aspire agent mcp`.

**Tech Stack:** Claude Code (skills, subagents, `.mcp.json`, `CLAUDE.md` + `@AGENTS.md`), GitHub Copilot (`AGENTS.md`, `.github/copilot-instructions.md`, `.github/agents/*.agent.md`, `.vscode/mcp.json`), Aspire CLI 13.6 (`aspire agent init|mcp`), Bash + PowerShell, `dotnet new`.

**Spec:** `docs/superpowers/specs/2026-10-15-agentic-harness-design.md`

## Global Constraints

- Branche `feat/agentic-harness` (depuis `main` c3fd86a). Commandes depuis la racine du dépôt, Git Bash. Commits terminés par la ligne `Co-Authored-By:` du modèle qui écrit.
- Rédaction des skills, agents et consignes : **français**, impératif, concis ; noms de fichiers, commandes et code en anglais. Chaque `SKILL.md` : frontmatter YAML en première ligne avec `name` (= nom du dossier) et `description` (cas d'usage et déclencheurs d'abord, < 1 000 caractères) ; étapes numérotées ; commandes exactes ; section « Terminé quand » ; section « Ne jamais faire sans demander ». Moins de 300 lignes ; le détail va dans `references/*.md` liés en relatif.
- Agent Claude : `.claude/agents/<nom>.md`, frontmatter `name`, `description`, `tools`, `model`. Agent Copilot : `.github/agents/<nom>.agent.md`, frontmatter `name`, `description`, `tools`.
- Chemins de code dans les skills : écrire `src/Web.Template.CQRS.Api/...` (le moteur de template remplace `Web.Template.CQRS` par le nom du projet généré).
- Faits vérifiés à réutiliser tels quels : API `https://localhost:7246` (jamais 5257), Keycloak `https://localhost:8080`, realm `template`, clients `template-api`/`template-scalar`/`template-cli`, personas `admin@local.test` / `user@local.test` (`Passw0rd!`), GUID de groupe local `6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11`, tests MTP (`dotnet test --solution <X>.slnx`, `--project`, `--filter-class`, `--filter-method`), réimport du realm `aspire stop --apphost <csproj> --force --volumes` (destructif), conteneurs persistants qui gardent le port 8080, `aspire start/stop/ps/describe/logs/otel`, `aspire agent init --non-interactive --workspace-root . --skill-locations claudecode --skills aspire,aspire-orchestration,aspire-monitoring --mcp`.
- Sources à lire pour rester exact : `docs/site/**` (doc vérifiée du template, notamment `guides/ajouter-une-feature.md`, `guides/ajouter-role-groupe-persona.md`, `identite/keycloak-local.md`, `aspire/dashboard-et-cli.md`) et le code de `src/`, `tests/`, `scripts/`, `.template.config/template.json`.
- Après toute modification de `docs/site/` : `python eng/docs/docs.py wiki && python eng/docs/docs.py check` (OK) et `python eng/docs/docs.py build` (sans avertissement).

## Review Focus

- Projet généré **sans Aspire** : pas de `run-local`, pas de `.mcp.json`/`.vscode/mcp.json`, pas de `smoke-test`, et `AGENTS.md` ne mentionne ni Aspire ni Keycloak (vérifié par `verify-template`, Task 7).
- Projet généré avec `Authenticated` : `smoke-test` ne doit pas échouer faute de `/admin/ping` (détection dynamique, Task 1).
- Dossier cible d'init existant et non vide : `new-project` refuse sans rien écraser (Task 6).
- Skill qui supprime des volumes ou pousse sur un remote : confirmation explicite exigée (Tasks 4, 5, 6).
- Le skill d'initialisation ne doit jamais se retrouver dans un projet généré (Task 7).

---

### Task 1: Plomberie — MCP, CLAUDE.md, smoke test, filtres du template

**Files:**
- Create: `.mcp.json`, `.vscode/mcp.json`, `CLAUDE.md`, `scripts/smoke-test.sh`, `scripts/smoke-test.ps1`
- Modify: `.gitignore`, `.template.config/template.json`

**Interfaces:**
- Produces: `scripts/smoke-test.sh` (variables `API_BASE_URL`, `SMOKE_ADMIN`, `SMOKE_USER`), `scripts/smoke-test.ps1` (`-ApiBaseUrl`, `-Admin`, `-User`), sortie `OK …`/`FAIL …`, code 1 au moindre échec ; règles de `template.json` pour les fichiers des Tasks 2 à 6.

- [ ] **Step 1: Fichiers MCP et CLAUDE.md**

`.mcp.json` :

```json
{
  "mcpServers": {
    "aspire": {
      "command": "aspire",
      "args": ["agent", "mcp"]
    }
  }
}
```

`.vscode/mcp.json` :

```json
{
  "servers": {
    "aspire": {
      "type": "stdio",
      "command": "aspire",
      "args": ["agent", "mcp"]
    }
  }
}
```

Dans `.gitignore`, juste après la ligne qui ignore `.vscode` (chercher `.vscode`), ajouter `!.vscode/mcp.json` ; si `.vscode/` est ignoré en tant que dossier (`.vscode/`), remplacer par `.vscode/*` puis `!.vscode/mcp.json`. Vérifier : `git check-ignore -v .vscode/mcp.json` ne retourne rien.

`CLAUDE.md` :

```markdown
@AGENTS.md

## Claude Code

- Utilisez les skills du dossier `.claude/skills/` dès que la demande correspond à leur description.
- Faites relire tout changement de code par l'agent `reviewer` avant de le présenter comme terminé.
```

- [ ] **Step 2: `scripts/smoke-test.sh`**

```bash
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
  curl --silent --output /dev/null --write-out '%{http_code}' "$@" || echo "000"
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
```

- [ ] **Step 3: `scripts/smoke-test.ps1`**

```powershell
<#
.SYNOPSIS
    Local smoke test of the Aspire stack: gets tokens for two personas and checks the expected HTTP status codes.
.EXAMPLE
    ./scripts/smoke-test.ps1
#>
param(
    [string]$ApiBaseUrl = 'https://localhost:7246',
    [string]$Admin = 'admin',
    [string]$User = 'user'
)

$ErrorActionPreference = 'Stop'
$failures = 0

function Get-Status([string]$Uri, [string]$Token) {
    $headers = @{}
    if ($Token) { $headers.Authorization = "Bearer $Token" }
    try {
        return [int](Invoke-WebRequest -Uri $Uri -Headers $headers -UseBasicParsing -Method Get).StatusCode
    }
    catch {
        if ($_.Exception.Response) { return [int]$_.Exception.Response.StatusCode }
        return 0
    }
}

function Test-Expected([string]$Description, [int]$Expected, [int]$Actual) {
    if ($Actual -eq $Expected) {
        Write-Output "OK   $Description ($Actual)"
    }
    else {
        Write-Output "FAIL ${Description}: expected $Expected, got $Actual"
        $script:failures++
    }
}

$adminToken = & "$PSScriptRoot/get-token.ps1" -Persona $Admin
$userToken = & "$PSScriptRoot/get-token.ps1" -Persona $User

Test-Expected 'GET /me without token' 401 (Get-Status "$ApiBaseUrl/me" $null)
Test-Expected "GET /me as $Admin" 200 (Get-Status "$ApiBaseUrl/me" $adminToken)
Test-Expected "GET /me as $User" 200 (Get-Status "$ApiBaseUrl/me" $userToken)

$pingAnonymous = Get-Status "$ApiBaseUrl/admin/ping" $null
if ($pingAnonymous -eq 404) {
    Write-Output 'SKIP /admin/ping (not generated for this authorization model)'
}
else {
    Test-Expected 'GET /admin/ping without token' 401 $pingAnonymous
    Test-Expected "GET /admin/ping as $Admin" 200 (Get-Status "$ApiBaseUrl/admin/ping" $adminToken)
    Test-Expected "GET /admin/ping as $User" 403 (Get-Status "$ApiBaseUrl/admin/ping" $userToken)
}

if ($failures -gt 0) {
    [Console]::Error.WriteLine("smoke-test: $failures check(s) failed")
    exit 1
}
Write-Output 'smoke-test: all checks passed'
```

- [ ] **Step 4: Règles de `template.json`**

Dans le premier `modifiers` (sans condition) : ajouter à `exclude` `"AGENTS.md"` et `".claude/skills/init-project/**"` ; ajouter au `rename` existant `"AGENTS.generated.md": "AGENTS.md"`.
Dans le modifier `(!EnableAspire)` : ajouter à `exclude` `".mcp.json"`, `".vscode/mcp.json"`, `".claude/skills/run-local/**"` (`scripts/**` y est déjà).

Vérifier le JSON : `python -m json.tool .template.config/template.json > /dev/null`.

- [ ] **Step 5: Vérifier et commit**

Run: `bash -n scripts/smoke-test.sh && python -m json.tool .mcp.json >/dev/null && python -m json.tool .vscode/mcp.json >/dev/null && echo ok`
Expected: `ok`.

Run: `pwsh -NoProfile -Command "[System.Management.Automation.Language.Parser]::ParseFile('scripts/smoke-test.ps1',[ref]\$null,[ref]\$errors) | Out-Null; \$errors.Count"`
Expected: `0`.

(La vérification live du smoke test est faite en Task 9.)

```bash
git add .mcp.json .vscode/mcp.json .gitignore CLAUDE.md scripts/smoke-test.sh scripts/smoke-test.ps1 .template.config/template.json
git update-index --chmod=+x scripts/smoke-test.sh
git commit -m "feat(harness): add MCP configuration, CLAUDE.md, smoke test scripts and template filters"
```

---

### Task 2: Consignes — `AGENTS.generated.md`, `AGENTS.md` mainteneur, instructions Copilot

**Files:**
- Create: `AGENTS.generated.md`, `AGENTS.md`, `.github/copilot-instructions.md`

**Interfaces:**
- Consumes: règles de Task 1 (`AGENTS.generated.md` → `AGENTS.md` en génération ; `AGENTS.md` exclu).
- Produces: noms des skills (`add-feature`, `manage-access`, `run-local`) et de l'agent (`reviewer`) cités par les consignes.

- [ ] **Step 1: `AGENTS.generated.md` (consignes du projet généré)**

Lire d'abord `docs/site/architecture/*.md`, `docs/site/reference/*.md`, `src/**/DependencyInjection.cs`, `src/Web.Template.CQRS.Api/Program.cs`. Contenu, en sections courtes :

1. **Projet** (2-3 lignes) : API ASP.NET Core .NET 10, CQRS, générée depuis le template Web CQRS ; doc : `https://floriandrevet.github.io/template-CQRS/`.
2. **Carte du code** : tableau projet → rôle → dépendances autorisées (Domain, Application, Infrastructure, Contracts, Api, ServiceDefaults et AppHost sous `<!--#if (EnableAspire) -->`, tests sous `<!--#if (EnableTests) -->`).
3. **Conventions** (liste) : CQRS avec Mediator (`IQuery`/`ICommand`, handlers en `ValueTask`), endpoints minimal API dans des classes `*Controller` avec `Map*Endpoints` et `TypedResults`, `*Result` (Application) vs `*Response` (Contracts), validation FluentValidation (pipeline `ValidationBehavior`), erreurs en Problem Details (`code` + `traceId`), autorisation explicite (`RequireAuthorization` / `Policies.*`), rate limiting (`RateLimitingPolicies.*`) sous `<!--#if (EnableRateLimiting) -->`, options validées au démarrage, `ICurrentUser` pour l'identité, claims Entra (`oid`, `roles`, `groups`).
4. **Modèle d'autorisation** de ce projet : une ligne par modèle sous directives (`<!--#if (UseAppRoles) -->` …) : où sont les rôles (`AppRoles.cs`), les groupes (`GroupOptions.cs`, `Authorization:Groups:*`), les policies.
5. **Commandes** : build, `dotnet test --solution Web.Template.CQRS.slnx` (sous `EnableTests`), et sous `EnableAspire` : `aspire start --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj`, `aspire stop …`, `./scripts/get-token.ps1 -Persona admin` / `bash ./scripts/get-token.sh admin`, `./scripts/smoke-test.ps1` / `bash ./scripts/smoke-test.sh` ; API sur `https://localhost:7246` uniquement.
6. **Skills et agent** : `add-feature`, `manage-access`, `run-local` (sous `EnableAspire`), agent `reviewer` ; quand les utiliser ; `project-profile.json` (sens métier des rôles et personas, s'il existe).
7. **Interdits** : secrets dans le dépôt ; code spécifique à Keycloak (Entra est la cible, Keycloak l'imite en local) ; désactiver la validation des options ou l'autorisation ; modifier le realm sans prévenir qu'il faut le réimporter ; supprimer des volumes ou pousser sans accord ; contourner les tests.

Chaque directive `<!--#if … -->`, `<!--#else -->`, `<!--#endif -->` sur **sa propre ligne** (sur une ligne partagée, le moteur tronque le fichier).

- [ ] **Step 2: `AGENTS.md` (mainteneurs du template)**

Contenu : rôle du dépôt (template `dotnet new`, pas une application) ; « Pour créer un projet client, utilisez le skill `init-project` » ; structure (symboles, `Directory.Build.props`, `RealmVariants/`, fichiers `*.generated.md`, `.claude/` livré aux projets sauf `init-project`) ; règles (toute modification du code template doit garder les quatre modèles et options compilables ; lancer `bash eng/verify-template.sh <AuthorizationModel> <EnableAspire> <EnableRateLimiting> <EnableTests>` sur les combinaisons touchées ; `dotnet test --solution Web.Template.CQRS.slnx` ; doc : `docs/site` + `python eng/docs/docs.py wiki && check && build`, ne jamais éditer `docs/wiki`) ; liens vers `docs/site/reference/scripts-et-ci.md` et `contribuer-a-la-doc.md`.

- [ ] **Step 3: `.github/copilot-instructions.md`**

10 à 20 lignes : renvoi explicite vers `AGENTS.md` (« lisez AGENTS.md en premier »), les 5 règles les plus importantes (couches, autorisation explicite, tests, Entra cible, API en HTTPS 7246), et la liste des skills de `.claude/skills/` et de l'agent `reviewer` (`.github/agents/reviewer.agent.md`). Pas de directives `#if` (fichier commun à toutes les variantes : formuler « si le projet utilise Aspire… »).

- [ ] **Step 4: Vérifier et commit**

Run: `bash eng/verify-template.sh AppRoles true true true` puis `bash eng/verify-template.sh Authenticated false false false`.
Expected: `verify-template: OK` pour les deux. Dans chaque dossier généré, inspecter `AGENTS.md` (ajouter temporairement `cat AGENTS.md` au script si besoin puis le retirer) : sections présentes, aucune mention d'Aspire/Keycloak dans la variante sans Aspire, aucune ligne `<!--#`.

```bash
git add AGENTS.generated.md AGENTS.md .github/copilot-instructions.md
git commit -m "feat(harness): add agent instructions for generated projects and template maintainers"
```

---

### Task 3: Skill `add-feature`

**Files:**
- Create: `.claude/skills/add-feature/SKILL.md`, `.claude/skills/add-feature/references/checklist.md`, `.claude/skills/add-feature/references/code-patterns.md`

**Interfaces:**
- Consumes: `docs/site/guides/ajouter-une-feature.md` (tutoriel vérifié), `docs/site/tests/index.md`, code existant (`CurrentUserController.cs`, `AdminController.cs`, `PingAdmin*`, `BaseRepository.cs`, `ApiFactory.cs`, `TestUserExtensions.cs`).
- Produces: skill `add-feature`.

- [ ] **Step 1: Rédiger le skill**

`SKILL.md` — description : « Ajouter une fonctionnalité métier complète à l'API (entité, persistance EF, commande/query CQRS, validation, endpoint, autorisation, rate limiting, tests) en suivant les conventions du projet. À utiliser pour toute nouvelle route, ressource ou cas d'usage métier. » Étapes :

1. Comprendre le besoin : ressource, opérations, règles métier, qui a le droit (rôle/groupe/policy), quota ; poser les questions manquantes ; lire `project-profile.json` s'il existe.
2. Annoncer un plan court (fichiers à créer/modifier) avant d'écrire.
3. Domaine → 4. Infrastructure (configuration EF, conversion de l'identifiant, `DbSet`, repository, DI) → 5. Migration (`dotnet ef migrations add <Nom> --project src/Web.Template.CQRS.Infrastructure --startup-project src/Web.Template.CQRS.Api --output-dir Persistence/Migrations`, package `Microsoft.EntityFrameworkCore.Design` dans l'API via `Directory.Packages.props`) → 6. Application → 7. Contrats → 8. Endpoint (policy, `RequireRateLimiting`, codes 201/200/400/403/404, Problem Details) → 9. Tests (unitaires validateur/handler, intégration : autorisé, refusé 403, anonyme 401, invalide 400, introuvable 404) → 10. `dotnet build` puis `dotnet test --solution …` → 11. Vérification live facultative avec le skill `run-local` → 12. Relecture par l'agent `reviewer`, corriger les constats bloquants.

« Terminé quand » : build sans erreur, tous les tests verts, nouveaux tests couvrant les cas 2xx/400/401/403/404 pertinents, revue sans constat bloquant. « Ne jamais faire sans demander » : changer un modèle d'autorisation, supprimer/renommer des routes existantes, modifier le realm (→ `manage-access`), appliquer une migration sur une base partagée.

`references/code-patterns.md` : un exemple court et correct de chaque pièce, **repris du tutoriel vérifié** (`docs/site/guides/ajouter-une-feature.md`) : identifiant `ValueObject`, entité avec invariants, configuration EF avec conversion, repository, commande + validateur + handler, query + handler, endpoint `Map*Endpoints`, test d'intégration avec `ApiFactory`/`AsUser`. Préciser les pièges vérifiés (validateur sur l'identifiant pour éviter une 500, `Database.Migrate()` gardé par `IsDevelopment()`).

`references/checklist.md` : check-list finale cochable (couches, nommage, autorisation explicite, rate limit, validation, tests, migration, doc éventuelle).

- [ ] **Step 2: Vérifier et commit**

Run: `head -5 .claude/skills/add-feature/SKILL.md` → frontmatter `name: add-feature` + `description:`. `wc -l .claude/skills/add-feature/SKILL.md` < 300.

```bash
git add .claude/skills/add-feature
git commit -m "feat(harness): add the add-feature skill"
```

(Évaluation par un agent neuf : Task 10.)

---

### Task 4: Skill `manage-access`

**Files:**
- Create: `.claude/skills/manage-access/SKILL.md`, `.claude/skills/manage-access/references/files-by-model.md`, `.claude/skills/manage-access/references/realm.md`, `.claude/skills/manage-access/references/entra.md`, `.claude/skills/manage-access/references/project-profile.md`

**Interfaces:**
- Consumes: `docs/site/guides/ajouter-role-groupe-persona.md`, `docs/site/identite/*.md`, `src/Web.Template.CQRS.Api/Authorization/*`, `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`, `tests/**/ApiFactory.cs`, `AdminEndpointTests.cs`.
- Produces: skill `manage-access` et le format de `project-profile.json` (spec §4.2) utilisé par `init-project` (Task 6).

- [ ] **Step 1: Rédiger le skill**

Description : « Gérer les accès de l'API : ajouter, renommer ou retirer un rôle (app role Entra), un groupe de sécurité ou un persona Keycloak local ; garder cohérents le code, le realm Keycloak, la configuration, les tests, la doc Entra et project-profile.json. À utiliser dès qu'une demande parle de rôle, groupe, droit, permission, persona ou compte de test. »

Étapes : 1. Lire `project-profile.json` (ou le reconstruire depuis le code : `AppRoles.cs`, `GroupOptions.cs`, realm) et identifier le modèle d'autorisation (fichiers présents). 2. Confirmer l'opération et ses impacts (liste des fichiers). 3. Appliquer selon `references/files-by-model.md`. 4. Realm (`references/realm.md`) : rôles, groupes avec attribut `objectId` (nouveau GUID local), utilisateurs avec `firstName`/`lastName`/`email`/`emailVerified`/`credentials`, uniquement des rôles/groupes existants ; valider le JSON. 5. Tests : mettre à jour `ApiFactory` (nouvelle clé de groupe sinon tous les tests échouent au démarrage) et ajouter des tests de policy. 6. `dotnet build` + `dotnet test --solution …`. 7. Mettre à jour `project-profile.json` (`references/project-profile.md`). 8. Communiquer les étapes Entra (`references/entra.md` : valeur d'app role dans le manifest, assignations, claim de groupes, object ID à fournir en configuration `Authorization__Groups__<Nom>`). 9. Réimport du realm si Keycloak tourne déjà : **demander l'accord** puis `aspire stop --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj --force --volumes` (efface aussi les données SQL locales), `aspire start`, puis vérifier le token du persona (`get-token` + décodage du payload : `roles`/`groups`).

« Terminé quand » : build + tests verts, realm JSON valide et cohérent, profil à jour, étapes Entra communiquées, token live vérifié si l'environnement tourne. « Ne jamais faire sans demander » : supprimer des volumes, retirer un rôle utilisé par une policy, changer le modèle d'autorisation.

`references/files-by-model.md` : tableau opération × modèle (`AppRoles`, `Groups`, `AppRolesAndGroups`, `Authenticated` → explique qu'il faut d'abord ajouter une couche d'autorisation et renvoie à la doc) avec la liste exacte des fichiers, extraits de code attendus (constantes, propriété `GroupOptions` + règle de validation, policy, `appsettings.json` vide, `appsettings.Development.json` GUID local, `ApiFactory`).
`references/realm.md` : extraits JSON exacts (rôle, groupe avec `objectId`, persona complet), règles (mot de passe local `Passw0rd!` sauf demande, `firstName`/`lastName` obligatoires sinon le password grant échoue, référence à un rôle/groupe absent = import en échec, import seulement au premier démarrage).
`references/entra.md` : étapes portail (app roles, assignations, Token configuration → groups claim, object IDs), avec renvoi vers `identite/entra-id.md` de la doc.
`references/project-profile.md` : schéma (spec §4.2) et règles de mise à jour.

- [ ] **Step 2: Vérifier et commit**

Frontmatter et taille comme Task 3.

```bash
git add .claude/skills/manage-access
git commit -m "feat(harness): add the manage-access skill"
```

---

### Task 5: Skill `run-local` et agent `reviewer`

**Files:**
- Create: `.claude/skills/run-local/SKILL.md`, `.claude/skills/run-local/references/troubleshooting.md`, `.claude/agents/reviewer.md`, `.github/agents/reviewer.agent.md`

**Interfaces:**
- Consumes: `scripts/smoke-test.*` (Task 1), `docs/site/aspire/*.md`, `docs/site/demarrer/premier-lancement.md`, `docs/site/fonctionnalites/observabilite.md`, `AGENTS.generated.md` (Task 2).
- Produces: skill `run-local`, agent `reviewer`.

- [ ] **Step 1: `run-local`**

Description : « Lancer, vérifier et dépanner l'environnement local Aspire (SQL Server, Azurite, Keycloak, API) : démarrer/arrêter, obtenir un token pour un persona, appeler l'API, lancer le smoke test, lire logs et traces via le MCP Aspire ou la CLI. À utiliser pour tester une feature en local, reproduire un bug ou diagnostiquer une erreur 401/403/500. »

Étapes : 1. Prérequis (Docker, `aspire --version`, `dotnet dev-certs https --check --trust`, user-secrets `Parameters:sql-password` et `Parameters:keycloak-admin-password` de l'AppHost ; les poser avec des valeurs aléatoires si absents, sans les afficher). 2. Port 8080 libre (`docker ps --filter name=keycloak`) ; si occupé par un autre projet, l'expliquer et proposer `aspire stop --apphost <autre> --force` ou `docker stop` — sans l'appliquer d'office. 3. `aspire start --apphost …` puis attendre (`aspire ps`, `aspire describe`, ou `aspire wait <ressource>` — vérifier `aspire wait --help`). 4. Token : `get-token`. 5. Appeler l'API (`https://localhost:7246`, jamais 5257) avec `curl -sS` ou `Invoke-RestMethod`. 6. `smoke-test`. 7. Observabilité : outils MCP Aspire (ressources, logs, traces) si disponibles, sinon `aspire logs <ressource>`, `aspire otel traces --trace-id <id>` ; relier le `traceId` d'une Problem Details à sa trace ; niveaux de logs (`Microsoft.AspNetCore` en Warning par défaut). 8. Arrêt : `aspire stop --apphost …` (les conteneurs persistants restent ; `--force` les supprime ; `--force --volumes` efface les données → accord).

`references/troubleshooting.md` : tableau symptôme → cause → remède (port 8080, certificat, realm non réimporté, 401 issuer, 401 sur 5257, `invalid_grant`, ressource bloquée en attente, migration au démarrage).

- [ ] **Step 2: Agent `reviewer` (deux formats)**

`.claude/agents/reviewer.md` :

```markdown
---
name: reviewer
description: Relit un changement de code de l'API contre les conventions du projet (AGENTS.md) et rend des constats classés. À utiliser après chaque feature ou correctif, avant de présenter le travail comme terminé.
tools: Read, Grep, Glob, Bash
model: sonnet
---
```

suivi du corps (commun aux deux formats) : rôle ; **lecture seule** (Bash uniquement pour `git diff`, `git log`, `git show`, `git status` ; jamais d'écriture, de build ni de migration) ; entrée : par défaut `git diff main...HEAD` + modifications non commitées (`git diff HEAD`) ; grille de relecture tirée d'`AGENTS.md` (couches et dépendances, CQRS, endpoint `Map*Endpoints`/`TypedResults`, autorisation explicite, rate limiting, validation, Problem Details, options validées, `ICurrentUser`, tests significatifs couvrant 401/403/400/404, migration présente si le modèle EF change, realm cohérent avec les rôles, pas de secret, pas de code spécifique à Keycloak) ; format de sortie : `Bloquant` / `Important` / `Mineur`, chaque constat avec `fichier:ligne`, le problème, pourquoi, la correction ; terminer par un verdict (`Prêt` / `À corriger`).

`.github/agents/reviewer.agent.md` : même corps, frontmatter :

```markdown
---
name: reviewer
description: Relit un changement de code de l'API contre les conventions du projet (AGENTS.md) et rend des constats classés. À utiliser après chaque feature ou correctif, avant de présenter le travail comme terminé.
tools: ['read', 'search']
---
```

et, dans le corps Copilot, demander à l'utilisateur de fournir le diff si l'outil terminal n'est pas disponible.

- [ ] **Step 3: Vérifier et commit**

Frontmatter et taille ; les deux corps de `reviewer` identiques sous le frontmatter (`diff <(sed '1,/^---$/d;1,/^---$/d' …)` ou comparaison manuelle consignée).

```bash
git add .claude/skills/run-local .claude/agents/reviewer.md .github/agents/reviewer.agent.md
git commit -m "feat(harness): add the run-local skill and the reviewer agent"
```

---

### Task 6: Skill `init-project` et scripts `new-project`

**Files:**
- Create: `.claude/skills/init-project/SKILL.md`, `.claude/skills/init-project/references/questionnaire.md`, `eng/init/new-project.sh`, `eng/init/new-project.ps1`

**Interfaces:**
- Consumes: skill `manage-access` (Task 4) et format `project-profile.json` ; `scripts/smoke-test.*` (Task 1).
- Produces: `eng/init/new-project.sh --name <N> [--output <dir>] [--authorization <M>] [--aspire true|false] [--rate-limiting true|false] [--tests true|false] [--remote <url>]` et `eng/init/new-project.ps1 -Name -Output -Authorization -Aspire -RateLimiting -Tests -Remote`.

- [ ] **Step 1: `eng/init/new-project.sh`**

```bash
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
  exit 2
}

while [ $# -gt 0 ]; do
  case "$1" in
    --name) name="${2:-}"; shift 2 ;;
    --output) output="${2:-}"; shift 2 ;;
    --authorization) authorization="${2:-}"; shift 2 ;;
    --aspire) aspire="${2:-}"; shift 2 ;;
    --rate-limiting) rate_limiting="${2:-}"; shift 2 ;;
    --tests) tests="${2:-}"; shift 2 ;;
    --remote) remote="${2:-}"; shift 2 ;;
    -h|--help) usage ;;
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
```

- [ ] **Step 2: `eng/init/new-project.ps1`**

```powershell
<#
.SYNOPSIS
    Generates a client project from this template, non-interactively. Used by the init-project skill; usable without AI.
.EXAMPLE
    ./eng/init/new-project.ps1 -Name Contoso.Orders -Authorization AppRolesAndGroups
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)*$')]
    [string]$Name,
    [string]$Output,
    [ValidateSet('Authenticated', 'AppRoles', 'Groups', 'AppRolesAndGroups')]
    [string]$Authorization = 'AppRoles',
    [bool]$Aspire = $true,
    [bool]$RateLimiting = $true,
    [bool]$Tests = $true,
    [string]$Remote
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked([string]$FilePath, [string[]]$Arguments) {
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        [Console]::Error.WriteLine("new-project: '$FilePath $($Arguments -join ' ')' failed with exit code $LASTEXITCODE")
        exit 1
    }
}

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $Output) { $Output = Join-Path (Split-Path $root -Parent) $Name }
if ((Test-Path $Output) -and (Get-ChildItem -Force $Output | Select-Object -First 1)) {
    [Console]::Error.WriteLine("new-project: '$Output' already exists and is not empty; nothing was changed")
    exit 1
}

$toFlag = { param([bool]$value) $value.ToString().ToLowerInvariant() }

Invoke-Checked 'dotnet' @('new', 'install', $root, '--force')
Invoke-Checked 'dotnet' @(
    'new', 'templatewebcqrs',
    '--name', $Name,
    '--output', $Output,
    '--AuthorizationModel', $Authorization,
    '--EnableAspire', (& $toFlag $Aspire),
    '--EnableRateLimiting', (& $toFlag $RateLimiting),
    '--EnableTests', (& $toFlag $Tests))
Invoke-Checked 'git' @('-C', $Output, 'init', '--quiet')
if ($Remote) { Invoke-Checked 'git' @('-C', $Output, 'remote', 'add', 'origin', $Remote) }

Write-Output "new-project: created $Output"
```

- [ ] **Step 3: Tester les scripts (sans IA)**

```bash
tmp="$(mktemp -d)"
bash eng/init/new-project.sh --name Contoso.Demo --output "$tmp/Contoso.Demo" --authorization Groups --aspire false --tests false
ls -A "$tmp/Contoso.Demo" | head; git -C "$tmp/Contoso.Demo" status --short | head -3
bash eng/init/new-project.sh --name Contoso.Demo --output "$tmp/Contoso.Demo"; echo "exit=$?"
bash eng/init/new-project.sh --name contoso; echo "exit=$?"
pwsh -NoProfile -File eng/init/new-project.ps1 -Name Contoso.Ps -Output "$tmp/Contoso.Ps" -Authorization AppRoles
ls -A "$tmp/Contoso.Ps" | head -3
rm -rf "$tmp"
```

Expected : premier appel `new-project: created …` avec un dépôt git ; second appel `already exists and is not empty` et `exit=1` ; nom invalide `exit=2` ; appel PowerShell `created`. Si `pwsh` est absent, utiliser `powershell -NoProfile -File` et le consigner.

- [ ] **Step 4: Rédiger le skill `init-project`**

Description : « Créer et initialiser un nouveau projet API client à partir de ce template : questionnaire, génération dans un dossier voisin, rôles/groupes/personas, environnement local Aspire, vérification réelle et premier commit. À utiliser quand on demande de créer, initialiser ou démarrer un nouveau projet ou une nouvelle API depuis le template. »

Étapes conformes à la spec §3.1 (prérequis → questionnaire `references/questionnaire.md` → récapitulatif + confirmation → `eng/init/new-project.(sh|ps1)` → accès via `<sortie>/.claude/skills/manage-access/SKILL.md` → `project-profile.json` → secrets (valeurs aléatoires générées, jamais écrites dans le dépôt) + `aspire agent init --non-interactive --workspace-root <sortie> --skill-locations claudecode --skills aspire,aspire-orchestration,aspire-monitoring --mcp` (mentionner les hooks de télémétrie et `ASPIRE_CLI_TELEMETRY_OPTOUT=true`) → `dotnet build` + `dotnet test --solution` → si Aspire : `aspire start --apphost …`, `scripts/smoke-test`, `aspire stop --apphost …` → `git add -A && git commit -m "chore: initialize <Nom> from the Web CQRS template"` (+ remote, **pas de push sans accord**) → passation).

`references/questionnaire.md` : chaque question, son explication pour un débutant, la valeur par défaut, la validation (nom PascalCase à points, rôles en PascalCase sans espace, usernames `xxx@local.test`), et le format du récapitulatif.

« Terminé quand » : projet généré, accès appliqués, build + tests verts, smoke test OK (si Aspire), commit créé, passation affichée. « Ne jamais faire sans demander » : installer des outils, écraser un dossier, pousser, supprimer des volumes, arrêter des conteneurs d'autres projets.

- [ ] **Step 5: Commit**

```bash
git add .claude/skills/init-project eng/init
git update-index --chmod=+x eng/init/new-project.sh
git commit -m "feat(harness): add the init-project skill and new-project scripts"
```

---

### Task 7: Vérifications du harnais dans `verify-template.sh`

**Files:**
- Modify: `eng/verify-template.sh`

- [ ] **Step 1: Ajouter les contrôles**

Après les contrôles de README existants, ajouter :

```bash
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
if grep -q "<!--#" AGENTS.md; then
  fail "AGENTS.md contains unprocessed template directives."
fi
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
fi
```

- [ ] **Step 2: Vérifier et commit**

Run: `bash eng/verify-template.sh AppRolesAndGroups true true true` puis `bash eng/verify-template.sh Authenticated false false false` puis `bash eng/verify-template.sh Groups true false true`.
Expected: trois `verify-template: OK`.

```bash
git add eng/verify-template.sh
git commit -m "test(template): verify the agentic harness in generated projects"
```

---

### Task 8: Documentation — section « Harnais agentique »

**Files:**
- Create: `docs/site/harnais/{index,initialiser-un-projet,skills-de-developpement,agent-de-revue,mcp-aspire,personnaliser-le-harnais}.md`
- Modify: `mkdocs.yml` (nav : section `Harnais agentique` après `Guides`), `docs/site/aspire/dashboard-et-cli.md` (renvoi vers `../harnais/mcp-aspire.md`), `docs/site/index.md` (parcours « Je travaille avec un agent IA »), `docs/site/reference/scripts-et-ci.md` (scripts `smoke-test` et `eng/init/new-project`), `docs/wiki/**` (régénéré)

- [ ] **Step 1: Rédiger**

Règles de rédaction de la doc existante (vouvoiement, novice, concept → pourquoi → comment → à essayer, inclusions `--8<--` des vrais fichiers : `AGENTS.generated.md`, `CLAUDE.md`, `.mcp.json`, `.claude/agents/reviewer.md`, extraits des `SKILL.md`, `scripts/smoke-test.sh`, `eng/init/new-project.sh`). Titres de navigation sans tiret. Pages :
- `index.md` : qu'est-ce qu'un agent de code, un harnais, `AGENTS.md`, un skill, un agent, le MCP ; Claude Code vs Copilot (tableau des fichiers lus par chacun, spec §2) ; carte du harnais (diagramme).
- `initialiser-un-projet.md` : depuis un clone du template, demande type à l'agent, déroulé du skill, ce qui est créé, utilisation des scripts `new-project` sans IA, `project-profile.json`, télémétrie Aspire (opt-out).
- `skills-de-developpement.md` : `add-feature`, `manage-access`, `run-local` : quand, exemples de demandes, ce que fait l'agent, critères de fin.
- `agent-de-revue.md` : rôle, comment l'invoquer (Claude Code et Copilot), grille, format du résultat.
- `mcp-aspire.md` : serveur MCP Aspire (`aspire agent mcp`), configuration `.mcp.json` / `.vscode/mcp.json`, approbation au premier usage dans Claude Code, `aspire agent init`, skills officiels Aspire, exemples de questions à l'agent (« quelles ressources tournent ? », « montre-moi la trace de cette erreur 500 »).
- `personnaliser-le-harnais.md` : ajouter un skill (structure, frontmatter, bonnes pratiques : description orientée déclencheurs, étapes, critères de fin, références), ajouter un agent, garder `AGENTS.md` à jour.

- [ ] **Step 2: Vérifier et commit**

Run: `python eng/docs/docs.py wiki && python eng/docs/docs.py check` → `docs check: OK` ; `python eng/docs/docs.py build` → sans avertissement.

```bash
git add docs/site docs/wiki mkdocs.yml
git commit -m "docs: add the agentic harness section"
```

---

### Task 9: Évaluations live — `init-project`, `manage-access`, `run-local`

**Files:**
- Modify (uniquement si une évaluation révèle un défaut) : fichiers du harnais (Tasks 1-6)

L'implémenteur **joue l'agent utilisateur** : il suit les skills à la lettre, comme le ferait Claude Code ou Copilot, et corrige le skill (pas le résultat) quand une instruction est fausse, ambiguë ou incomplète.

- [ ] **Step 1: E1 — `init-project`**

Depuis le dépôt template, suivre `.claude/skills/init-project/SKILL.md` avec ces réponses scénarisées : nom `Contoso.Eval`, sortie dans un dossier temporaire hors du dépôt, `AppRolesAndGroups`, Aspire/rate limiting/tests activés, rôles `Admin` (administration), `Editor` (édition du catalogue), `Viewer` (lecture), groupe `Editors` (sans object ID Entra), personas `admin@local.test` (Admin + Administrators), `user@local.test` (Viewer), `editor@local.test` (Editor + Editors), pas de remote. Aller jusqu'à la passation : build, tests, `aspire start`, smoke test, token `editor` contenant `Editor` et le GUID local d'`Editors`, `aspire stop`.

- [ ] **Step 2: E2 — `manage-access`**

Dans `Contoso.Eval`, suivre `manage-access` pour ajouter le rôle `Auditor` et le persona `auditor@local.test` (rôle `Auditor`). Build + tests verts ; réimport du realm (accord simulé consigné) ; token `auditor` live contenant `Auditor`.

- [ ] **Step 3: E3 — `run-local`**

Suivre `run-local` : démarrage, smoke test, appel `/me` pour `editor`, provoquer une 401 (sans token), retrouver la trace correspondante via `aspire otel traces` (ou le MCP si disponible), arrêt avec `aspire stop --apphost … --force` (nettoyage), puis suppression des volumes du projet d'évaluation et du dossier temporaire.

- [ ] **Step 4: Corrections et commit**

Corriger chaque instruction de skill fautive rencontrée ; relancer l'étape concernée. Consigner dans le rapport : commandes, sorties clés, écarts trouvés et corrections. Si une correction est faite : `git commit -m "fix(harness): correct skills after live evaluation"` ; sinon pas de commit.

---

### Task 10: Évaluations — `add-feature` et `reviewer`

**Files:**
- Modify (uniquement si une évaluation révèle un défaut) : `.claude/skills/add-feature/**`, `.claude/agents/reviewer.md`, `.github/agents/reviewer.agent.md`

- [ ] **Step 1: E4 — `add-feature`**

Générer `Contoso.Library` (`--AuthorizationModel AppRoles`) dans un dossier temporaire via `eng/init/new-project.sh`. Suivre `add-feature` pour la demande : « Gérer des livres : un Admin crée un livre (titre obligatoire ≤ 200 caractères, ISBN-13 unique), tout utilisateur authentifié peut lister et consulter un livre. » Aller jusqu'à `dotnet build` + `dotnet test --solution` verts et la migration créée (sans Docker).

- [ ] **Step 2: E5 — `reviewer`**

Dans `Contoso.Library`, ajouter volontairement un endpoint `DELETE /books/{id}` **sans** `RequireAuthorization(Policies.RequireAdmin)` explicite ni test, puis appliquer la grille de `.claude/agents/reviewer.md` sur `git diff` comme le ferait l'agent. Le constat « autorisation non explicite / tests manquants » doit sortir en Bloquant ou Important. Ajuster la grille si ce n'est pas le cas.

- [ ] **Step 3: Corrections, nettoyage et commit**

Supprimer le dossier temporaire. Consigner commandes et résultats. Commit éventuel : `git commit -m "fix(harness): correct add-feature and reviewer after evaluation"`.

---

### Task 11: Vérification finale

- [ ] **Step 1: Contrôles**

Run: `bash eng/verify-template.sh AppRolesAndGroups true true true`, `bash eng/verify-template.sh Authenticated false false false` → OK.
Run: `dotnet test --solution Web.Template.CQRS.slnx` → vert.
Run: `python -m pytest eng/docs/tests -q`, `python eng/docs/docs.py check`, `python eng/docs/docs.py build` → vert / OK / sans avertissement.
Run: `git status --short` → propre ; aucun conteneur ni volume d'évaluation restant (`docker ps -a --filter name=keycloak`, `docker volume ls | grep -i eval`).
