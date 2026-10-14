# Sous-projet 1 — Modèle d'autorisation, Keycloak « miroir d'Entra » et tests

- Date : 2026-10-14
- Statut : validé en conversation, en attente de relecture de la spec
- Sous-projets liés : 2 — harnais agentique (init, skills de dev, Aspire MCP) ; 3 — site de documentation MkDocs Material

## 1. Contexte et objectif

Le template `templatewebcqrs` sert de base aux API clientes. Dans tous les projets, **Entra ID est la cible** d'authentification. Keycloak n'existe que pour le développement local via Aspire, au même titre qu'Azurite émule Blob Storage.

Les projets diffèrent par leur modèle d'autorisation : simple authentification, app roles, groupes de sécurité ou combinaison. Aujourd'hui le template ne propose que la fallback policy « utilisateur authentifié », le realm Keycloak ne contient ni rôles, ni groupes, ni utilisateurs, et aucun projet de tests n'existe.

Objectif : que le template génère, selon un paramètre, un code d'autorisation **identique en local et une fois déployé**, un realm Keycloak qui émet des tokens **de même forme qu'Entra**, des personas prêts à l'emploi, un moyen d'obtenir un token sans navigateur, et des tests automatisés — le tout vérifié en CI.

### Critères de succès

1. `dotnet new templatewebcqrs --AuthorizationModel <X>` produit une solution qui compile et dont les tests passent, pour chaque valeur de `X`.
2. Le même code d'API autorise correctement un token Keycloak local et un token Entra ; seules l'authority, l'audience et les object IDs de groupes changent dans la configuration.
3. Un développeur ou un agent obtient un access token pour un persona en une commande, et `GET /me` affiche ses `roles` et `groups`.
4. Une CI GitHub Actions génère une matrice de combinaisons et exécute `build` + `test`.

### Hors périmètre

- Scopes délégués (`scp`) et autorisation par scope.
- Dépassement de groupes Entra (> 200 groupes, appel Microsoft Graph).
- Test e2e Aspire avec un vrai conteneur Keycloak en CI (le smoke test viendra du harnais, sous-projet 2).
- Paramétrage des listes de rôles, groupes et personas propres au projet : c'est le rôle de l'agent d'init (sous-projet 2). Le template fournit des exemples.
- Renommage du realm `template` et des clients par projet.

## 2. Paramètres du template

Ajouts dans `.template.config/template.json` :

```jsonc
"AuthorizationModel": {
  "type": "parameter",
  "datatype": "choice",
  "defaultValue": "AppRoles",
  "choices": [
    { "choice": "Authenticated",     "description": "Utilisateur connecté uniquement." },
    { "choice": "AppRoles",          "description": "App roles Entra (claim roles)." },
    { "choice": "Groups",            "description": "Groupes de sécurité Entra (claim groups)." },
    { "choice": "AppRolesAndGroups", "description": "App roles et groupes de sécurité." }
  ]
},
"EnableTests": {
  "type": "parameter",
  "datatype": "bool",
  "defaultValue": "true",
  "description": "Ajoute les projets de tests unitaires et d'intégration."
},
"UseAppRoles": {
  "type": "computed",
  "value": "(AuthorizationModel == \"AppRoles\" || AuthorizationModel == \"AppRolesAndGroups\")"
},
"UseGroups": {
  "type": "computed",
  "value": "(AuthorizationModel == \"Groups\" || AuthorizationModel == \"AppRolesAndGroups\")"
},
"UseAuthorizationPolicies": {
  "type": "computed",
  "value": "(AuthorizationModel != \"Authenticated\")"
}
```

Règles :

- Le code n'utilise que `#if (UseAppRoles)`, `#if (UseGroups)` et `#if (UseAuthorizationPolicies)`, jamais `AuthorizationModel` directement.
- Les fichiers propres à un modèle sont exclus par `modifiers` (même mécanisme que `EnableRateLimiting`).
- `EnableTests=false` exclut `tests/**` et retire les projets de tests des `.slnx`.
- `.idea/**` est exclu de la génération (dossiers d'IDE d'autres projets actuellement suivis dans le dépôt).
- Le JSON du realm ne supporte pas les commentaires conditionnels de façon fiable : le template embarque **une variante de realm par modèle d'autorisation** (voir §4.4) et les `modifiers` ne conservent que la bonne, renommée en `template-realm.json`.

## 3. Autorisation dans l'API

Nouveau dossier `src/Web.Template.CQRS.Api/Authorization/` :

| Fichier | Condition | Contenu |
|---|---|---|
| `AppRoles.cs` | `UseAppRoles` | Constantes `Admin = "Admin"`, `User = "User"` (valeurs des app roles Entra). |
| `GroupOptions.cs` | `UseGroups` | Section `Authorization:Groups`, dictionnaire nom logique → object ID. Constante `Administrators`. |
| `Policies.cs` | `UseAuthorizationPolicies` | Noms de policies, dont `RequireAdmin`. |
| `AuthorizationSetup.cs` | toujours | `AddTemplateAuthorization(...)` : fallback policy « authentifié » (déplacée depuis `DependencyInjection.cs`) et, selon le modèle, les policies nommées. |

Construction de `RequireAdmin` :

- `AppRoles` : `RequireRole(AppRoles.Admin)`.
- `Groups` : `RequireClaim("groups", <object ID de Administrators lu en config>)`.
- `AppRolesAndGroups` : rôle **ou** groupe (`RequireAssertion`), pour illustrer la combinaison.

Les object IDs de groupes **viennent toujours de la configuration**, jamais du code. `GroupOptions` est validé au démarrage (`ValidateOnStart`) : chaque groupe déclaré doit avoir un GUID valide, sinon l'application refuse de démarrer.

Configuration :

- `appsettings.json` : `"Authorization": { "Groups": { "Administrators": "" } }` (si `UseGroups`), valeur fournie par l'environnement en déploiement.
- `appsettings.Development.json` (variante Aspire) : GUID fixe correspondant au groupe du realm local.
- AppHost : rien à ajouter, `appsettings.Development.json` suffit.

### 3.1 `/me` enrichi

`ICurrentUser`, `HttpCurrentUser`, `CurrentUserResult` et `CurrentUserResponse` exposent en plus `Roles` (`IReadOnlyList<string>`, claim `roles`) et `Groups` (`IReadOnlyList<string>`, claim `groups`). Les listes sont vides si le claim est absent. Ces champs existent quel que soit le modèle : `/me` est l'outil de diagnostic d'un token.

### 3.2 Endpoint d'exemple

`GET /admin/ping` (si `UseAuthorizationPolicies`) dans `Controllers/AdminController.cs` : passe par une query Mediator `PingAdminQuery`, renvoie `{ "message": "pong", "userId": ... }`, protégé par `Policies.RequireAdmin`, rate limit `read` si activé. Il sert de démonstration et de cible « autorisé / refusé ».

### 3.3 Effet sur Scalar et OpenAPI

Le client Keycloak `template-scalar` continue d'obtenir les scopes `roles` par défaut. Aucun changement du transformer OpenAPI.

## 4. Realm Keycloak « miroir d'Entra »

Principe : le token local doit avoir **la même forme** qu'un access token Entra v2, pour que `MapInboundClaims = false`, `RoleClaimType = "roles"` et les policies fonctionnent sans branche spécifique à Keycloak.

### 4.1 Mappers (client scope `entra-compat`, ajouté aux scopes par défaut de `template-scalar` et `template-cli`)

| Mapper | Type Keycloak | Claim produit |
|---|---|---|
| `oid` | `oidc-usermodel-property-mapper`, propriété `id` | `oid` (string) |
| `roles` | `oidc-usermodel-realm-role-mapper`, multivalué | `roles` (tableau plat) |
| `groups` (si `UseGroups`) | `oidc-usermodel-attribute-mapper`, attribut `objectId`, multivalué, `aggregate.attrs=true` | `groups` (tableau de GUID) |

Tous en `access.token.claim=true`.

### 4.2 Rôles, groupes, personas

- Realm roles (si `UseAppRoles`) : `Admin`, `User`.
- Groupes (si `UseGroups`) : `Administrators` avec l'attribut `objectId = 6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11` (GUID fixe repris dans `appsettings.Development.json`).
- Personas (toujours présents) :
  - `admin@local.test` — rôle `Admin` et/ou groupe `Administrators` selon le modèle ;
  - `user@local.test` — rôle `User` (si `UseAppRoles`), aucun groupe.
- Mots de passe en clair dans le realm (`Passw0rd!`), `temporary=false`, e-mails vérifiés. Acceptable uniquement parce que ce realm ne sert qu'en local ; la documentation le dira explicitement.

### 4.3 Client `template-cli`

Client public, `directAccessGrantsEnabled=true`, `standardFlowEnabled=false`, scopes par défaut incluant `entra-compat`, `profile`, `email`, `template-api-audience`. Il permet d'obtenir un token par persona sans navigateur (password grant). Usage strictement local : Entra n'utilise jamais ce flux.

### 4.4 Variantes de fichiers

L'AppHost importe **tout** le dossier `Realms/` : il ne doit donc contenir qu'un seul realm `template`, sinon Keycloak tente d'importer quatre fois le même realm.

- `src/Web.Template.CQRS.AppHost/Realms/template-realm.json` : variante complète `AppRolesAndGroups`, utilisée pour développer le template lui-même.
- `src/Web.Template.CQRS.AppHost/RealmVariants/` : `template-realm.authenticated.json`, `template-realm.approles.json`, `template-realm.groups.json`.
- À la génération : `RealmVariants/**` est toujours exclu ; pour un modèle autre que `AppRolesAndGroups`, le `modifier` correspondant exclut `Realms/template-realm.json` et renomme la variante en `Realms/template-realm.json`.

La CI (§6) valide le JSON des quatre fichiers ; la duplication entre variantes est assumée (fichiers de données, pas de logique).

Risque connu : `aggregate.attrs` doit remonter les attributs des groupes dans la version de Keycloak tirée par `Aspire.Hosting.Keycloak` 13.5. À vérifier pendant l'implémentation ; repli : attribut `objectId` posé directement sur chaque persona (même forme de token, moins réaliste).

### 4.5 Obtenir un token

- `scripts/get-token.ps1 -Persona admin` et `scripts/get-token.sh admin` : appel `POST /realms/template/protocol/openid-connect/token` (client `template-cli`, password grant), affiche l'access token. Paramètres optionnels : `-KeycloakUrl` (défaut `http://localhost:8080`), `-Password`.
- `Api.http` : requête de token par persona puis appels `/me` et `/admin/ping` réutilisant la réponse.
- Les scripts sont inclus seulement si `EnableAspire=true`.

## 5. Tests

### 5.1 Projets

- `tests/Web.Template.CQRS.Application.UnitTests` : xUnit v3. Tests de `GetCurrentUserQueryHandler`, de `PingAdminQuery` (si présent) et de `ValidationBehavior` (avec un message et un validateur de test).
- `tests/Web.Template.CQRS.Api.IntegrationTests` : xUnit v3 + `Microsoft.AspNetCore.Mvc.Testing`.

Versions des packages vérifiées et centralisées dans `Directory.Packages.props` au moment de l'implémentation.

### 5.2 Infrastructure de test d'intégration

- `ApiFactory : WebApplicationFactory<Program>`, environnement `Testing`.
- Configuration en mémoire : `Auth:Provider=Keycloak`, authority factice HTTPS, audience, GUID de groupe de test.
- Schéma `Test` : `TestAuthHandler` construit le principal depuis les en-têtes `X-Test-User`, `X-Test-Roles`, `X-Test-Groups` (listes séparées par des virgules). Pas d'en-tête `X-Test-User` = non authentifié. Le schéma par défaut est remplacé par `Test` dans la factory.
- `ProjectDbContext` remplacé par un fournisseur en mémoire (pas de SQL Server requis) ; `IBlobService` remplacé par un faux.

### 5.3 Cas fournis

| Test | Condition |
|---|---|
| `/me` sans authentification → 401 | toujours |
| `/me` authentifié → 200, `id`, `roles`, `groups` reflètent les claims | toujours |
| `/admin/ping` en `User` → 403 | `UseAuthorizationPolicies` |
| `/admin/ping` en `Admin` (rôle et/ou groupe) → 200 | `UseAuthorizationPolicies` |
| Erreur de validation → 400 Problem Details avec `code=VALIDATION` et `traceId` | toujours (hôte `TestServer` minimal dans le projet de tests : `UseErrorHandling()` + une route qui lève `ValidationException`) |
| Exception inattendue → 500 Problem Details avec `code=INTERNAL`, sans détail interne | toujours (même hôte minimal) |
| Dépassement de quota → 429 Problem Details avec `code=RATE_LIMITED` | `EnableRateLimiting` |
| Démarrage avec groupe sans GUID → échec de validation | `UseGroups` |

## 6. CI du dépôt template

`.github/workflows/template-ci.yml`, déclenché sur push et pull request :

1. `actions/setup-dotnet` selon `global.json`.
2. `dotnet new install ./`.
3. Matrice de génération :

| # | AuthorizationModel | EnableAspire | EnableRateLimiting | EnableTests |
|---|---|---|---|---|
| 1 | Authenticated | true | true | true |
| 2 | AppRoles | true | true | true |
| 3 | Groups | true | false | true |
| 4 | AppRolesAndGroups | true | true | true |
| 5 | AppRoles | false | true | true |
| 6 | AppRoles | true | true | false |

4. Pour chaque combinaison : `dotnet new templatewebcqrs -n Ci.Generated <flags>`, `dotnet build`, `dotnet test` si `EnableTests`.
5. Vérification que `Realms/template-realm.json` et les trois fichiers de `RealmVariants/` sont des JSON valides.

## 7. Fichiers impactés (résumé)

- Modifiés : `.template.config/template.json`, `Directory.Packages.props`, `*.slnx`, `Api/Program.cs`, `Api/DependencyInjection.cs`, `Api/Authentication/HttpCurrentUser.cs`, `Api/Controllers/CurrentUserController.cs`, `Api/appsettings*.json`, `Api/Api.http`, `Application/Common/Interfaces/Authentication/ICurrentUser.cs`, `Application/Users/**`, `Contracts/Authentication/CurrentUserResponse.cs`, `AppHost/Realms/template-realm.json`, `README.md`.
- Créés : `Api/Authorization/*`, `Api/Controllers/AdminController.cs`, `Application/Admin/Queries/PingAdmin/*`, `Contracts/Admin/*`, `AppHost/RealmVariants/*`, `scripts/get-token.{ps1,sh}`, `tests/**`, `.github/workflows/template-ci.yml`.

## 8. Risques

- `aggregate.attrs` (voir §4.4) — repli défini.
- Syntaxe des symboles `computed` et des conditions de `modifiers` avec des `choice` : vérifiée par la CI de matrice.
- Fichiers JSON avec `//#if` (`appsettings.Development.json`) : déjà utilisé dans le template, on conserve le même procédé.
- Le dossier `docs/` accueillera le site MkDocs (sous-projet 3) : `docs/superpowers/**` devra être exclu du site.
