# Agent de revue

Cette page présente `reviewer`, l'agent qui relit un changement de code contre les conventions du projet : son rôle, la manière de l'invoquer avec Claude Code ou Copilot, sa grille de relecture et le format de son résultat.

## Le concept simplement

Un **agent** (ou sous-agent) est un assistant spécialisé que l'agent principal peut appeler. Il a ses propres consignes et une liste d'outils limitée. `reviewer` ne fait qu'une chose : relire. Il lit le diff et les fichiers voisins, compare avec `AGENTS.md`, et rend des constats classés. **Il ne modifie rien.**

Séparer l'auteur et le relecteur est une bonne pratique humaine : celui qui a écrit le code voit mal ses propres oublis. C'est vrai aussi pour un agent, qui relit avec un contexte neuf et une grille explicite.

## Pourquoi c'est important

- Il détecte ce qu'un build vert ne voit pas : un endpoint sans autorisation explicite, une query sans `NotEmpty()` sur l'identifiant, une migration oubliée, un test supprimé.
- Il applique **les conventions de votre projet**, pas des règles génériques : il lit `AGENTS.md`, y compris le modèle d'autorisation et les interdits.
- Il est sans danger : lecture seule, aucune commande qui écrit.

## Comment le template le fait

### Deux fichiers pour deux outils

Les deux fichiers ont le même contenu ; seul l'en-tête change, car les deux outils n'écrivent pas les outils autorisés de la même façon. Le template fournit les deux formats (VS Code peut aussi lire `.claude/agents/`).

**Claude Code**

**Fichier :** `.claude/agents/reviewer.md`

```yaml
name: reviewer
description: "Relit un changement de code de l'API contre les conventions du projet (AGENTS.md) et rend des constats classés. À utiliser après chaque feature ou correctif, avant de présenter le travail comme terminé."
tools: Read, Grep, Glob, Bash
model: sonnet
```

**Copilot**

**Fichier :** `.github/agents/reviewer.agent.md`

```yaml
name: reviewer
description: "Relit un changement de code de l'API contre les conventions du projet (AGENTS.md) et rend des constats classés. À utiliser après chaque feature ou correctif, avant de présenter le travail comme terminé."
tools: ['read', 'search']
```

Côté Claude Code, l'agent a les outils `Read`, `Grep`, `Glob` et `Bash` (ce dernier limité par ses consignes à `git diff`, `git log`, `git show` et `git status`). Côté Copilot, il a les outils de lecture et de recherche.

### L'invoquer

Dans les deux outils, la `description` de l'agent dit « à utiliser après chaque feature ou correctif » : le skill `add-feature` le sollicite lui-même (étape de relecture), et `CLAUDE.md` demande à Claude Code de lui faire relire tout changement avant de le présenter comme terminé. Vous pouvez aussi l'appeler à la demande.

**Claude Code**

Mentionnez l'agent : tapez `@` et choisissez `reviewer` dans la liste de suggestions, ou écrivez directement `@agent-reviewer` ; la mention garantit que cet agent s'exécute.

```text
@agent-reviewer relis mes changements
```

Nommer l'agent en langage courant (« Fais relire mon travail par l'agent reviewer ») reste une demande que Claude peut suivre, sans garantie. Selon la version, `/agents` ouvre un assistant de gestion ou rappelle de demander à Claude ou de modifier `.claude/agents/`.

**Copilot**

Dans VS Code, ouvrez la conversation Copilot, choisissez l'agent `reviewer` dans la liste déroulante Agent du chat, puis écrivez :

```text
Relis les changements de ma branche.
```

Par défaut, l'agent relit `git diff main...HEAD` **et** les modifications non commitées. Vous pouvez désigner d'autres fichiers, un commit ou une plage de commits. Si l'outil terminal n'est pas disponible, il vous demande de lui fournir le diff.

### La grille de relecture

L'agent applique quinze points, en ignorant ceux dont la fonctionnalité n'existe pas dans votre projet (pas de dossier `tests/`, pas de rate limiting, pas de realm local...) :

1. couches et dépendances ;
2. CQRS (un record plus un handler, `*Result` distinct de `*Response`) ;
3. endpoints (`*Controller`, `TypedResults`, aucune logique métier) ;
4. autorisation explicite sur chaque route ;
5. rate limiting ;
6. validation (un validateur par commande ou query, `NotEmpty()` sur les identifiants) ;
7. Problem Details et codes d'erreur ;
8. options validées au démarrage ;
9. identité courante via `ICurrentUser` ;
10. persistance (configuration EF, index unique, migration relue) ;
11. tests significatifs ;
12. realm Keycloak cohérent ;
13. secrets ;
14. absence de code propre à un fournisseur d'identité local ;
15. interdits d'`AGENTS.md`.

### Le format du résultat

Les constats sont regroupés sous trois titres, dans cet ordre, un titre vide étant omis.

**Fichier :** `.claude/agents/reviewer.md`

```markdown
## Format de sortie

Regroupez les constats sous trois titres, dans cet ordre, en omettant un titre vide :

- **Bloquant** : bug, faille de sécurité, autorisation absente, secret, migration manquante, test supprimé ou vide, violation de dépendance. À corriger avant de présenter le travail comme terminé.
- **Important** : écart net aux conventions, cas de test manquant, risque de régression.
- **Mineur** : nommage, lisibilité, détail de style.

Chaque constat tient en quatre éléments : `chemin/fichier.cs:ligne`, le problème, pourquoi c'est un problème (convention d'`AGENTS.md` ou risque concret), la correction proposée. Terminez par une phrase de synthèse (périmètre relu : fichiers et plage) et un verdict sur une ligne : `Verdict : Prêt` (aucun constat bloquant) ou `Verdict : À corriger` (au moins un constat bloquant).
```

Un constat comporte donc toujours : l'emplacement (`chemin/fichier.cs:ligne`), le problème, la raison, la correction proposée. Seul un constat **Bloquant** donne le verdict `À corriger`.

> 💡 **Traiter le verdict**
>
> Corrigez les constats bloquants, puis relancez la relecture. Les constats importants se discutent ; les mineurs sont facultatifs. L'agent déclare ses incertitudes au lieu de les présenter comme des faits : vérifiez-les avant de modifier le code.

## À essayer

1. Dans un projet généré, ajoutez volontairement un endpoint sans `RequireAuthorization()` et sans test, puis demandez une relecture : le constat est-il classé Bloquant ?
2. Corrigez-le et relancez : le verdict passe-t-il à `Prêt` ?
3. Comparez les en-têtes des deux fichiers `reviewer` : quelle ligne n'existe que côté Claude Code ?
4. Demandez à l'agent de relire uniquement le dernier commit (`git show HEAD`) : le périmètre annoncé dans sa phrase de synthèse correspond-il ?

## Pour aller plus loin

- [Skills de développement](/Harnais-agentique/Skills-de-développement.md) : les skills qui font appel à `reviewer`.
- [Personnaliser le harnais](/Harnais-agentique/Personnaliser-le-harnais.md) : ajouter votre propre agent.
- [Erreurs et Problem Details](/Architecture/Erreurs-et-Problem-Details.md) : les codes que la grille vérifie.
