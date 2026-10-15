# Skills de développement

Un projet généré contient trois skills pour le travail de tous les jours : `add-feature`, `manage-access` et `run-local`. Cette page dit quand les utiliser, comment les demander, ce que fait l'agent et à quoi vous reconnaissez que la tâche est vraiment terminée.

## Le concept simplement

Un skill est une fiche de procédure que l'agent charge **tout seul** quand votre demande correspond à sa `description`. Vous n'avez pas à écrire `/add-feature` : il suffit de dire « ajoute une ressource Produit ». La description commence donc par les cas d'usage et les mots déclencheurs.

Chaque skill suit la même trame : des étapes numérotées avec les commandes exactes, une section « Terminé quand » (les preuves attendues) et une section « Ne jamais faire sans demander » (les garde-fous). Le détail volumineux, comme les exemples de code complets, est rangé dans des fichiers `references/` que l'agent ne lit que quand il en a besoin.

## Pourquoi c'est important

- Sans skill, deux demandes identiques donnent deux résultats différents ; avec un skill, l'ordre des étapes et les vérifications sont les mêmes à chaque fois.
- Les critères de fin évitent le « c'est fait » sans preuve : le skill exige un build vert, des tests verts, parfois une vérification en direct.
- Les garde-fous protègent les actions irréversibles : suppression de volumes, modification du modèle d'autorisation, migration sur une base partagée.

## Comment le template le fait

| Skill | Présent si | Sert à |
|-------|------------|--------|
| `add-feature` | toujours | ajouter une fonctionnalité métier de bout en bout |
| `manage-access` | toujours | ajouter, renommer ou retirer un rôle, un groupe ou un persona |
| `run-local` | Aspire activé | lancer, tester et diagnostiquer l'environnement local |

Les trois skills sont lus par Claude Code et par Copilot (dossier `.claude/skills/`). Le skill `init-project`, lui, reste dans le dépôt du template : voir [Initialiser un projet](initialiser-un-projet.md).

### `add-feature`

**Quand** : toute nouvelle route, ressource, endpoint de liste ou de lecture, règle d'unicité ou cas d'usage métier.

```yaml title=".claude/skills/add-feature/SKILL.md"
--8<-- ".claude/skills/add-feature/SKILL.md" start="name: add-feature" end="description:"
```

**Exemples de demandes**

```text
Ajoute une ressource Book avec un titre, un ISBN unique et un auteur ; seuls les admins peuvent créer.
Crée un endpoint de liste paginée des commandes.
```

**Ce que fait l'agent** : il lit `AGENTS.md` et `project-profile.json`, vous pose les questions manquantes (champs, règles d'unicité, qui a le droit de quoi) et annonce un plan court avec la liste des fichiers. Puis il construit la tranche verticale dans l'ordre : domaine, infrastructure (configuration EF, repository, injection), migration, application (record, validateur, handler), contrats, endpoint, tests unitaires et d'intégration. Il lance `dotnet build` et `dotnet test`, propose une vérification en direct avec `run-local` si le projet a Aspire, et termine par une relecture de l'agent `reviewer`. Le tutoriel [Ajouter une feature](../guides/ajouter-une-feature.md) détaille les mêmes étapes à la main.

**Critères de fin et interdits**

```markdown title=".claude/skills/add-feature/SKILL.md"
--8<-- ".claude/skills/add-feature/SKILL.md" start="## Terminé quand"
```

### `manage-access`

**Quand** : dès qu'une demande parle de rôle, groupe, droit, permission, persona ou compte de test, y compris juste après la génération d'un projet.

```yaml title=".claude/skills/manage-access/SKILL.md"
--8<-- ".claude/skills/manage-access/SKILL.md" start="name: manage-access" end="description:"
```

**Exemples de demandes**

```text
Ajoute un rôle Editor et un persona editor@local.test qui l'a.
Renomme le rôle Admin en Administrator.
```

**Ce que fait l'agent** : un rôle, un groupe ou un persona vit à trois endroits qui doivent rester identiques, le code de l'API, le realm Keycloak local et Entra ID. L'agent établit l'état actuel, confirme l'opération et liste les fichiers touchés avant d'écrire, puis modifie le code, le realm, les tests et `project-profile.json`. Il vous **communique** les étapes à faire dans le portail Entra (créer l'app role, assigner les utilisateurs) : ce sont des actions humaines, il ne les exécute pas. Si Keycloak tourne déjà, le realm n'est réimporté qu'après la réinitialisation des volumes (`aspire stop --apphost <csproj> --force --volumes`, puis contrôle des volumes restants et suppression de ceux du projet par nom exact, voir [Émulateurs](../aspire/emulateurs.md#reinitialiser-les-donnees)), une opération destructive (elle efface aussi la base SQL locale) qui demande votre accord explicite. Voir aussi [Rôles, groupes et personas](../guides/ajouter-role-groupe-persona.md).

**Critères de fin et interdits**

```markdown title=".claude/skills/manage-access/SKILL.md"
--8<-- ".claude/skills/manage-access/SKILL.md" start="## Terminé quand"
```

### `run-local`

**Quand** : tester une feature en local, reproduire un bug, diagnostiquer une erreur 401, 403 ou 500, lancer le smoke test. Ce skill n'existe que dans un projet généré avec Aspire.

```yaml title=".claude/skills/run-local/SKILL.md"
--8<-- ".claude/skills/run-local/SKILL.md" start="name: run-local" end="description:"
```

**Exemples de demandes**

```text
Démarre l'environnement local et vérifie que l'API répond pour le persona user.
J'ai une 500 sur POST /books : trouve la cause.
```

**Ce que fait l'agent** : il vérifie les prérequis (Docker, CLI Aspire, certificat, secrets de l'AppHost sans jamais afficher leurs valeurs), contrôle que le port 8080 est libre, démarre avec `aspire start`, attend que l'API soit saine, obtient un token avec `scripts/get-token.*` et appelle l'API sur `https://localhost:7246`. Pour une erreur, il relève le `traceId` du Problem Details et lit la trace correspondante, par le [serveur MCP Aspire](mcp-aspire.md) s'il est disponible, sinon avec `aspire otel`. Il termine en annonçant l'état final de l'environnement.

**Critères de fin et interdits**

```markdown title=".claude/skills/run-local/SKILL.md"
--8<-- ".claude/skills/run-local/SKILL.md" start="## Terminé quand"
```

### Le smoke test

Le skill `run-local` lance `scripts/smoke-test.sh` (ou `.ps1`) : un contrôle rapide de l'API en cours d'exécution. Il demande un token à deux personas, vérifie que `/me` répond 401 sans token puis 200 pour chacun, et, si la route `/admin/ping` existe dans ce projet, 200 pour l'administrateur, 403 pour l'utilisateur et 401 sans token. Si la route n'est pas générée (modèle `Authenticated`), cette partie est ignorée avec un message `SKIP`. Le script sort avec un code non nul au moindre `FAIL`.

```bash title="scripts/smoke-test.sh"
--8<-- "scripts/smoke-test.sh" start='ping_anonymous=' before='if [ "$failures"'
```

## À essayer

1. Dans un projet généré avec Aspire, demandez à l'agent : « Démarre l'environnement local et lance le smoke test. » Quelles commandes `aspire` a-t-il lancées, dans quel ordre ?
2. Demandez : « Ajoute un persona viewer@local.test sans aucun droit. » Vérifie-t-il que l'opération est cohérente avec le modèle d'autorisation avant d'écrire ?
3. Lisez la section « Ne jamais faire sans demander » de `run-local` : quelle commande efface la base SQL locale, et pourquoi l'agent doit-il vous interroger avant ?
4. Demandez une petite feature avec `add-feature` et vérifiez que l'agent a lancé l'agent `reviewer` avant de conclure.

## Pour aller plus loin

- [Ajouter une feature](../guides/ajouter-une-feature.md) : la même procédure, à la main.
- [Rôles, groupes et personas](../guides/ajouter-role-groupe-persona.md)
- [Dashboard et CLI](../aspire/dashboard-et-cli.md)
- [Agent de revue](agent-de-revue.md)
