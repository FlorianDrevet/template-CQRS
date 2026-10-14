# Web.Template.CQRS

API ASP.NET Core (.NET 10) générée depuis le template Web CQRS : architecture CQRS, Problem Details, authentification Entra ID.
<!--#if (EnableAspire) -->
L'orchestration locale Aspire (SQL Server, Azurite, Keycloak) est incluse.
<!--#endif -->
<!--#if (EnableRateLimiting) -->
La limitation de débit est activée.
<!--#endif -->

Documentation complète (cours, guides, référence) : https://floriandrevet.github.io/template-CQRS/

<!--#if (EnableAspire) -->
## Lancer en local

Prérequis : SDK .NET 10, Docker, Aspire CLI, certificat de développement approuvé (`dotnet dev-certs https --trust`).

```powershell
dotnet user-secrets set "Parameters:sql-password" "Choose-A-Strong-Local-Password!" --project src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj
dotnet user-secrets set "Parameters:keycloak-admin-password" "Choose-Another-Strong-Password!" --project src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj
aspire start --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj
```

Obtenir un token pour un persona local puis appeler l'API sur `https://localhost:7246` :

```powershell
./scripts/get-token.ps1 -Persona admin
```

Personas : `admin@local.test` et `user@local.test` (mot de passe `Passw0rd!`, realm local uniquement).
<!--#else -->
## Lancer en local

Configurez `Auth:*` (Entra ID), `ConnectionStrings:projectdb` et `ConnectionStrings:blobs` dans les User Secrets, puis :

```powershell
dotnet run --project src/Web.Template.CQRS.Api/Web.Template.CQRS.Api.csproj
```
<!--#endif -->
<!--#if (EnableTests) -->

## Tests

```powershell
dotnet test --solution Web.Template.CQRS.slnx
```
<!--#endif -->

## Configuration en déploiement

Fournissez par variables d'environnement :

- `Auth__Provider=Entra`, `Auth__Instance`, `Auth__TenantId`, `Auth__ClientId`
<!--#if (UseGroups) -->
- `Authorization__Groups__Administrators` (object ID du groupe Entra)
<!--#endif -->
- `ConnectionStrings__projectdb` et `ConnectionStrings__blobs`
- `APPLICATIONINSIGHTS_CONNECTION_STRING` pour la télémétrie
