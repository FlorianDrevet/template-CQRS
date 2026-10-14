# Template Web CQRS

Template de solution ASP.NET Core CQRS sous .NET 10. Il comprend l'authentification bearer Entra ID, un modèle d'autorisation au choix (app roles, groupes de sécurité ou les deux), des erreurs Problem Details, la limitation de débit, des projets de tests et une orchestration locale Aspire avec OpenTelemetry et un Keycloak qui imite Entra.

## Installer le template

Depuis la racine du dépôt :

```powershell
dotnet new install ./
dotnet new templatewebcqrs --help
```

## Générer une API

```powershell
dotnet new templatewebcqrs --name Contoso.Orders
```

| Option | Valeurs | Défaut | Effet |
|---|---|---|---|
| `--AuthorizationModel` | `Authenticated`, `AppRoles`, `Groups`, `AppRolesAndGroups` | `AppRoles` | Policies générées et forme du realm Keycloak local |
| `--EnableAspire` | `true` / `false` | `true` | AppHost, ServiceDefaults, OpenTelemetry, SQL Server, Azurite et Keycloak locaux |
| `--EnableRateLimiting` | `true` / `false` | `true` | Politiques `read`, `write`, `generate`, `publish` |
| `--EnableTests` | `true` / `false` | `true` | Projets `tests/*.UnitTests` et `tests/*.IntegrationTests` |

Exemple : `dotnet new templatewebcqrs --name Contoso.Orders --AuthorizationModel AppRolesAndGroups --EnableRateLimiting false`.

## Modèles d'autorisation

Entra ID est toujours la cible. Le code de l'API lit les claims `oid`, `roles` et `groups` d'un access token Entra ; en local, Keycloak émet des tokens de même forme.

| Modèle | Côté Entra | Côté API |
|---|---|---|
| `Authenticated` | Aucun droit fin | Tout endpoint exige un utilisateur authentifié |
| `AppRoles` | App roles `Admin` et `User` déclarés dans l'App Registration, assignés aux utilisateurs | Policy `RequireAdmin` = rôle `Admin` |
| `Groups` | Claim de groupes activé (Token configuration → Add groups claim → Security groups, Group ID) | Policy `RequireAdmin` = membre du groupe `Authorization:Groups:Administrators` |
| `AppRolesAndGroups` | Les deux | `RequireAdmin` = rôle `Admin` **ou** membre du groupe |

Les object IDs de groupes ne sont jamais dans le code : configurez `Authorization:Groups:Administrators` avec l'object ID du groupe Entra (variable d'environnement `Authorization__Groups__Administrators`). L'application refuse de démarrer si la valeur n'est pas un GUID. Au-delà de 200 groupes par utilisateur, Entra n'émet plus le claim `groups` : ce cas n'est pas géré par le template.

L'endpoint `GET /me` renvoie l'identifiant, le nom, l'e-mail, les rôles et les groupes du token : c'est le premier outil de diagnostic. `GET /admin/ping` (hors modèle `Authenticated`) illustre un endpoint protégé par `RequireAdmin`.

## Lancer l'API sans Aspire

Cette section concerne une génération avec `--EnableAspire false`. Configurez Entra, les groupes éventuels ainsi que les chaînes de connexion SQL Server et Blob Storage dans User Secrets ou vos variables d'environnement, puis lancez l'API :

```powershell
dotnet run --project src/Contoso.Orders.Api/Contoso.Orders.Api.csproj
```

Pour construire l'image API depuis la racine du projet généré :

```powershell
docker build --file src/Contoso.Orders.Api/Dockerfile --tag contoso-orders .
```

## Lancer la pile locale Aspire

Prérequis : installez l'Aspire CLI avec `dotnet tool install -g Aspire.Cli` si nécessaire, démarrez Docker Desktop et approuvez une fois le certificat de développement ASP.NET Core :

```powershell
dotnet dev-certs https --trust
```

Keycloak et l'API l'utilisent en local ; sans cette approbation, les demandes de token et la récupération des métadonnées JWT par l'API échouent avec des erreurs TLS.

Depuis le dossier créé, ajoutez les deux paramètres secrets de l'AppHost :

```powershell
dotnet user-secrets set "Parameters:sql-password" "Choose-A-Strong-Local-Password!" --project src/Contoso.Orders.AppHost/Contoso.Orders.AppHost.csproj
dotnet user-secrets set "Parameters:keycloak-admin-password" "Choose-Another-Strong-Password!" --project src/Contoso.Orders.AppHost/Contoso.Orders.AppHost.csproj
aspire start --apphost src/Contoso.Orders.AppHost/Contoso.Orders.AppHost.csproj
```

Aspire démarre SQL Server, Azurite et Keycloak (`https://localhost:8080`, servi en HTTPS par l'intégration Aspire Keycloak avec le certificat de développement), puis l'API. Le tableau de bord affiche les traces, métriques, journaux et l'état de santé des ressources. L'API expose `/health` et `/alive` en développement.

Appelez l'API avec un token sur `https://localhost:7246` : `http://localhost:5257` redirige (307) vers HTTPS et les clients HTTP abandonnent l'en-tête `Authorization` lors de la redirection.

## Keycloak local : personas et tokens

Le realm `template` est importé au démarrage. Il imite Entra : claims `oid`, `roles` (plat) et `groups` (object IDs), audience `template-api`.

| Persona | Mot de passe | Rôles (`AppRoles*`) | Groupes (`*Groups`) |
|---|---|---|---|
| `admin@local.test` | `Passw0rd!` | `Admin` | `Administrators` (`6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11`) |
| `user@local.test` | `Passw0rd!` | `User` | aucun |

Ces comptes et mots de passe n'existent que dans le realm local ; ils ne doivent jamais être réutilisés ailleurs.

Obtenir un token sans navigateur (client `template-cli`, password grant, local uniquement, `https://localhost:8080`) :

```powershell
./scripts/get-token.ps1 -Persona admin
```

```bash
./scripts/get-token.sh admin
```

Collez le token dans `src/Contoso.Orders.Api/Api.http` (`@AccessToken`, requêtes vers `https://localhost:7246`) ou utilisez Scalar sur `/scalar` (client `template-scalar`, connexion interactive).

**Important : Keycloak n'importe le realm qu'au premier démarrage.** Le conteneur et son volume sont persistants : une modification de `src/Contoso.Orders.AppHost/Realms/template-realm.json` n'est appliquée qu'après suppression du volume. Le volume se nomme `<nom-du-projet-apphost-en-minuscules>-<hash>-keycloak-data` (par exemple `contoso.orders.apphost-<hash>-keycloak-data`) et le conteneur `keycloak-<suffixe>`. Arrêtez l'AppHost, puis :

```bash
docker volume ls --filter name=keycloak-data
docker ps -a --filter name=keycloak
docker rm -f <conteneur-keycloak>
docker volume rm <volume-keycloak-du-projet>
```

## Authentification Entra

L'API valide les access tokens Entra avec `Microsoft.Identity.Web`. Configurez les valeurs `Auth` par variables d'environnement, User Secrets ou un fournisseur de configuration externe ; ne stockez pas les identifiants de tenant dans le dépôt.

Les clés principales sont `Auth:Provider=Entra`, `Auth:Instance`, `Auth:TenantId` et `Auth:ClientId`. Pour activer la connexion interactive depuis Scalar en développement, ajoutez `Auth:OpenApiClientId` et `Auth:OpenApiScope`, et enregistrez l'URL de rappel Scalar dans l'application cliente Entra. Déclarez dans l'App Registration de l'API les app roles dont les valeurs correspondent aux constantes de `Api/Authorization/AppRoles.cs`.

## Tests

```powershell
dotnet test --solution Contoso.Orders.slnx
```

Les tests d'intégration démarrent l'API en mémoire avec un schéma d'authentification de test : les en-têtes `X-Test-User`, `X-Test-Roles` et `X-Test-Groups` construisent l'utilisateur, sans Keycloak ni Entra. SQL Server est remplacé par EF Core InMemory et le stockage blob par un faux.

## Options intégrées

- **Aspire** : AppHost, ServiceDefaults, traces et métriques OpenTelemetry, logs structurés, health checks, découverte de services et résilience HTTP.
- **Services locaux Aspire** : SQL Server, Azurite et Keycloak avec import d'un realm qui imite Entra.
- **Rate limiting** : politiques nommées `read`, `write`, `generate` et `publish`, partitionnées par identifiant d'utilisateur ou adresse IP.
- **Gestion d'erreurs** : réponses Problem Details, validation en 400, erreurs inattendues en 500 avec `traceId`.
- **Identité** : Entra en configuration standard, Keycloak pour le lancement local avec Aspire.

## Développer le template

- `Directory.Build.props` (exclu de la génération) définit toutes les constantes de template : le dépôt compile et teste la variante la plus complète (`AppRolesAndGroups`, Aspire, rate limiting, tests).
- `eng/verify-template.sh <AuthorizationModel> <EnableAspire> <EnableRateLimiting> <EnableTests>` génère une solution, vérifie les exclusions, la compile et lance ses tests.
- `.github/workflows/template-ci.yml` (GitHub Actions) et `eng/azure-pipelines/template-ci.yml` (Azure DevOps) exécutent la même matrice de six combinaisons, valident les fichiers de realm et testent les sources du template. Côté Azure DevOps, créez le pipeline à partir de ce fichier YAML existant ; avec Azure Repos, la validation des pull requests passe par une branch policy sur `main`.
- `src/Web.Template.CQRS.AppHost/Realms/template-realm.json` est la variante complète ; `RealmVariants/` contient les autres variantes, renommées à la génération.
