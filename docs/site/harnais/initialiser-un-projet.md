# Initialiser un projet

Cette page explique comment créer un projet client à partir du template avec l'aide d'un agent, ce que cet agent fait étape par étape, et comment obtenir le même résultat sans IA avec les scripts `new-project`.

## Le concept simplement

Créer un projet demande une dizaine de décisions (nom, modèle d'autorisation, Aspire, rôles, personas...) puis de nombreuses vérifications (build, tests, démarrage de l'environnement). Le skill `init-project` pose les questions à votre place, génère le projet dans un dossier voisin, applique vos rôles et personas, puis **prouve** que tout fonctionne avant de vous rendre la main.

Ce skill vit **uniquement dans le dépôt du template** : il n'est jamais copié dans un projet généré. Il se lance donc depuis un clone du template.

## Pourquoi c'est important

- Vous ne partez pas d'un projet « qui devrait marcher » mais d'un projet dont le build, les tests et le smoke test ont réellement réussi.
- Les rôles, groupes et personas sont cohérents entre le code, le realm Keycloak local et la configuration dès le premier jour.
- Les actions risquées (installer un outil, pousser sur un remote, supprimer des volumes) demandent toujours votre accord.

## Comment le template le fait

### Demander à l'agent

1. Clonez le dépôt du template et ouvrez-le dans Claude Code ou dans Copilot (VS Code, mode agent).
2. Écrivez une demande comme :

```text
Initialise un nouveau projet nommé Contoso.Orders à partir de ce template.
```

L'agent reconnaît la demande grâce à la `description` du skill, dont voici l'en-tête :

```yaml title=".claude/skills/init-project/SKILL.md"
--8<-- ".claude/skills/init-project/SKILL.md" start="name: init-project" end="description:"
```

### Le déroulé du skill

Le skill enchaîne neuf étapes. À chaque échec, l'agent s'arrête, explique la cause en une ou deux phrases et propose une remédiation **sans l'appliquer d'office**.

| # | Étape | Ce qui se passe |
|---|-------|-----------------|
| 1 | Prérequis | vérifie le SDK .NET, Docker, le CLI Aspire et le certificat de développement ; propose les commandes d'installation manquantes et demande votre accord |
| 2 | Questionnaire | pose les questions, explique chaque choix, affiche un récapitulatif et attend votre confirmation avant d'écrire quoi que ce soit |
| 3 | Génération | lance `eng/init/new-project.sh` (ou `.ps1`) ; le script refuse un dossier cible non vide |
| 4 | Accès | applique vos rôles, groupes et personas en suivant le skill `manage-access` du projet généré |
| 5 | Profil | écrit `project-profile.json` |
| 6 | Environnement local | (avec Aspire) génère les secrets de l'AppHost sans les afficher, puis lance `aspire agent init` après votre accord |
| 7 | Vérification | `dotnet build`, `dotnet test --solution`, puis (avec Aspire) `aspire start`, `smoke-test`, `aspire stop` |
| 8 | Git | premier commit ; aucun `git push` sans accord |
| 9 | Passation | résumé : dossier créé, options, personas, commandes utiles, étapes Entra restantes |

Le questionnaire couvre : le nom du projet (PascalCase avec points, par exemple `Contoso.Orders`), le dossier cible (par défaut `../<Nom>`), le modèle d'autorisation (`Authenticated`, `AppRoles`, `Groups`, `AppRolesAndGroups`), Aspire, le rate limiting, les tests, les rôles, les groupes, les personas locaux et l'URL d'un remote git (facultative). Les questions qui ne s'appliquent pas aux options choisies sont sautées.

!!! warning "Ce qui demande toujours votre accord"
    Le skill ne fait jamais sans demander : installer un outil ou lancer `aspire agent init`, écraser un dossier existant, pousser sur un remote, supprimer des volumes ou arrêter les conteneurs d'un autre projet, écrire un secret dans le dépôt.

Les critères de fin et les interdits du skill, tels qu'il les contient :

```markdown title=".claude/skills/init-project/SKILL.md"
--8<-- ".claude/skills/init-project/SKILL.md" start="## Terminé quand"
```

### Ce qui est créé

Dans le dossier cible (par exemple `../Contoso.Orders`) :

- la solution complète, avec tous les projets renommés pour votre nom ;
- le harnais : `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`, les skills, l'agent `reviewer` et, avec Aspire, `run-local`, `.mcp.json`, `.vscode/mcp.json` et `scripts/smoke-test.*` ;
- `project-profile.json`, écrit par `init-project` ;
- un dépôt git initialisé avec un premier commit `chore: initialize <Nom> from the Web CQRS template`.

### Le fichier `project-profile.json`

C'est la mémoire **métier** du projet : le sens de chaque rôle, groupe et persona (« Admin : administre le catalogue »). Le code et le realm Keycloak restent la source de vérité technique ; les skills `manage-access` et `add-feature` relisent ce profil avant de nommer un rôle ou une politique, et le tiennent à jour. Son format (version de schéma 1, options, rôles, groupes, personas) est décrit dans le skill `manage-access`, fichier `references/project-profile.md`.

### Sans IA : les scripts `new-project`

Le skill s'appuie sur un script que vous pouvez lancer seul, depuis la racine du template. Il valide les paramètres, installe le template (`dotnet new install <racine> --force`, ce qui remplace le template déjà installé sur votre poste), génère le projet, puis initialise git.

=== "Bash"
    ```bash
    bash eng/init/new-project.sh --name Contoso.Orders --authorization AppRolesAndGroups
    ```

=== "PowerShell"
    ```powershell
    ./eng/init/new-project.ps1 -Name Contoso.Orders -Authorization AppRolesAndGroups
    ```

Options du script Bash : `--name` (obligatoire), `--output` (par défaut `../<Nom>`), `--authorization` (par défaut `AppRoles`), `--aspire`, `--rate-limiting` et `--tests` (`true` ou `false`, par défaut `true`) et `--remote <url git>`. Voici sa partie génération :

```bash title="eng/init/new-project.sh"
--8<-- "eng/init/new-project.sh" start='root="$(cd' end='echo "new-project: created'
```

Si le dossier cible existe et n'est pas vide, le script s'arrête avec un code non nul sans rien modifier. Il n'effectue aucune action réseau en dehors de celles de `dotnet new`. Sans l'agent, il vous reste à faire à la main ce que les étapes 4 à 8 du skill automatisent : rôles et personas ([Rôles, groupes et personas](../guides/ajouter-role-groupe-persona.md)), secrets de l'AppHost ([Paramètres et secrets](../aspire/parametres-et-secrets.md)), build, tests et premier commit.

### La télémétrie d'Aspire

À l'étape 6, le skill lance :

```bash
aspire agent init --non-interactive --workspace-root <sortie> --skill-locations claudecode --skills aspire,aspire-orchestration,aspire-monitoring --mcp
```

Cette commande écrit `.mcp.json` et `.vscode/mcp.json`, installe les skills officiels Aspire et **installe aussi des hooks de télémétrie** côté utilisateur. Pour refuser cette télémétrie, définissez la variable d'environnement `ASPIRE_CLI_TELEMETRY_OPTOUT=true`. Le skill demande votre accord avant de lancer la commande et vous rappelle cette option.

## À essayer

1. Sans IA : générez un projet avec `bash eng/init/new-project.sh --name Contoso.Demo --aspire false`, puis ouvrez le dossier créé. Quels fichiers du harnais sont absents sans Aspire ?
2. Relancez la même commande : que répond le script, et le dossier a-t-il changé ?
3. Avec un agent : demandez-lui d'initialiser un projet et vérifiez qu'il affiche un récapitulatif **avant** d'écrire quoi que ce soit.
4. Après l'initialisation, lisez `project-profile.json` : retrouvez-vous vos rôles et personas ?

## Pour aller plus loin

- [Créer un projet](../demarrer/creer-un-projet.md) : les options du template et la génération à la main.
- [Options du template](../reference/options-du-template.md)
- [Scripts et CI](../reference/scripts-et-ci.md) : `new-project` et `smoke-test`.
