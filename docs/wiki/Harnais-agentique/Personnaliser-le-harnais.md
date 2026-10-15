# Personnaliser le harnais

Le harnais livré avec le template couvre les tâches communes. Cette page montre comment l'adapter à votre équipe : ajouter un skill, ajouter un agent, et garder `AGENTS.md` à jour.

## Le concept simplement

Le harnais est du texte versionné dans votre dépôt : vous le modifiez comme du code, par une pull request relue. Un skill ou un agent n'est pas un programme, c'est un fichier Markdown précis que l'agent lit au bon moment. La qualité du fichier fait la qualité du résultat : une consigne floue donne un comportement flou.

## Pourquoi c'est important

- Vos règles propres (nommage, bibliothèque interne, procédure de déploiement) sortent de la tête de l'équipe et deviennent exécutables.
- Un skill mal écrit ne se déclenche pas, ou pire, se déclenche à contresens : quelques règles simples évitent ces défauts.
- Un `AGENTS.md` périmé trompe l'agent avec aplomb : il faut le maintenir comme la documentation.

## Comment le template le fait

### Ajouter un skill

Un skill est un dossier dans `.claude/skills/`, lu par Claude Code et par Copilot :

```text
.claude/skills/
  mon-skill/
    SKILL.md
    references/
      details.md
```

Le nom du dossier est le nom du skill. `SKILL.md` commence par un en-tête YAML. Voici celui de `add-feature` :

**Fichier :** `.claude/skills/add-feature/SKILL.md`

```yaml
name: add-feature
description: "Ajouter une fonctionnalité métier complète à l'API (entité, persistance EF, commande/query CQRS, validation, endpoint, autorisation, rate limiting, tests) en suivant les conventions du projet. À utiliser pour toute nouvelle route, ressource, endpoint de liste ou de lecture, règle d'unicité ou cas d'usage métier (« ajouter une ressource », « créer un endpoint », « nouvelle feature »)."
```

Les règles du template pour un skill :

- **`name`** : identique au nom du dossier.
- **`description`** : la partie la plus importante, car c'est ce que l'agent lit pour décider s'il charge le skill. Écrivez d'abord le cas d'usage puis les mots que l'utilisateur emploiera (« ajouter une ressource », « créer un endpoint »). Moins de 1 000 caractères. Mettez-la entre guillemets doubles : les `:` et les guillemets français de la phrase casseraient sinon le YAML.
- **Étapes numérotées** avec les commandes exactes, pas des intentions vagues.
- **Section « Terminé quand »** : des critères vérifiables (commande qui passe, fichier présent), jamais « le code est propre ».
- **Section « Ne jamais faire sans demander »** : les actions irréversibles ou à effet externe (supprimer, pousser, installer, écrire un secret).
- **Moins de 300 lignes** : le détail volumineux (exemples de code complets, schémas) va dans `references/*.md`, lié en relatif. L'agent ne les ouvre que s'il en a besoin.
- Les chemins de code s'écrivent avec le nom de projet du template (`src/Web.Template.CQRS.Api/...`) : le moteur de template le remplace à la génération.
- Rédigez en français à l'impératif ; noms de fichiers, commandes et code restent en anglais.

Squelette minimal :

````text
---
name: mon-skill
description: "Ce que fait le skill. À utiliser quand l'utilisateur demande « ... » ou « ... »."
---

# Titre

## Étapes

1. **Première étape.** Ce qu'il faut faire, avec la commande exacte :
   ```bash
   dotnet build
   ```
2. **Deuxième étape.** ...

## Terminé quand

- Le build est vert.

## Ne jamais faire sans demander

- Supprimer des données.
````

> ℹ️ **Dans le dépôt du template ou dans un projet généré ?**
>
> Un skill ajouté dans un projet généré est à vous. Un skill ajouté dans le dépôt du template, sous `.claude/skills/`, est livré à **tous** les projets générés : traitez-le comme du code du template et vérifiez-le avec `eng/verify-template.sh`. Un skill réservé au dépôt du template doit être exclu de la génération dans `.template.config/template.json`, comme `init-project`.

Pour savoir si votre skill marche, testez-le avec un agent neuf : donnez-lui une demande réelle et ne l'aidez pas. S'il se trompe d'étape ou ne charge pas le skill, corrigez la `description` ou l'étape ambiguë, jamais l'agent.

### Ajouter un agent

Un agent est un fichier unique, le template fournit les deux formats (VS Code peut aussi lire `.claude/agents/`) (voir [Agent de revue](/Harnais-agentique/Agent-de-revue.md)) :

- `.claude/agents/<nom>.md`, avec `name`, `description`, `tools` et `model` ;
- `.github/agents/<nom>.agent.md`, avec `name`, `description` et `tools`.

Le corps décrit le rôle, les règles et le format de sortie. Principes à suivre :

- **Donnez le minimum d'outils.** Un relecteur n'a pas besoin d'écrire : `reviewer` n'a que des outils de lecture.
- **Une seule mission** par agent, décrite par une `description` qui dit quand l'utiliser.
- **Un format de sortie imposé**, pour que le résultat soit exploitable (titres, ordre, verdict).
- **Gardez les deux fichiers équivalents** : quand vous corrigez l'un, corrigez l'autre.

### Garder `AGENTS.md` à jour

`AGENTS.md` est lu à chaque session : il doit rester court et vrai. Mettez-le à jour dans la même pull request que le changement qu'il décrit :

- vous ajoutez un projet dans `src/` : mettez à jour la carte du code ;
- vous changez une convention (retour des endpoints, politique d'autorisation) : mettez à jour la section « Conventions » ;
- vous ajoutez un skill ou un agent : ajoutez-le à la section « Skills et agent », et dans `.github/copilot-instructions.md` si Copilot doit le connaître.

Dans le dépôt du template, le fichier à éditer est `AGENTS.generated.md` (il devient `AGENTS.md` à la génération). Ses lignes `<!--#if (Symbole) -->`, `<!--#else -->` et `<!--#endif -->` doivent chacune rester **seule sur sa ligne** et couvrir ce qui dépend d'une option. Les interdits actuels en donnent un exemple :

**Fichier :** `AGENTS.generated.md`

```markdown
## Interdits

- Ne jamais écrire de secret (mot de passe, token, chaîne de connexion) dans le dépôt.
- Ne pas écrire de code propre à un fournisseur d'identité local : Entra ID est la cible.
- Ne pas désactiver la validation des options, l'autorisation ou la validation des commandes.
<!--#if (EnableAspire) -->
- Ne pas modifier le realm Keycloak sans prévenir qu'il faut le réimporter (`aspire stop --apphost <csproj> --force --volumes`, destructif).
- Ne pas supprimer de volumes sans accord explicite.
<!--#endif -->
- Ne pas pousser sur un remote sans accord explicite.
- Ne pas contourner ni supprimer un test pour faire passer le build.
```

Après une modification, générez un projet avec `eng/verify-template.sh` dans les variantes concernées (avec et sans Aspire) : le script contrôle la présence des fichiers du harnais, la validité JSON des fichiers MCP et l'en-tête de chaque skill.

## À essayer

1. Dans un projet généré, créez le skill `format-code` avec un en-tête valide, deux étapes et les deux sections de fin. Demandez à l'agent une tâche qui doit le déclencher : est-il chargé ?
2. Réécrivez volontairement sa `description` de façon vague (« outil utile ») et refaites la même demande : que constatez-vous ?
3. Ajoutez une ligne dans la section « Interdits » de `AGENTS.md` et demandez à l'agent d'effectuer l'action interdite : la refuse-t-il ?
4. Dans le dépôt du template, ajoutez un mot à une ligne de `AGENTS.generated.md`, générez un projet avec `bash eng/init/new-project.sh --name Contoso.Demo` et vérifiez que la modification est dans `AGENTS.md`.

## Pour aller plus loin

- [Skills de développement](/Harnais-agentique/Skills-de-développement.md) : trois exemples de skills complets.
- [Agent de revue](/Harnais-agentique/Agent-de-revue.md)
- [Scripts et CI](/Référence/Scripts-et-CI.md) : `verify-template.sh`.
- [Claude Code, les skills](https://code.claude.com/docs/en/skills)
