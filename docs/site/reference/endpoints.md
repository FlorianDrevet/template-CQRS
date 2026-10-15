# Endpoints

Cette page liste toutes les routes exposées par l'API générée : méthode, chemin, authentification, policy d'autorisation, limite de débit, environnements où la route existe et réponses. Pour ajouter un endpoint, voir [Ajouter une feature](../guides/ajouter-une-feature.md).

En local avec Aspire, l'API est servie sur **`https://localhost:7246`**. Le port HTTP `5257` répond par une redirection vers HTTPS et les clients abandonnent alors l'en-tête `Authorization` : appelez toujours l'adresse HTTPS.

## Vue d'ensemble

| Méthode | Chemin | Authentification | Policy | Limite de débit | Environnement | Réponse |
|---------|--------|------------------|--------|-----------------|---------------|---------|
| `GET` | `/me` | jeton requis | aucune (tout utilisateur connecté) | `read` (600 par minute) | tous | `200` `CurrentUserResponse` |
| `GET` | `/admin/ping` | jeton requis | `RequireAdmin` | `read` (600 par minute) | tous ; absent si le modèle est `Authenticated` | `200` `PingAdminResponse` |
| `GET` | `/openapi/v1.json` | anonyme | aucune | aucune | `Development` | `200` document OpenAPI |
| `GET` | `/scalar` (page du document : `/scalar/v1`) | anonyme | aucune | aucune | `Development` | `200` interface Scalar |
| `GET` | `/health` | anonyme | aucune | aucune | `Development` ou `Testing`, avec Aspire | `200` si tous les checks sont sains |
| `GET` | `/alive` | anonyme | aucune | aucune | `Development` ou `Testing`, avec Aspire | `200` si les checks étiquetés `live` sont sains |
| toute autre | tout autre chemin | anonyme | aucune | aucune | tous | `404` |

La colonne « Limite de débit » ne s'applique que si l'option `EnableRateLimiting` est activée ; sans elle, aucune route n'est limitée. « Avec Aspire » signifie l'option `EnableAspire` : sans elle, `/health` et `/alive` n'existent pas.

Par défaut, **toute route qui n'est pas explicitement anonyme exige un utilisateur authentifié** : le template définit une politique de repli (*fallback policy*) qui impose l'authentification. Les routes de la documentation, des health checks et le repli 404 sont déclarées anonymes avec `AllowAnonymous()`.

```csharp title="src/Web.Template.CQRS.Api/Program.cs"
--8<-- "src/Web.Template.CQRS.Api/Program.cs" start="app.MapCurrentUserEndpoints();" end="app.MapFallback(() => Results.NotFound()).AllowAnonymous();"
```

!!! info "Les lignes `#if` de l'extrait"
    `#if (UseAuthorizationPolicies)` est une directive du template. Dans un projet généré, seules les branches correspondant à vos options sont conservées : avec le modèle `Authenticated`, `MapAdminEndpoints()` et la route `/admin/ping` n'existent pas.

## `GET /me`

Retourne l'identité de l'utilisateur appelant, lue dans son jeton. Tout utilisateur authentifié y a accès.

| Statut | Quand | Corps |
|--------|-------|-------|
| `200` | jeton valide | `CurrentUserResponse` |
| `401` | pas de jeton ou jeton invalide | aucun contenu métier |
| `429` | limite de débit dépassée | Problem Details, `code` = `RATE_LIMITED` |

```csharp title="src/Web.Template.CQRS.Contracts/Authentication/CurrentUserResponse.cs"
--8<-- "src/Web.Template.CQRS.Contracts/Authentication/CurrentUserResponse.cs" start="public sealed record CurrentUserResponse"
```

Le JSON utilise les noms en minuscules initiales (`id`, `displayName`, `email`, `roles`, `groups`). Avec le persona `admin@local.test`, `roles` vaut `["Admin"]` et `groups` contient le GUID du groupe d'administration ; avec `user@local.test`, `roles` vaut `["User"]` et `groups` est vide (selon le modèle d'autorisation choisi, voir [Options du template](options-du-template.md)).

## `GET /admin/ping`

Réservé aux administrateurs : la route exige la policy `RequireAdmin`, qui accepte selon le modèle le rôle `Admin`, l'appartenance au groupe configuré, ou l'un des deux. Elle sert d'exemple d'endpoint protégé. Elle n'existe pas si le modèle est `Authenticated`.

| Statut | Quand | Corps |
|--------|-------|-------|
| `200` | administrateur | `PingAdminResponse` |
| `401` | pas de jeton ou jeton invalide | aucun contenu métier |
| `403` | authentifié mais non administrateur | aucun contenu métier |
| `429` | limite de débit dépassée | Problem Details, `code` = `RATE_LIMITED` |

```csharp title="src/Web.Template.CQRS.Contracts/Admin/PingAdminResponse.cs"
--8<-- "src/Web.Template.CQRS.Contracts/Admin/PingAdminResponse.cs" start="public sealed record PingAdminResponse"
```

Résultats vérifiés en local : administrateur `200`, utilisateur ordinaire `403`, appel anonyme `401`. Voir [Modèles d'autorisation](../identite/modeles-d-autorisation.md).

## `GET /openapi/v1.json` et Scalar

`MapOpenApi()` publie le document OpenAPI sur `/openapi/v1.json` ; `MapScalarApiReference` publie l'interface interactive sous `/scalar`, la page du document `v1` étant `/scalar/v1`. Ces deux routes n'existent qu'en environnement `Development` et sont anonymes (l'interface doit se charger avant d'avoir un jeton). Voir [OpenAPI et Scalar](../fonctionnalites/openapi-et-scalar.md).

## `GET /health` et `GET /alive`

Fournies par le projet `ServiceDefaults` (option `EnableAspire`), en `Development` ou `Testing` seulement :

| Chemin | Checks exécutés | Usage |
|--------|-----------------|-------|
| `/health` | tous les checks enregistrés | sonde de disponibilité (*readiness*) |
| `/alive` | uniquement les checks étiquetés `live` | sonde de vivacité (*liveness*), utilisée par l'AppHost |

Le template n'enregistre qu'un check, `self`, toujours sain. Ces routes ne sont ni limitées en débit ni tracées par OpenTelemetry. Voir [Health checks](../fonctionnalites/health-checks.md).

## Le repli `404`

Toute requête qui ne correspond à aucune route reçoit `404`, y compris sans jeton. La route de repli (`MapFallback`) est déclarée anonyme : sans `AllowAnonymous()`, la politique de repli lui imposerait l'authentification et un chemin inconnu appelé sans jeton recevrait `401` au lieu de `404`.

## Limites de débit

Quand `EnableRateLimiting` est activé, quatre politiques existent. Elles utilisent une fenêtre fixe d'une minute, sans file d'attente. Seule `read` est utilisée par les endpoints du template ; les trois autres sont prêtes pour vos propres endpoints.

```csharp title="src/Web.Template.CQRS.Api/Common/RateLimiting/RateLimiting.cs"
--8<-- "src/Web.Template.CQRS.Api/Common/RateLimiting/RateLimiting.cs" start="options.AddPolicy(RateLimitingPolicies.Read" end="RateLimitingPolicies.Publish, context => CreatePartition(context, 5));"
```

| Politique | Requêtes par minute | Usage prévu |
|-----------|---------------------|-------------|
| `read` | 600 | lectures |
| `write` | 120 | écritures |
| `generate` | 10 | opérations coûteuses |
| `publish` | 5 | opérations rares et sensibles |

Le compteur est propre à chaque appelant : l'identifiant `oid` du jeton, sinon `sub`, sinon l'adresse IP. Un appel refusé reçoit `429`, un en-tête `Retry-After` (en secondes) et un corps Problem Details avec `code` = `RATE_LIMITED` et un `traceId`. Voir [Rate limiting](../fonctionnalites/rate-limiting.md).

## Les erreurs communes

| Statut | `code` | Origine | Voir |
|--------|--------|---------|------|
| `400` | `VALIDATION` | une commande ou une query invalide (le corps contient `errors` par propriété) | [Validation](../architecture/validation.md) |
| `401` | | pas de jeton, jeton expiré ou invalide | [Entra ID](../identite/entra-id.md) |
| `403` | | jeton valide mais droits insuffisants | [Modèles d'autorisation](../identite/modeles-d-autorisation.md) |
| `404` | | route inconnue | ci-dessus |
| `429` | `RATE_LIMITED` | limite de débit dépassée | ci-dessus |
| `500` | `INTERNAL` | exception non gérée, sans détail interne | [Erreurs et Problem Details](../architecture/erreurs-et-problem-details.md) |

## À essayer

1. Obtenez un jeton avec `./scripts/get-token.sh admin` (voir [Scripts et CI](scripts-et-ci.md)) et appelez `GET /me` puis `GET /admin/ping`.
2. Refaites les deux appels avec le jeton de `user`, puis sans jeton : vérifiez les statuts `200`, `403` et `401` du tableau.
3. Appelez `GET /nimporte-quoi` sans jeton : quel statut obtenez-vous, et pourquoi pas `401` ?
