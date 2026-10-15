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

Les deux fichiers ont le même contenu ; seul l'en-tête change, car les deux outils n'écrivent pas les outils autorisés de la même façon.

=== "Claude Code"
    ```yaml title=".claude/agents/reviewer.md"
    --8<-- ".claude/agents/reviewer.md" start="name: reviewer" end="model: sonnet"
    ```

=== "Copilot"
    ```yaml title=".github/agents/reviewer.agent.md"
    --8<-- ".github/agents/reviewer.agent.md" start="name: reviewer" end="tools:"
    ```

Côté Claude Code, l'agent a les outils `Read`, `Grep`, `Glob` et `Bash` (ce dernier limité par ses consignes à `git diff`, `git log`, `git show` et `git status`). Côté Copilot, il a les outils de lecture et de recherche.

### L'invoquer

Dans les deux outils, la `description` de l'agent dit « à utiliser après chaque feature ou correctif » : le skill `add-feature` le sollicite lui-même (étape de relecture), et `CLAUDE.md` demande à Claude Code de lui faire relire tout changement avant de le présenter comme terminé. Vous pouvez aussi l'appeler à la demande.

=== "Claude Code"
    Demandez en toutes lettres, ou mentionnez l'agent :

    ```text
    Fais relire mon travail par l'agent reviewer.
    ```

    La commande `/agents` liste les agents disponibles.

=== "Copilot"
    Dans VS Code, ouvrez la conversation Copilot, choisissez l'agent `reviewer` dans le sélecteur d'agents, puis écrivez :

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

```markdown title=".claude/agents/reviewer.md"
--8<-- ".claude/agents/reviewer.md" start="## Format de sortie"
```

Un constat comporte donc toujours : l'emplacement (`chemin/fichier.cs:ligne`), le problème, la raison, la correction proposée. Seul un constat **Bloquant** donne le verdict `À corriger`.

!!! tip "Traiter le verdict"
    Corrigez les constats bloquants, puis relancez la relecture. Les constats importants se discutent ; les mineurs sont facultatifs. L'agent déclare ses incertitudes au lieu de les présenter comme des faits : vérifiez-les avant de modifier le code.

## À essayer

1. Dans un projet généré, ajoutez volontairement un endpoint sans `RequireAuthorization()` et sans test, puis demandez une relecture : le constat est-il classé Bloquant ?
2. Corrigez-le et relancez : le verdict passe-t-il à `Prêt` ?
3. Comparez les en-têtes des deux fichiers `reviewer` : quelle ligne n'existe que côté Claude Code ?
4. Demandez à l'agent de relire uniquement le dernier commit (`git show HEAD`) : le périmètre annoncé dans sa phrase de synthèse correspond-il ?

## Pour aller plus loin

- [Skills de développement](skills-de-developpement.md) : les skills qui font appel à `reviewer`.
- [Personnaliser le harnais](personnaliser-le-harnais.md) : ajouter votre propre agent.
- [Erreurs et Problem Details](../architecture/erreurs-et-problem-details.md) : les codes que la grille vérifie.
