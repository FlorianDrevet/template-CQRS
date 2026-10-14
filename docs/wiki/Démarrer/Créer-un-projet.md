# Créer un projet

## Le concept simplement

Créer un projet se fait en deux temps : installer le template une fois sur votre machine, puis le générer autant de fois que nécessaire avec `dotnet new`. Les options choisies à la génération déterminent quels projets, fichiers et blocs de code se retrouvent dans le résultat. Rien ne se paramètre après coup : ce qui n'est pas généré n'existe tout simplement pas.

## Pourquoi c'est important

Chaque option retire de la complexité. Une API interne simple n'a pas besoin de groupes de sécurité ; un environnement sans Docker ne peut pas utiliser Aspire. Choisir les bonnes options au départ évite de supprimer à la main des dizaines de fichiers.

## Comment le template le fait

### Installer le template et lire l'aide

Depuis la racine du dépôt cloné :

```bash
dotnet new install ./
dotnet new templatewebcqrs --help
```

La seconde commande affiche toutes les options avec leur description, lue dans `template.json`.

### Les options

| Option | Valeurs | Défaut | Effet |
|--------|---------|--------|-------|
| `-n`, `--name` | un nom de projet, par exemple `Contoso.Orders` | nom du dossier | Remplace `Web.Template.CQRS` partout (voir [Comprendre les templates](/Démarrer/Comprendre-les-templates.md)) |
| `--EnableAspire` | `true`, `false` | `true` | Ajoute Aspire (AppHost, ServiceDefaults), l'observabilité OpenTelemetry, les scripts et les services locaux SQL Server, Azurite et Keycloak |
| `--EnableRateLimiting` | `true`, `false` | `true` | Ajoute les politiques de limitation de débit de l'API |
| `--AuthorizationModel` | `Authenticated`, `AppRoles`, `Groups`, `AppRolesAndGroups` | `AppRoles` | Choisit le modèle d'autorisation Entra ID reproduit localement par Keycloak |
| `--EnableTests` | `true`, `false` | `true` | Ajoute les projets de tests unitaires et d'intégration |

### Ce que chaque option ajoute ou retire

| Option | Ce qui disparaît quand elle est désactivée ou différente |
|--------|-----------------------------------------------------------|
| `EnableAspire = false` | Projets `AppHost` et `ServiceDefaults`, dossier `scripts/`. La solution `Web.Template.CQRS.Core.slnx` est renommée en `.slnx` principal |
| `EnableRateLimiting = false` | Dossier `Common/RateLimiting` de l'API et ses tests d'intégration |
| `EnableTests = false` | Dossier `tests/` |
| `AuthorizationModel = Authenticated` | `Authorization/Policies.cs`, `AdminController`, la feature `Admin` (Application et Contracts) et ses tests ; le realm Keycloak ne contient plus de rôles ni de groupes |
| `AuthorizationModel = Groups` | `Authorization/AppRoles.cs` |
| `AuthorizationModel = AppRoles` | `Authorization/GroupOptions.cs` et les tests d'autorisation par groupes |
| `AuthorizationModel = AppRolesAndGroups` | Rien : les deux mécanismes sont présents |

Le choix du modèle sélectionne aussi, parmi `RealmVariants/`, le fichier realm Keycloak qui devient `Realms/template-realm.json`. Les modèles sont détaillés dans [Modèles d'autorisation](/Identité/Modèles-d'autorisation.md).

### Quatre scénarios types

**API interne simple**


Les utilisateurs doivent seulement être connectés. Pas de rôles, pas de groupes.

```bash
dotnet new templatewebcqrs -n Contoso.Orders --AuthorizationModel Authenticated
```

**Back-office avec rôles**


Certaines actions sont réservées aux administrateurs, via des app roles Entra ID (c'est le défaut).

```bash
dotnet new templatewebcqrs -n Contoso.Orders --AuthorizationModel AppRoles
```

**Application d'entreprise pilotée par groupes**


Les droits viennent de l'appartenance à des groupes de sécurité Entra ID.

```bash
dotnet new templatewebcqrs -n Contoso.Orders --AuthorizationModel Groups
```

**Sans Aspire**


Environnement contraint sans Docker. Vous fournissez vous-même SQL, stockage et fournisseur d'identité.

```bash
dotnet new templatewebcqrs -n Contoso.Orders --EnableAspire false
```

> ⚠️ **Sans Aspire, plus de services locaux**
>
> Le projet généré ne démarre pas SQL Server, Azurite ni Keycloak. Configurez `Auth:*` (Entra ID), `ConnectionStrings:projectdb` et `ConnectionStrings:blobs` dans les User Secrets, puis lancez `dotnet run --project src/Contoso.Orders.Api/Contoso.Orders.Api.csproj`.

### Ce que vous obtenez

Le dossier généré contient `src/` (les projets), `tests/` (si activés), `scripts/` (avec Aspire), un `README.md` propre au projet et la solution `.slnx`. Le détail de chaque dossier se trouve dans [Arborescence](/Référence/Arborescence.md) et la liste complète des options dans [Options du template](/Référence/Options-du-template.md).

## À essayer

1. Générez un projet dans un dossier temporaire avec les options par défaut, puis un second avec `--AuthorizationModel Authenticated --EnableTests false`.
2. Comparez les deux : le second contient-il `AdminController.cs` ? un dossier `tests/` ?
3. Dans le premier, ouvrez `Program.cs` de l'API : cherchez `RateLimiting`. Dans un troisième projet généré avec `--EnableRateLimiting false`, la même recherche ne donne plus rien.
4. Compilez chaque projet avec `dotnet build` pour vérifier que les variantes restent cohérentes.

Passez ensuite à [Premier lancement](/Démarrer/Premier-lancement.md).
