---
name: init-project
description: "Créer et initialiser un nouveau projet API client à partir de ce template : questionnaire, génération dans un dossier voisin, rôles/groupes/personas, environnement local Aspire, vérification réelle et premier commit. À utiliser quand on demande de créer, initialiser ou démarrer un nouveau projet ou une nouvelle API depuis le template."
---

# Initialiser un nouveau projet

Ce skill vit **uniquement dans le dépôt du template** (il n'est pas copié dans les projets générés) et se lance depuis la racine de ce dépôt. Il enchaîne neuf étapes ; à chaque échec, **arrêtez-vous**, expliquez la cause en une ou deux phrases et proposez une remédiation sans l'appliquer d'office. Dans les commandes, `<sortie>` est le dossier du projet généré et `<Nom>` son nom.

Questions, valeurs par défaut et validations : [references/questionnaire.md](references/questionnaire.md).

## Étapes

1. **Prérequis.** Vérifiez, sans rien installer :
   ```bash
   dotnet --version          # doit satisfaire global.json (SDK 10.0.x)
   docker info               # Docker doit tourner (seulement si Aspire)
   aspire --version          # Aspire CLI (seulement si Aspire)
   dotnet dev-certs https --check --trust
   ```
   Pour chaque prérequis manquant, donnez la commande d'installation (`dotnet dev-certs https --trust`, installation de l'Aspire CLI, démarrage de Docker Desktop) et **demandez l'accord** avant de l'exécuter. Sans accord, arrêtez-vous.
2. **Questionnaire.** Posez les questions de `references/questionnaire.md` (nom, dossier cible, modèle d'autorisation, Aspire, rate limiting, tests, rôles, groupes, personas, remote git). Expliquez chaque choix à un débutant. Affichez un **récapitulatif** et attendez une confirmation explicite avant toute écriture.
3. **Génération.** Depuis la racine du template (le script refuse un dossier cible non vide et ne change alors rien) :
   ```bash
   bash eng/init/new-project.sh --name <Nom> --output <sortie> --authorization <Modèle> --aspire <true|false> --rate-limiting <true|false> --tests <true|false> [--remote <url>]
   ```
   PowerShell : `pwsh -NoProfile -File eng/init/new-project.ps1 -Name <Nom> -Output <sortie> -Authorization <Modèle> -Aspire $true -RateLimiting $true -Tests $true [-Remote <url>]`. Dans le projet généré, `Web.Template.CQRS` est remplacé par `<Nom>` dans tous les chemins.
4. **Accès.** Lisez `<sortie>/.claude/skills/manage-access/SKILL.md` et appliquez-le **dans `<sortie>`** pour les rôles, groupes et personas du questionnaire, en une seule passe, en situation « projet fraîchement généré » (rien ne tourne : sautez la réimport du realm et la vérification live). Ne dupliquez pas ses règles ici. Modèle `Authenticated` : aucun rôle ni groupe ; seuls des personas simples sont possibles. Sans Aspire : pas de realm, pas de personas, pas de GUID local.
5. **Profil.** Écrivez `<sortie>/project-profile.json` au format de `<sortie>/.claude/skills/manage-access/references/project-profile.md` (`schemaVersion` 1, options, rôles, groupes, personas, `purpose` renseigné). `manage-access` le crée déjà à son étape 7 : complétez-le plutôt que de l'écraser, puis validez :
   ```bash
   python -c "import json; json.load(open('<sortie>/project-profile.json', encoding='utf-8')); print('JSON OK')"
   ```
6. **Environnement local** (seulement si Aspire).
   - Secrets de l'AppHost : générez deux valeurs aléatoires fortes (par exemple `openssl rand -base64 24`, ou en PowerShell `[Convert]::ToBase64String((1..24 | ForEach-Object { Get-Random -Maximum 256 }))`) et enregistrez-les dans les user-secrets, **hors dépôt** ; ne les affichez pas, ne les écrivez dans aucun fichier du projet :
     ```bash
     dotnet user-secrets set "Parameters:sql-password" "<valeur générée>" --project <sortie>/src/<Nom>.AppHost/<Nom>.AppHost.csproj
     dotnet user-secrets set "Parameters:keycloak-admin-password" "<valeur générée>" --project <sortie>/src/<Nom>.AppHost/<Nom>.AppHost.csproj
     ```
   - Skills et MCP Aspire (prévenez l'utilisateur que cette commande installe des hooks de télémétrie ; l'opt-out est la variable d'environnement `ASPIRE_CLI_TELEMETRY_OPTOUT=true`) :
     ```bash
     aspire agent init --non-interactive --workspace-root <sortie> --skill-locations claudecode --skills aspire,aspire-orchestration,aspire-monitoring --mcp
     ```
7. **Vérification.** Depuis `<sortie>` :
   ```bash
   dotnet build <Nom>.slnx
   dotnet test --solution <Nom>.slnx
   ```
   (sans tests générés : seulement le build). Corrigez la cause d'un échec ; n'assouplissez aucun test. Puis, **si Aspire** :
   ```bash
   aspire start --apphost src/<Nom>.AppHost/<Nom>.AppHost.csproj
   bash scripts/smoke-test.sh        # PowerShell : ./scripts/smoke-test.ps1
   aspire stop --apphost src/<Nom>.AppHost/<Nom>.AppHost.csproj
   ```
   Si le port 8080 est déjà pris, la cause probable est un conteneur Keycloak persistant d'un autre projet : expliquez-le (`docker ps --filter publish=8080`) et proposez de l'arrêter, **sans le faire** sans accord. Ne passez jamais `--volumes` ici.
8. **Git.** Dans `<sortie>` (le dépôt a déjà été initialisé et le remote ajouté par le script) :
   ```bash
   git add -A
   git commit -m "chore: initialize <Nom> from the Web CQRS template"
   ```
   Aucun `git push` sans accord explicite ; si un remote est configuré, proposez la commande et attendez.
9. **Passation.** Affichez un résumé : dossier créé, options retenues, rôles/groupes/personas (mot de passe local `Passw0rd!`, local uniquement), commandes utiles (`aspire start`, `dotnet test --solution <Nom>.slnx`, `scripts/get-token.*`, `scripts/smoke-test.*`), étapes Entra restantes (voir `manage-access`) et prochaines étapes : skill `add-feature` pour la première fonctionnalité, `run-local` pour l'environnement, dossier `docs/` pour la documentation. Rappelez d'ouvrir le projet généré (`<sortie>`) dans une nouvelle session.

## Terminé quand

- Le projet est généré dans `<sortie>` et les accès demandés sont appliqués (`project-profile.json` valide).
- `dotnet build` et `dotnet test` sont verts.
- Le smoke test est OK (si Aspire), puis l'environnement est arrêté.
- Le premier commit existe.
- La passation a été affichée.

## Ne jamais faire sans demander

- Installer un outil (SDK, Aspire CLI, certificat de développement).
- Écraser ou vider un dossier existant (le script refuse, ne le contournez pas).
- Pousser sur un remote (`git push`).
- Supprimer des volumes (`--volumes`) ou arrêter des conteneurs d'autres projets (Keycloak sur le port 8080).
- Écrire un secret ou un mot de passe généré dans un fichier du dépôt ou l'afficher.
