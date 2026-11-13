---
name: init-project
description: "Créer et initialiser un nouveau projet API client à partir de ce template : questionnaire, génération dans un dossier voisin, rôles/groupes/personas, environnement local Aspire, vérification réelle et premier commit. À utiliser quand on demande de créer, initialiser ou démarrer un nouveau projet ou une nouvelle API depuis le template."
---

# Initialiser un nouveau projet

Ce skill vit **uniquement dans le dépôt du template** (il n'est pas copié dans les projets générés) et se lance depuis la racine de ce dépôt. Il enchaîne neuf étapes ; à chaque échec, **arrêtez-vous**, expliquez la cause en une ou deux phrases et proposez une remédiation sans l'appliquer d'office. Dans les commandes, `<sortie>` est le dossier du projet généré et `<Nom>` son nom.

Questions, valeurs par défaut et validations : [references/questionnaire.md](references/questionnaire.md).

## Étapes

1. **Environnement local et prérequis.**
   - **Choix du moteur, avant toute vérification.** Demandez d'abord si l'utilisateur veut **Aspire** (questionnaire, question 4) ; si oui, demandez **Docker ou Podman** (question 5) en suivant [references/container-runtime.md](references/container-runtime.md) § 1. Ces deux réponses sont retenues pour le questionnaire : ne les reposez pas à l'étape 2. Sans Aspire, il n'y a ni moteur de conteneurs ni Aspire CLI à vérifier.
   - **Vérification**, sans rien installer :
     ```bash
     dotnet --version          # doit satisfaire global.json (SDK 10.0.x)
     aspire --version          # Aspire CLI (seulement si Aspire)
     dotnet dev-certs https --check --trust   # seulement si Aspire
     aspire doctor             # (seulement si Aspire) section « Container Runtime » : le moteur choisi doit être `running` et `← active`
     ```
     Avec Aspire, vérifiez aussi le moteur lui-même (`docker info` ou `podman info`, `podman machine list`) comme dans `container-runtime.md` § 2.
   - **Si quelque chose manque**, ne vous contentez pas de le signaler : **proposez de tout configurer** (SDK, Aspire CLI, certificat, et pour le moteur choisi son installation, sa machine virtuelle et la variable `ASPIRE_CONTAINER_RUNTIME` ; Podman Desktop en option) selon `container-runtime.md` § 3 à 5. Listez d'abord l'ensemble des actions avec leurs commandes exactes, **demandez l'accord**, puis exécutez une étape à la fois en redemandant avant celles qui touchent le système (administrateur, redémarrage, `sudo`, variable d'environnement permanente). Ordre : Aspire CLI (`dotnet tool install -g Aspire.Cli`), moteur, puis `aspire doctor`. Sans accord, ne contournez pas : proposez de générer **sans Aspire** (`--aspire false`) ou d'arrêter pour que l'utilisateur configure lui-même. Ne passez à l'étape 2 que lorsque `aspire doctor` confirme le moteur choisi.
2. **Questionnaire.** Posez les questions de `references/questionnaire.md` (nom, dossier cible, modèle d'autorisation, rate limiting, tests, rôles, groupes, personas, remote git ; Aspire et le moteur de conteneurs ont déjà été posés à l'étape 1 et figurent seulement au récapitulatif). Expliquez chaque choix à un débutant. Affichez un **récapitulatif** et attendez une confirmation explicite avant toute écriture.
3. **Génération.** Depuis la racine du template (le script refuse un dossier cible non vide et ne change alors rien) :
   ```bash
   bash eng/init/new-project.sh --name <Nom> --output <sortie> --authorization <Modèle> --aspire <true|false> --container-runtime <Docker|Podman> --rate-limiting <true|false> --tests <true|false> [--remote <url>]
   ```
   PowerShell : `pwsh -NoProfile -File eng/init/new-project.ps1 -Name <Nom> -Output <sortie> -Authorization <Modèle> -Aspire <true|false> -ContainerRuntime <Docker|Podman> -RateLimiting <true|false> -Tests <true|false> [-Remote <url>]`. Dans le projet généré, `Web.Template.CQRS` est remplacé par `<Nom>` dans tous les chemins.
4. **Accès.** Lisez `<sortie>/.claude/skills/manage-access/SKILL.md` et appliquez-le **dans `<sortie>`** pour les rôles, groupes et personas du questionnaire, en une seule passe, en situation « projet fraîchement généré » (rien ne tourne : sautez la réimport du realm et la vérification live). Ne dupliquez pas ses règles ici. Modèle `Authenticated` : aucun rôle ni groupe ; seuls des personas simples sont possibles. Sans Aspire : pas de realm, pas de personas, pas de GUID local.
5. **Profil.** Écrivez `<sortie>/project-profile.json` au format de `<sortie>/.claude/skills/manage-access/references/project-profile.md` (`schemaVersion` 1, options, rôles, groupes, personas, `purpose` renseigné). `manage-access` le crée déjà à son étape 7 : complétez-le plutôt que de l'écraser, puis validez :
   ```bash
   pwsh -NoProfile -Command "Get-Content '<sortie>/project-profile.json' -Raw | ConvertFrom-Json | Out-Null; 'JSON OK'"
   ```
   (Linux/macOS sans `pwsh` : `jq . <sortie>/project-profile.json`.)
6. **Environnement local** (seulement si Aspire).
   - Secrets de l'AppHost : générez les deux valeurs **dans la commande elle-même** (jamais de valeur tapée en clair dans un appel, ni de variable affichée) ; le préfixe `Aa1_` (pas de `!`, que le shell interprète) garantit la complexité exigée par SQL Server. Elles vont dans les user-secrets, **hors dépôt** ; ne les affichez ni ne les écrivez dans un fichier du projet :
     ```bash
     command -v openssl >/dev/null && dotnet user-secrets set "Parameters:sql-password" "Aa1_$(openssl rand -hex 16)" --project <sortie>/src/<Nom>.AppHost/<Nom>.AppHost.csproj && dotnet user-secrets set "Parameters:keycloak-admin-password" "Aa1_$(openssl rand -hex 16)" --project <sortie>/src/<Nom>.AppHost/<Nom>.AppHost.csproj || echo "openssl introuvable : utilisez la variante PowerShell" >&2
     ```
     Les deux `set` ne s'exécutent que si openssl existe (une seule commande, chaînée) : sans openssl, aucun secret n'est écrit. Si openssl est absent, utilisez la variante PowerShell ; n'enregistrez jamais un secret sans sa partie aléatoire.
     PowerShell : `dotnet user-secrets set "Parameters:sql-password" ("Aa1_" + [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16))) --project <csproj>` (idem pour `Parameters:keycloak-admin-password`).
   - Skills et MCP Aspire : cette commande installe des hooks de télémétrie. **Demandez un accord explicite** avant de la lancer, en mentionnant l'opt-out (variable d'environnement `ASPIRE_CLI_TELEMETRY_OPTOUT=true`, rappelée par la CLI après l'init) :
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
   aspire wait keycloak --timeout 300 --apphost src/<Nom>.AppHost/<Nom>.AppHost.csproj   # premier démarrage : plusieurs minutes
   aspire wait api --timeout 300 --apphost src/<Nom>.AppHost/<Nom>.AppHost.csproj
   bash scripts/smoke-test.sh        # PowerShell : ./scripts/smoke-test.ps1
   aspire stop --apphost src/<Nom>.AppHost/<Nom>.AppHost.csproj
   ```
   Si le port 8080 est déjà pris, la cause probable est un conteneur Keycloak persistant d'un autre projet : expliquez-le (`docker ps --filter publish=8080`, ou `podman ps` avec Podman) et proposez de l'arrêter, **sans le faire** sans accord. Si le port reste pris après `docker stop` (`podman stop` avec Podman), un autre AppHost tourne (`aspire ps`) : demandez l'accord de l'arrêter (`aspire stop --apphost <csproj>`) ou arrêtez-vous. Ne passez jamais `--volumes` ici.
8. **Git.** Dans `<sortie>` (le dépôt a déjà été initialisé et le remote ajouté par le script) :
   ```bash
   git add -A
   git commit -m "chore: initialize <Nom> from the Web CQRS template"
   ```
   Aucun `git push` sans accord explicite ; si un remote est configuré, proposez la commande et attendez.
9. **Passation.** Affichez un résumé : dossier créé, options retenues, rôles/groupes/personas (mot de passe local `Passw0rd!`, local uniquement), commandes utiles (`aspire start`, `dotnet test --solution <Nom>.slnx`, `scripts/get-token.*`, `scripts/smoke-test.*`), étapes Entra restantes (voir `manage-access`) et prochaines étapes : skill `add-feature` pour la première fonctionnalité, `run-local` pour l'environnement, documentation du template https://floriandrevet.github.io/template-CQRS/. Si `aspire agent init` a été lancé, rappelez la télémétrie désactivable avec `ASPIRE_CLI_TELEMETRY_OPTOUT=true`. Rappelez d'ouvrir le projet généré (`<sortie>`) dans une nouvelle session.

## Terminé quand

- Le projet est généré dans `<sortie>` et les accès demandés sont appliqués (`project-profile.json` valide).
- `dotnet build` et `dotnet test` sont verts.
- Le smoke test est OK (si Aspire), puis l'environnement est arrêté.
- Le premier commit existe.
- La passation a été affichée.

## Ne jamais faire sans demander

- Installer un outil (SDK, Aspire CLI, certificat de développement, Docker, Podman, Podman Desktop, WSL), créer ou démarrer la machine Podman, poser une variable d'environnement permanente (`ASPIRE_CONTAINER_RUNTIME`), ou lancer `aspire agent init` (hooks de télémétrie). Les installations de Podman, Podman Desktop et Docker peuvent demander une élévation (UAC), `sudo` ou un redémarrage : annoncez-le avant l'accord.
- Écraser ou vider un dossier existant (le script refuse, ne le contournez pas).
- Pousser sur un remote (`git push`).
- Supprimer des volumes (`--volumes`) ou arrêter des conteneurs d'autres projets (Keycloak sur le port 8080).
- Écrire un secret ou un mot de passe généré dans un fichier du dépôt ou l'afficher.
