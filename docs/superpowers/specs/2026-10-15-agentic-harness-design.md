# Sous-projet 2 — Harnais agentique

- Date : 2026-10-15
- Statut : design validé en conversation (exécution enchaînée demandée par l'utilisateur)
- Dépend de : sous-projets 1 (autorisation, realm, tests) et 3 (documentation), fusionnés dans `main`

## 1. Objectif

Permettre à une équipe qui clone le dépôt du template de :

1. **initialiser un projet client avec un agent IA** (Claude Code ou GitHub Copilot) : l'agent pose les bonnes questions, génère le projet, l'adapte (rôles, groupes, personas), prépare l'environnement local et prouve que tout fonctionne ;
2. **développer ensuite dans l'API avec un agent** qui connaît les conventions du template : ajouter une feature, gérer les accès (rôles, groupes, personas), lancer et tester en local (Aspire, MCP, Keycloak), et faire relire son travail.

### Critères de succès

1. Depuis un clone du template, la demande « initialise un projet » à Claude Code ou Copilot aboutit à un projet généré dans un dossier voisin, avec ses rôles/groupes/personas, un dépôt git initialisé, et un smoke test réel réussi (token admin → 200, token user → 403).
2. Dans un projet généré, chaque skill (`add-feature`, `manage-access`, `run-local`) suivi à la lettre par un agent neuf aboutit à un état vert (build, tests, et vérification live quand le skill la prévoit).
3. L'agent `reviewer` détecte une violation volontaire des conventions dans un diff.
4. Les mêmes fichiers servent Claude Code et Copilot (skills uniques dans `.claude/skills/`).
5. `eng/verify-template.sh` garantit que chaque projet généré contient le harnais attendu et jamais le skill d'initialisation.
6. La documentation (site + wiki) contient une section « Harnais agentique ».

### Hors périmètre

- Autres agents qu'Claude Code et GitHub Copilot (Cursor, OpenCode… restent possibles via `AGENTS.md`, sans support dédié).
- Évaluations automatisées en CI (elles nécessitent une clé d'API) : les évaluations sont faites pendant l'implémentation par des agents neufs.
- Publication du template sur NuGet.

## 2. Formats retenus (vérifiés)

| Élément | Claude Code | GitHub Copilot |
|---|---|---|
| Consignes du dépôt | `CLAUDE.md` (importe `@AGENTS.md`) | `AGENTS.md` + `.github/copilot-instructions.md` |
| Skills | `.claude/skills/<nom>/SKILL.md` | lus aussi dans `.claude/skills/` |
| Agents | `.claude/agents/<nom>.md` | `.github/agents/<nom>.agent.md` |
| MCP Aspire | `.mcp.json` (`mcpServers.aspire` → `aspire agent mcp`) | `.vscode/mcp.json` (`servers.aspire`, `type: stdio`) |

`aspire agent init --non-interactive --workspace-root . --skill-locations claudecode --skills <liste> --mcp` installe les skills officiels Aspire dans `.claude/skills/` et écrit `.mcp.json` / `.vscode/mcp.json` (vérifié sur Aspire CLI 13.6). Le template commite lui-même ces deux fichiers MCP pour que le MCP fonctionne même sans l'initialisation ; `.gitignore` reçoit l'exception `!.vscode/mcp.json`.

## 3. Dépôt du template (jamais copié dans les projets générés)

| Fichier | Rôle |
|---|---|
| `AGENTS.md` | Consignes pour les agents qui travaillent **sur le template** : structure, symboles, `verify-template`, doc (`eng/docs`), règles (pas de code client ici, régénérer le wiki…), et renvoi vers `init-project` pour créer un projet |
| `CLAUDE.md` | `@AGENTS.md` |
| `.claude/skills/init-project/SKILL.md` (+ `references/questionnaire.md`) | Skill d'initialisation (§3.1) |
| `eng/init/new-project.ps1`, `eng/init/new-project.sh` | Génération non interactive (§3.2), utilisable sans IA |

`template.json` exclut `AGENTS.md`, `.claude/skills/init-project/**`, `eng/**` (déjà) et renomme `AGENTS.generated.md` en `AGENTS.md`. `CLAUDE.md` est identique des deux côtés (`@AGENTS.md`) et n'est pas exclu.

### 3.1 Skill `init-project`

Étapes, dans l'ordre, avec arrêt et explication en cas d'échec :

1. **Prérequis** : SDK .NET (version de `global.json`), Docker démarré, Aspire CLI, certificat de développement approuvé (`dotnet dev-certs https --check --trust`). Propose les commandes d'installation manquantes, ne les exécute qu'avec accord.
2. **Questionnaire** (`references/questionnaire.md`) : nom du projet (PascalCase avec points, ex. `Contoso.Orders`), dossier cible (défaut `../<Nom>`), modèle d'autorisation (avec explication des 4 choix), Aspire, rate limiting, tests, rôles métier (nom + description), groupes (nom logique + object ID Entra si connu), personas (identifiant, prénom/nom, rôles, groupes), URL du remote git (facultatif). Récapitulatif et confirmation avant toute écriture.
3. **Génération** : `eng/init/new-project.(ps1|sh)` avec les options.
4. **Accès** : applique rôles, groupes et personas en suivant `.claude/skills/manage-access/SKILL.md` **du projet généré** (une seule source de vérité pour ces règles).
5. **Profil** : écrit `project-profile.json` (§4.2).
6. **Environnement local** (si Aspire) : user-secrets de l'AppHost (mots de passe générés aléatoirement, jamais affichés en clair dans le dépôt), `aspire agent init` (skills Aspire `aspire`, `aspire-orchestration`, `aspire-monitoring`, emplacement `claudecode`, `--mcp`).
7. **Vérification** : `dotnet build`, `dotnet test --solution`, puis (si Aspire) `aspire start`, `scripts/smoke-test`, `aspire stop`. Si le port 8080 est pris, explique la cause (conteneur Keycloak persistant d'un autre projet) et propose la remédiation sans l'appliquer d'office.
8. **Git** : premier commit, remote si fourni (pas de push sans accord).
9. **Passation** : résumé (ce qui a été créé, personas et mots de passe locaux, commandes utiles, prochaines étapes : `add-feature`, `run-local`, doc).

### 3.2 Script `new-project`

Paramètres : nom, sortie, `AuthorizationModel`, `EnableAspire`, `EnableRateLimiting`, `EnableTests`, remote facultatif. Effets : `dotnet new install <racine du template> --force`, `dotnet new templatewebcqrs …`, refus si le dossier cible existe et n'est pas vide, `git init`, `.gitattributes` si absent non requis, remote ajouté si fourni. Codes de sortie non nuls et messages clairs en cas d'erreur. Aucune action réseau autre que celles de `dotnet new`.

## 4. Projet généré

| Fichier | Contenu |
|---|---|
| `AGENTS.md` (depuis `AGENTS.generated.md`, directives `<!--#if -->` sur leurs propres lignes) | Présentation, carte des projets et des couches, conventions (CQRS/Mediator, `*Controller` = minimal API, `Result` vs `Response`, validation, Problem Details, policies et `RequireAuthorization`, `RequireRateLimiting`, `ICurrentUser`, configuration par options validées), commandes (build, tests MTP, `aspire start/stop`, `get-token`, `smoke-test`, `docs`), skills et agent disponibles, interdits (secrets, Entra en local, modifier le realm sans réimport, désactiver la validation…), liens vers la documentation |
| `CLAUDE.md` | `@AGENTS.md` + préférences Claude Code (utiliser les skills, faire relire par `reviewer`) |
| `.github/copilot-instructions.md` | Règles essentielles + renvoi vers `AGENTS.md` et les skills |
| `.claude/skills/add-feature/SKILL.md` (+ `references/`) | §4.1 |
| `.claude/skills/manage-access/SKILL.md` (+ `references/`) | §4.1 |
| `.claude/skills/run-local/SKILL.md` | §4.1 (seulement si `EnableAspire`) |
| `.claude/agents/reviewer.md`, `.github/agents/reviewer.agent.md` | §4.3 |
| `.mcp.json`, `.vscode/mcp.json` | MCP Aspire (seulement si `EnableAspire`) |
| `scripts/smoke-test.ps1`, `scripts/smoke-test.sh` | §4.4 (seulement si `EnableAspire`) |
| `project-profile.json` | écrit par `init-project`, maintenu par `manage-access` (§4.2) — non fourni par le template |

### 4.1 Skills de développement

- **`add-feature`** : comprendre le besoin (questions si ambigu) → plan court → domaine (entité, `ValueObject` d'identifiant, invariants) → infrastructure (configuration EF, conversion d'identifiant, `DbSet`, repository, DI) → migration (`dotnet ef`, projet de démarrage, package Design) → application (commande/query, validateur, handler, `Result`) → contrats → endpoint (`Map*Endpoints`, `TypedResults`, policy, rate limit, 201/404/400) → tests (unitaires + intégration avec `ApiFactory`/`AsUser`, cas autorisé/refusé/invalide/introuvable) → `dotnet build` + `dotnet test` → vérification live facultative via `run-local` → relecture `reviewer`. S'appuie sur le tutoriel vérifié de la documentation.
- **`manage-access`** : opérations ajouter/retirer/renommer un rôle, un groupe, un persona ; pour chaque opération, liste exhaustive des fichiers à modifier selon le modèle d'autorisation (`AppRoles.cs`, `GroupOptions.cs` + validation, `Policies.cs`/`AuthorizationSetup.cs`, `appsettings*.json`, `ApiFactory` des tests, `Realms/template-realm.json` : rôles, groupes avec `objectId`, utilisateurs avec `firstName`/`lastName`, références cohérentes), étapes Entra à communiquer (app roles du manifest, assignations, claim de groupes, object IDs), tests à ajouter, mise à jour de `project-profile.json`, réimport du realm (`aspire stop --apphost … --force --volumes`, destructif → confirmation obligatoire), vérification live du token (claims `roles`/`groups`).
- **`run-local`** : prérequis, secrets, `aspire start` / `aspire ps` / `aspire describe` / `aspire stop`, outils MCP Aspire (ressources, logs, traces) et équivalents CLI (`aspire logs`, `aspire otel`), `get-token` par persona, appels `https://localhost:7246` (jamais 5257), `smoke-test`, tester une nouvelle route, dépannage (port 8080 et conteneurs persistants, certificat, realm non réimporté, 401 issuer, niveaux de logs).

Chaque skill : frontmatter `name` + `description` (déclencheurs explicites), étapes numérotées, commandes exactes, critères de fin (« terminé quand… »), et ce qu'il ne faut jamais faire sans demander.

### 4.2 `project-profile.json`

```json
{
  "schemaVersion": 1,
  "name": "Contoso.Orders",
  "authorizationModel": "AppRolesAndGroups",
  "options": { "aspire": true, "rateLimiting": true, "tests": true },
  "roles": [ { "name": "Admin", "description": "Administre le catalogue" } ],
  "groups": [ { "name": "Administrators", "description": "…", "entraObjectId": null, "localObjectId": "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11" } ],
  "personas": [ { "username": "admin@local.test", "firstName": "Alice", "lastName": "Admin", "roles": ["Admin"], "groups": ["Administrators"], "purpose": "…" } ]
}
```

Source de vérité **métier** (sens des rôles et personas) ; le code et le realm restent la source de vérité **technique**. Les skills le relisent et le tiennent à jour ; s'il est absent, ils le reconstruisent depuis le code.

### 4.3 Agent `reviewer`

Lecture seule. Entrée : un diff (par défaut `git diff` de la branche courante vs `main`). Vérifie les conventions d'`AGENTS.md` (couches et dépendances, CQRS, endpoints, autorisation explicite, rate limiting, validation, Problem Details, configuration validée, tests présents et significatifs, pas de secrets, realm cohérent avec les rôles) et rend une liste de constats classés (bloquant / important / mineur) avec fichier:ligne. Deux fichiers au contenu équivalent (formats Claude et Copilot).

### 4.4 Script `smoke-test`

Pour chaque persona de test (par défaut `admin` et `user`) : token via `get-token`, appel `GET /me` (200 attendu) et, si `/admin/ping` existe, 200 pour admin et 403 pour user ; appel anonyme `/me` → 401. URL de base `https://localhost:7246` (paramétrable). Sortie lisible, code de sortie non nul au premier écart.

## 5. Vérification

- **Automatique** : `eng/verify-template.sh` vérifie dans chaque projet généré la présence de `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, des skills et agents attendus (et `run-local`, MCP, `smoke-test` seulement avec Aspire), l'absence de `init-project` et du `AGENTS.md` du template, la validité JSON des fichiers MCP, la présence d'un frontmatter `name`/`description` dans chaque `SKILL.md`.
- **Évaluations par agents neufs** (pendant l'implémentation, consignées dans le rapport) :
  1. `init-project` avec des réponses scénarisées (AppRolesAndGroups, rôles Admin/Editor/Viewer, groupe Editors, persona `editor@local.test`) jusqu'au smoke test live ;
  2. `add-feature` sur un projet généré (feature différente du tutoriel) → build + tests verts ;
  3. `manage-access` : ajout d'un rôle et d'un persona → tests verts + token live contenant le rôle ;
  4. `run-local` : démarrage, smoke test, lecture d'une trace, arrêt ;
  5. `reviewer` sur un diff contenant une violation volontaire (endpoint sans autorisation explicite ni test) → violation détectée.

## 6. Documentation

Nouvelle section « Harnais agentique » (site + wiki) : `index.md` (qu'est-ce qu'un agent, un harnais, `AGENTS.md`, skills, agents, MCP ; Claude Code et Copilot), `initialiser-un-projet.md`, `skills-de-developpement.md` (les trois skills et quand les utiliser), `agent-de-revue.md`, `mcp-aspire.md`, `personnaliser-le-harnais.md` (ajouter un skill, bonnes pratiques). La page « Dashboard et CLI » renvoie vers `mcp-aspire.md`.

## 7. Risques

- Formats Copilot encore mouvants (`.agent.md`, lecture de `.claude/skills/`) : vérifiés au moment de l'implémentation ; `AGENTS.md` reste le socle commun.
- `aspire agent init` installe aussi des hooks de télémétrie côté utilisateur : signalé dans la passation et la doc (opt-out `ASPIRE_CLI_TELEMETRY_OPTOUT=true`).
- Des skills trop longs dégradent leur usage : chaque `SKILL.md` reste centré sur les étapes ; le détail va dans `references/`.
- Évaluations live dépendantes de Docker et du port 8080 : l'implémenteur arrête ses conteneurs (`--force`) en fin d'évaluation.
