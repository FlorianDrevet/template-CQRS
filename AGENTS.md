# Template CQRS : consignes pour les mainteneurs

Ce dépôt est un template `dotnet new` (`templatewebcqrs`) pour une API ASP.NET Core CQRS, pas une application. Les consignes d'un projet généré sont dans `AGENTS.generated.md` (devient `AGENTS.md` à la génération).

Pour créer un projet client à partir de ce template, utilisez le skill `init-project`.

## Structure

- `.template.config/template.json` : symboles (`EnableAspire`, `EnableRateLimiting`, `EnableTests`, `AuthorizationModel` et les calculés `UseAppRoles`, `UseGroups`, `UseAuthorizationPolicies`), exclusions et renommages.
- `Directory.Build.props` : propre au template, exclu de la génération.
- `src/Web.Template.CQRS.AppHost/RealmVariants/` : un realm Keycloak par modèle d'autorisation.
- Fichiers `*.generated.md` (`README.generated.md`, `AGENTS.generated.md`) : renommés à la génération ; `README.md` et `AGENTS.md` du dépôt sont exclus.
- `.claude/` est livré aux projets générés, sauf `.claude/skills/init-project/`. Sans Aspire, `run-local`, `.mcp.json`, `.vscode/mcp.json` et `scripts/` sont exclus.
- `docs/site` : documentation source ; `docs/wiki` est généré, ne jamais l'éditer.

## Règles

- Toute modification du code template doit garder compilables les quatre modèles d'autorisation et les options du template.
- Vérifiez les combinaisons touchées : `bash eng/verify-template.sh <AuthorizationModel> <EnableAspire> <EnableRateLimiting> <EnableTests>`.
- Tests du dépôt : `dotnet test --solution Web.Template.CQRS.slnx`.
- Directives dans les `.md` du template : `<!--#if (Symbole) -->`, `<!--#else -->`, `<!--#endif -->` chacune seule sur sa ligne.
- Après toute modification de `docs/site` : `python eng/docs/docs.py wiki && python eng/docs/docs.py check && python eng/docs/docs.py build`.

## Pour aller plus loin

- `docs/site/reference/scripts-et-ci.md` : scripts de vérification et CI.
- `docs/site/reference/contribuer-a-la-doc.md` : écrire et construire la documentation.
