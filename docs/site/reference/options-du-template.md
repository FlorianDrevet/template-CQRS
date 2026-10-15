# Options du template

Cette page décrit les options acceptées par `dotnet new templatewebcqrs`, ce que chacune ajoute ou retire dans le projet généré, et l'effet du modèle d'autorisation sur le realm Keycloak local. Pour comprendre comment fonctionnent les symboles, lisez d'abord [Comprendre les templates](../demarrer/qu-est-ce-qu-un-template.md) ; pour générer un projet pas à pas, voir [Créer un projet](../demarrer/creer-un-projet.md).

## Les options

Quatre options existent. Elles se passent en ligne de commande sous la forme `--Option valeur`, et `dotnet new templatewebcqrs --help` en affiche la liste.

| Option | Type | Valeurs | Défaut | Rôle |
|--------|------|---------|--------|------|
| `EnableAspire` | booléen | `true`, `false` | `true` | Ajoute Aspire, l'observabilité OpenTelemetry et les services locaux SQL Server, Azurite et Keycloak |
| `EnableRateLimiting` | booléen | `true`, `false` | `true` | Ajoute les politiques de limitation de débit de l'API |
| `AuthorizationModel` | choix | `Authenticated`, `AppRoles`, `Groups`, `AppRolesAndGroups` | `AppRoles` | Modèle d'autorisation Entra ID, reproduit localement par Keycloak |
| `EnableTests` | booléen | `true`, `false` | `true` | Ajoute les projets de tests unitaires et d'intégration |

Les quatre options sont indépendantes : toutes les combinaisons sont permises. La CI du dépôt en génère six, représentatives, à chaque modification (voir [Scripts et CI](scripts-et-ci.md)).

```bash
dotnet new templatewebcqrs --name Contoso.Orders --output Contoso.Orders \
  --AuthorizationModel Groups --EnableRateLimiting false --EnableTests false
```

## Les symboles tels que le template les déclare

Voici le bloc `symbols` de `.template.config/template.json`. Les trois symboles `computed` ne se saisissent jamais : ils sont calculés à partir de `AuthorizationModel`.

```json title=".template.config/template.json"
--8<-- ".template.config/template.json" start='"symbols": {' before='"sources"'
```

| Symbole calculé | Vrai quand | Sert à |
|-----------------|-----------|--------|
| `UseAppRoles` | `AuthorizationModel` vaut `AppRoles` ou `AppRolesAndGroups` | garder `AppRoles.cs` et la prise en compte du claim `roles` |
| `UseGroups` | `AuthorizationModel` vaut `Groups` ou `AppRolesAndGroups` | garder `GroupOptions.cs`, la clé `Authorization:Groups:Administrators` et les tests associés |
| `UseAuthorizationPolicies` | `AuthorizationModel` n'est pas `Authenticated` | garder la policy `RequireAdmin`, `/admin/ping` et ce qui s'y rattache |

## Valeurs de `AuthorizationModel`

| Valeur | Droits portés par | Policy `RequireAdmin` | `UseAppRoles` | `UseGroups` | `UseAuthorizationPolicies` |
|--------|-------------------|-----------------------|:-------------:|:-----------:|:--------------------------:|
| `Authenticated` | rien : tout utilisateur connecté | absente | non | non | non |
| `AppRoles` | claim `roles` | rôle `Admin` | oui | non | oui |
| `Groups` | claim `groups` | appartenance au groupe `Authorization:Groups:Administrators` | non | oui | oui |
| `AppRolesAndGroups` | les deux | rôle `Admin` **ou** appartenance au groupe | oui | oui | oui |

Les explications et les compromis de chaque modèle sont dans [Modèles d'autorisation](../identite/modeles-d-autorisation.md).

## Ce que chaque option retire

Le tableau suit les règles `modifiers` de `template.json`. Une option à sa valeur par défaut (activée) ne retire rien.

| Option | Valeur | Fichiers et dossiers retirés |
|--------|--------|------------------------------|
| `EnableAspire` | `false` | `src/*.AppHost/**`, `src/*.ServiceDefaults/**`, `scripts/**` ; la solution `*.Core.slnx` (sans AppHost ni ServiceDefaults) est renommée en `*.slnx` |
| `EnableRateLimiting` | `false` | `Common/RateLimiting/**` dans l'API, `RateLimiting/**` dans les tests d'intégration |
| `EnableTests` | `false` | `tests/**` |
| `AuthorizationModel` | sans policies (`Authenticated`) | `Authorization/Policies.cs`, `Controllers/AdminController.cs`, `Application/Admin/**`, `Contracts/Admin/**` et les tests `Admin/**` correspondants |
| `AuthorizationModel` | sans rôles (`Groups`, `Authenticated`) | `Authorization/AppRoles.cs` |
| `AuthorizationModel` | sans groupes (`AppRoles`, `Authenticated`) | `Authorization/GroupOptions.cs`, tests d'intégration `Authorization/**` |

En plus des fichiers retirés, les directives `#if` du code C#, des fichiers JSON et des fichiers Markdown retirent des lignes : par exemple, sans Aspire, `Program.cs` ne contient plus `AddServiceDefaults()` ni `MapDefaultEndpoints()`, donc plus d'OpenTelemetry ni d'endpoints `/health` et `/alive` (voir [Health checks](../fonctionnalites/health-checks.md) et [Observabilité](../fonctionnalites/observabilite.md)).

## Effet sur le realm Keycloak

Le realm importé au premier démarrage d'Aspire (`src/*.AppHost/Realms/template-realm.json`) dépend du modèle d'autorisation. Les trois clients `template-api`, `template-scalar` et `template-cli` et les deux personas `admin@local.test` et `user@local.test` existent toujours ; ce qui change, ce sont les rôles et les groupes.

| `AuthorizationModel` | Rôles du realm | Groupes du realm | Contenu du jeton de `admin@local.test` |
|----------------------|----------------|------------------|-----------------------------------------|
| `Authenticated` | aucun | aucun | ni `roles` ni `groups` |
| `AppRoles` | `Admin`, `User` | aucun | `roles` = `["Admin"]` |
| `Groups` | aucun | `Administrators` | `groups` = GUID du groupe |
| `AppRolesAndGroups` | `Admin`, `User` | `Administrators` | `roles` et `groups` |

Le template conserve quatre variantes du realm dans le dépôt. À la génération, la variante qui correspond au modèle choisi est renommée en `Realms/template-realm.json` et le dossier `RealmVariants` disparaît (pour `AppRolesAndGroups`, c'est le fichier `Realms/template-realm.json` du dépôt qui est utilisé tel quel). Le realm n'est importé qu'une fois : si vous changez de modèle après un premier lancement, supprimez les volumes (voir [Keycloak en local](../identite/keycloak-local.md)).

## Ce qui est toujours exclu de la génération

Quelle que soit la combinaison d'options, ces éléments du dépôt ne se retrouvent jamais dans un projet généré : ils servent à développer le template, pas à l'utiliser.

| Élément | Raison |
|---------|--------|
| `docs/**`, `mkdocs.yml`, `.github/workflows/docs.yml` | la documentation du template |
| `eng/**` (outillage de documentation, `verify-template.sh`, pipelines Azure DevOps) | l'outillage du dépôt |
| `.github/workflows/template-ci.yml`, `Directory.Build.props` | la CI et les constantes de compilation du dépôt |
| `README.md` | le README du dépôt template ; `README.generated.md` est renommé en `README.md` |
| `.idea/**`, `.vs/**`, `.git/**`, `.cache/**`, `.pytest_cache/**`, `build/**`, `site/**`, `.superpowers/**`, `**/TestResults/**` | fichiers d'environnement et de sortie |

Voir [Scripts et CI](scripts-et-ci.md) pour la vérification automatique de ces exclusions.

## La liste complète des règles

Voici le bloc `sources` de `template.json`, source de vérité de tout ce qui précède.

```json title=".template.config/template.json"
--8<-- ".template.config/template.json" start='"sources": [' before='"guids"'
```
