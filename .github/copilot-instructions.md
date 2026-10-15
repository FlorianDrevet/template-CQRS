# Instructions Copilot

Lisez `AGENTS.md` à la racine en premier : il décrit la carte du code, les conventions et les interdits du projet.

Règles essentielles :

- Respectez les couches : Domain ne référence rien, Application ne référence jamais Infrastructure, un endpoint ne contient pas de logique métier.
- Déclarez l'autorisation explicitement sur chaque endpoint (`RequireAuthorization` ou une politique `Policies.*`).
<!--#if (EnableTests) -->
- Ajoutez ou mettez à jour les tests de tout changement de comportement, puis lancez `dotnet test --solution Web.Template.CQRS.slnx`.
<!--#endif -->
- Entra ID est la cible d'authentification : n'écrivez pas de code propre à un fournisseur d'identité local.
<!--#if (EnableAspire) -->
- L'API se lance en HTTPS sur `https://localhost:7246` uniquement.
<!--#endif -->

Skills disponibles dans `.claude/skills/` :

- `add-feature` : ajouter une query ou une commande de bout en bout.
- `manage-access` : ajouter ou modifier un rôle, un groupe ou une politique.
<!--#if (EnableAspire) -->
- `run-local` : démarrer l'environnement local.
<!--#endif -->

Relecture : l'agent `reviewer` (`.github/agents/reviewer.agent.md`) relit tout changement de code avant de le présenter comme terminé.
