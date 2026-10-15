# Template Web CQRS

Template `dotnet new` pour créer des API ASP.NET Core (.NET 10) : architecture CQRS, authentification Entra ID avec modèle d'autorisation au choix, Problem Details, limitation de débit, tests, et orchestration locale Aspire avec SQL Server, Azurite et un Keycloak qui imite Entra.

**Documentation complète** (cours pour débutants, guides, référence) : https://floriandrevet.github.io/template-CQRS/ — également publiée en wiki de code Azure DevOps depuis `docs/wiki`.

## Démarrage rapide

```powershell
dotnet new install ./
dotnet new templatewebcqrs --name Contoso.Orders
```

Options : `--AuthorizationModel` (`Authenticated`, `AppRoles`, `Groups`, `AppRolesAndGroups`), `--EnableAspire`, `--EnableRateLimiting`, `--EnableTests`. Voir `dotnet new templatewebcqrs --help` et la page « Créer un projet » de la documentation.

## Contribuer

- Template : `bash eng/verify-template.sh <AuthorizationModel> <EnableAspire> <EnableRateLimiting> <EnableTests>` génère, compile et teste une combinaison.
- Documentation : source dans `docs/site`, outillage dans `eng/docs` (`python eng/docs/docs.py check`). Voir la page « Contribuer à la doc ».
