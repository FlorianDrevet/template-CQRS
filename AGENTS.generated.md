# Web.Template.CQRS : consignes pour les agents

## Projet

API ASP.NET Core (.NET 10), architecture CQRS, générée depuis le template Web CQRS. Authentification Entra ID.
Documentation : https://floriandrevet.github.io/template-CQRS/

## Carte du code

| Projet | Rôle | Dépend de |
|--------|------|-----------|
| `src/Web.Template.CQRS.Domain` | Briques du domaine (`Entity`, `AggregateRoot`, `ValueObject`) | rien |
| `src/Web.Template.CQRS.Application` | Queries, commandes, handlers, validateurs, interfaces | Domain |
| `src/Web.Template.CQRS.Contracts` | Types JSON échangés avec les clients (`*Response`) | Domain |
| `src/Web.Template.CQRS.Infrastructure` | EF Core, stockage blob, horloge (implémente les interfaces d'Application) | Application |
| `src/Web.Template.CQRS.Api` | Endpoints, authentification, autorisation, erreurs (racine de composition) | Application, Infrastructure, Contracts |
<!--#if (EnableAspire) -->
| `src/Web.Template.CQRS.ServiceDefaults` | OpenTelemetry, health checks, résilience | paquets NuGet |
| `src/Web.Template.CQRS.AppHost` | Orchestration locale (SQL Server, Azurite, Keycloak, API) | déclare l'API |
<!--#endif -->
<!--#if (EnableTests) -->
| `tests/Web.Template.CQRS.Application.UnitTests` | Tests unitaires des handlers et validateurs | Application |
| `tests/Web.Template.CQRS.Api.IntegrationTests` | Tests d'intégration HTTP | Api |
<!--#endif -->

Les dépendances pointent vers l'intérieur : Domain ne référence rien, Application ne référence jamais Infrastructure.

## Conventions

- CQRS avec la bibliothèque Mediator : `IQuery<T>` / `ICommand<T>`, un handler par opération, retour en `ValueTask`. Un cas d'usage = un record + un handler dans `Application/<Feature>/Queries|Commands/<Nom>/`.
- Endpoints en minimal API, regroupés dans des classes `*Controller` (`Map*Endpoints`) du dossier `Api/Controllers`, retours en `TypedResults`. Un endpoint ne contient pas de logique métier : il envoie la query ou la commande à `IMediator`.
- `*Result` (type du cas d'usage, dans Application) et `*Response` (contrat public, dans Contracts) restent deux types distincts.
- Validation avec FluentValidation : un validateur à côté de la commande ou de la query ; `ValidationBehavior` l'exécute avant le handler.
- Erreurs en Problem Details (RFC 9457) avec `code` (`VALIDATION`, `INTERNAL`, `RATE_LIMITED`, `CONFLICT` pour un 409) et `traceId`. Ne jamais renvoyer le message d'une exception.
- Un endpoint qui mélange un succès `Created` et un `Problem` (409) utilise `Results.*` pour toutes ses branches (ou une union typée `Results<...>`) : `TypedResults.Created` et `Results.Problem` ne se mélangent pas.
- Autorisation explicite sur chaque endpoint : `RequireAuthorization()` ou `RequireAuthorization(Policies.*)`. Une politique de repli exige un utilisateur authentifié.
<!--#if (EnableRateLimiting) -->
- Limitation de débit : chaque endpoint déclare une politique `RateLimitingPolicies.*` (`Read`, `Write`, `Generate`, `Publish`) via `RequireRateLimiting`.
<!--#endif -->
- Options validées au démarrage (`ValidateOnStart`).
- Identité courante via `ICurrentUser` (jamais `HttpContext` dans un handler). Claims Entra : `oid`, `roles`, `groups`.

## Modèle d'autorisation de ce projet

<!--#if (UseAppRoles && UseGroups) -->
App roles et groupes : un utilisateur est administrateur s'il a le rôle `Admin` (`AppRoles.cs`, claim `roles`) ou s'il appartient au groupe `Authorization:Groups:Administrators` (`GroupOptions.cs`, GUID d'un groupe Entra, claim `groups`). Politique : `Policies.RequireAdmin`.
<!--#elseif (UseAppRoles) -->
App roles : rôles `Admin` et `User` dans `src/Web.Template.CQRS.Api/Authorization/AppRoles.cs` (claim `roles`). Politique : `Policies.RequireAdmin` (rôle `Admin`).
<!--#elseif (UseGroups) -->
Groupes de sécurité : l'administrateur appartient au groupe dont le GUID est dans `Authorization:Groups:Administrators` (`GroupOptions.cs`, claim `groups`). Politique : `Policies.RequireAdmin`.
<!--#else -->
Utilisateur authentifié uniquement : aucun rôle ni groupe, aucune politique. `RequireAuthorization()` suffit.
<!--#endif -->

## Commandes

```bash
dotnet build Web.Template.CQRS.slnx
<!--#if (EnableTests) -->
dotnet test --solution Web.Template.CQRS.slnx
dotnet test --project tests/Web.Template.CQRS.Application.UnitTests --filter-class "*MaClasseTests"
<!--#endif -->
<!--#if (EnableAspire) -->
aspire start --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj
aspire stop --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj
./scripts/get-token.ps1 -Persona admin      # ou : bash ./scripts/get-token.sh admin
./scripts/smoke-test.ps1                    # ou : bash ./scripts/smoke-test.sh
<!--#endif -->
```

<!--#if (EnableAspire) -->
L'API écoute uniquement sur `https://localhost:7246`. Keycloak local : `https://localhost:8080`, realm `template`. Personas : `admin@local.test` et `user@local.test` (mot de passe `Passw0rd!`, local uniquement).

<!--#endif -->
## Skills et agent

- `add-feature` : ajouter une fonctionnalité en tranche verticale complète (Domain, Infrastructure, Application, Contracts, Api, tests).
- `manage-access` : ajouter ou modifier un rôle, un groupe ou une politique d'autorisation.
<!--#if (EnableAspire) -->
- `run-local` : démarrer l'environnement local, obtenir un token, lancer le smoke test.
<!--#endif -->
- Agent `reviewer` : à lancer pour relire tout changement de code avant de le présenter comme terminé.
- `project-profile.json` (s'il existe, à la racine) : sens métier des rôles et des personas ; lisez-le avant de nommer un rôle ou une politique.

## Interdits

- Ne jamais écrire de secret (mot de passe, token, chaîne de connexion) dans le dépôt.
- Ne pas écrire de code propre à un fournisseur d'identité local : Entra ID est la cible.
- Ne pas désactiver la validation des options, l'autorisation ou la validation des commandes.
<!--#if (EnableAspire) -->
- Ne pas modifier le realm Keycloak sans prévenir qu'il faut le réimporter (skill `manage-access`, étape 9 : `aspire stop --apphost <csproj> --force --volumes`, puis contrôle et suppression par noms exacts des volumes restants, destructif).
- Ne pas supprimer de volumes sans accord explicite.
<!--#endif -->
- Ne pas pousser sur un remote sans accord explicite.
- Ne pas contourner ni supprimer un test pour faire passer le build.
