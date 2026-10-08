# Template Web CQRS

Template de solution ASP.NET Core CQRS sous .NET 10. Il comprend l'authentification bearer Entra, des erreurs Problem Details et, par défaut, la limitation de débit et une orchestration locale Aspire avec OpenTelemetry et Keycloak.

## Installer le template

Depuis la racine du dépôt :

```powershell
dotnet new install ./
```

Pour afficher les options disponibles :

```powershell
dotnet new templatewebcqrs --help
```

## Générer une API

```powershell
dotnet new templatewebcqrs --name Contoso.Orders
```

Les options de génération activées par défaut sont `--EnableAspire true` et `--EnableRateLimiting true`.

Pour générer l'API sans AppHost Aspire ni observabilité OpenTelemetry :

```powershell
dotnet new templatewebcqrs --name Contoso.Orders --EnableAspire false
```

Pour désactiver les politiques de limitation de débit :

```powershell
dotnet new templatewebcqrs --name Contoso.Orders --EnableRateLimiting false
```

## Lancer l'API sans Aspire

Cette section concerne une génération avec `--EnableAspire false`. Configurez Entra ainsi que les chaînes de connexion SQL Server et Blob Storage dans User Secrets ou vos variables d'environnement, puis lancez l'API :

```powershell
dotnet run --project src/Contoso.Orders.Api/Contoso.Orders.Api.csproj
```

Pour construire l'image API depuis la racine du projet généré, avec ou sans Aspire :

```powershell
docker build --file src/Contoso.Orders.Api/Dockerfile --tag contoso-orders .
```

## Lancer la pile locale Aspire

Cette section concerne une génération avec `--EnableAspire true`.

Installez l'Aspire CLI avec `dotnet tool install -g Aspire.Cli` si nécessaire. Docker Desktop ou un runtime de conteneurs compatible doit être démarré. Depuis le dossier créé, ajoutez les deux paramètres secrets dans le magasin User Secrets de l'AppHost :

```powershell
dotnet user-secrets set "Parameters:sql-password" "Choose-A-Strong-Local-Password!" --project src/Contoso.Orders.AppHost/Contoso.Orders.AppHost.csproj
dotnet user-secrets set "Parameters:keycloak-admin-password" "Choose-Another-Strong-Password!" --project src/Contoso.Orders.AppHost/Contoso.Orders.AppHost.csproj
```

Puis démarrez l'AppHost :

```powershell
aspire start --apphost src/Contoso.Orders.AppHost/Contoso.Orders.AppHost.csproj
```

Aspire démarre SQL Server, Azurite et Keycloak, puis l'API. Le tableau de bord affiche les traces, métriques, journaux et l'état de santé des ressources. L'API expose `/health` et `/alive` en développement.

Le realm `template` et les clients `template-api` et `template-scalar` sont importés au démarrage. Créez un utilisateur local depuis la console d'administration Keycloak, puis authentifiez-vous dans Scalar sur `/scalar`.

## Authentification Entra

L'API valide les access tokens Entra avec `Microsoft.Identity.Web`. Configurez les valeurs `Auth` par variables d'environnement, User Secrets ou un fournisseur de configuration externe ; ne stockez pas les identifiants de tenant dans le dépôt.

Les clés principales sont `Auth:Provider=Entra`, `Auth:Instance`, `Auth:TenantId` et `Auth:ClientId`. Pour activer la connexion interactive depuis Scalar en développement, ajoutez aussi `Auth:OpenApiClientId` et `Auth:OpenApiScope`, et enregistrez l'URL de rappel Scalar dans l'application cliente Entra.

L'endpoint protégé `GET /me` montre comment récupérer l'identifiant, le nom et l'adresse électronique à partir des revendications du jeton. L'API n'émet pas elle-même de jetons et ne conserve pas les mots de passe des utilisateurs.

## Options intégrées

- **Aspire** (`EnableAspire=true` par défaut) : AppHost, ServiceDefaults, traces et métriques OpenTelemetry, logs structurés, health checks, découverte de services et résilience HTTP.
- **Services locaux Aspire** (`EnableAspire=true`) : SQL Server, Azurite et Keycloak avec import d'un realm de développement.
- **Rate limiting** (`EnableRateLimiting=true` par défaut) : politiques nommées `read`, `write`, `generate` et `publish`, partitionnées par identifiant d'utilisateur ou adresse IP.
- **Gestion d'erreurs** : réponses Problem Details, validation en 400, erreurs inattendues en 500 avec `traceId`.
- **Identité** : Entra en configuration standard, Keycloak pour le lancement local avec Aspire.
