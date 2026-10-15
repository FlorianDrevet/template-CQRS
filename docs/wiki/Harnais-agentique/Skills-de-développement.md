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

Les trois skills sont lus par Claude Code et par Copilot (dossier `.claude/skills/`). Le skill `init-project`, lui, reste dans le dépôt du template : voir [Initialiser un projet](/Harnais-agentique/Initialiser-un-projet.md).

### `add-feature`

**Quand** : toute nouvelle route, ressource, endpoint de liste ou de lecture, règle d'unicité ou cas d'usage métier.

**Fichier :** `.claude/skills/add-feature/SKILL.md`

```yaml
name: add-feature
description: "Ajouter une fonctionnalité métier complète à l'API (entité, persistance EF, commande/query CQRS, validation, endpoint, autorisation, rate limiting, tests) en suivant les conventions du projet. À utiliser pour toute nouvelle route, ressource, endpoint de liste ou de lecture, règle d'unicité ou cas d'usage métier (« ajouter une ressource », « créer un endpoint », « nouvelle feature »)."
```

**Exemples de demandes**

```text
Ajoute une ressource Book avec un titre, un ISBN unique et un auteur ; seuls les admins peuvent créer.
Crée un endpoint de liste paginée des commandes.
```

**Ce que fait l'agent** : il lit `AGENTS.md` et `project-profile.json`, vous pose les questions manquantes (champs, règles d'unicité, qui a le droit de quoi) et annonce un plan court avec la liste des fichiers. Puis il construit la tranche verticale dans l'ordre : domaine, infrastructure (configuration EF, repository, injection), migration, application (record, validateur, handler), contrats, endpoint, tests unitaires et d'intégration. Il lance `dotnet build` et `dotnet test`, propose une vérification en direct avec `run-local` si le projet a Aspire, et termine par une relecture de l'agent `reviewer`. Le tutoriel [Ajouter une feature](/Guides/Ajouter-une-feature.md) détaille les mêmes étapes à la main.

**Critères de fin et interdits**

**Fichier :** `.claude/skills/add-feature/SKILL.md`

```markdown
## Terminé quand

- `dotnet build` sans erreur et `dotnet test` entièrement vert.
- Les nouveaux tests couvrent les cas 2xx/400/401/403/404/409 pertinents pour les routes ajoutées.
- Chaque route a une autorisation explicite et, si disponible, une politique de rate limiting.
- Une migration existe et a été relue.
- La revue de l'agent `reviewer` n'a plus de constat bloquant.

## Ne jamais faire sans demander

- Changer le modèle d'autorisation, ou modifier une policy existante (voir `manage-access`).
- Supprimer ou renommer des routes existantes, ou changer leur contrat.
- Modifier le realm Keycloak (voir `manage-access`).
- Appliquer une migration sur une base partagée.
- Désactiver une validation, une autorisation ou un test.
```

### `manage-access`

**Quand** : dès qu'une demande parle de rôle, groupe, droit, permission, persona ou compte de test, y compris juste après la génération d'un projet.

**Fichier :** `.claude/skills/manage-access/SKILL.md`

```yaml
name: manage-access
description: "Gérer les accès de l'API : ajouter, renommer ou retirer un rôle (app role Entra), un groupe de sécurité ou un persona Keycloak local ; garder cohérents le code, le realm Keycloak, la configuration, les tests, la doc Entra et project-profile.json. À utiliser dès qu'une demande parle de rôle, groupe, droit, permission, persona ou compte de test, y compris juste après la génération d'un projet (rien ne tourne encore)."
```

**Exemples de demandes**

```text
Ajoute un rôle Editor et un persona editor@local.test qui l'a.
Renomme le rôle Admin en Administrator.
```

**Ce que fait l'agent** : un rôle, un groupe ou un persona vit à trois endroits qui doivent rester identiques, le code de l'API, le realm Keycloak local et Entra ID. L'agent établit l'état actuel, confirme l'opération et liste les fichiers touchés avant d'écrire, puis modifie le code, le realm, les tests et `project-profile.json`. Il vous **communique** les étapes à faire dans le portail Entra (créer l'app role, assigner les utilisateurs) : ce sont des actions humaines, il ne les exécute pas. Si Keycloak tourne déjà, le realm n'est réimporté qu'avec `aspire stop --apphost <csproj> --force --volumes`, une opération destructive (elle efface aussi la base SQL locale) qui demande votre accord explicite. Voir aussi [Rôles, groupes et personas](/Guides/Rôles,-groupes-et-personas.md).

**Critères de fin et interdits**

**Fichier :** `.claude/skills/manage-access/SKILL.md`

```markdown
## Terminé quand

- `dotnet build` sans erreur et `dotnet test` entièrement vert.
- Constantes, validation, policies, `appsettings*.json`, `ApiFactory` et realm sont cohérents entre eux (mêmes noms, même GUID local) ; le JSON du realm est valide ; aucun persona ne cite un rôle ou un groupe absent du realm.
- Des tests couvrent le nouveau droit (accepté, refusé en 403).
- `project-profile.json` est à jour.
- Les étapes Entra ont été communiquées à l'utilisateur.
- Si l'environnement tourne : le token du persona contient les `roles` / `groups` attendus. Sinon, dites que ce contrôle reste à faire au prochain `aspire start`.

## Ne jamais faire sans demander

- Supprimer des volumes (`aspire stop ... --volumes` efface aussi la base SQL locale) : accord explicite à chaque fois.
- Retirer ou renommer un rôle ou un groupe utilisé par une policy ou un endpoint.
- Changer le modèle d'autorisation (régénération du projet).
- Retirer `admin@local.test` / `user@local.test` ou changer leur mot de passe local.
- Mettre un secret ou un vrai mot de passe dans le realm (le mot de passe local `Passw0rd!` est public), ou référencer un compte Entra réel.
- Désactiver une validation, une policy ou un test pour faire passer le build.
```

### `run-local`

**Quand** : tester une feature en local, reproduire un bug, diagnostiquer une erreur 401, 403 ou 500, lancer le smoke test. Ce skill n'existe que dans un projet généré avec Aspire.

**Fichier :** `.claude/skills/run-local/SKILL.md`

```yaml
name: run-local
description: "Lancer, vérifier et dépanner l'environnement local Aspire (SQL Server, Azurite, Keycloak, API) : démarrer/arrêter, obtenir un token pour un persona, appeler l'API, lancer le smoke test, lire logs et traces via le MCP Aspire ou la CLI. À utiliser pour tester une feature en local, reproduire un bug ou diagnostiquer une erreur 401/403/500."
```

**Exemples de demandes**

```text
Démarre l'environnement local et vérifie que l'API répond pour le persona user.
J'ai une 500 sur POST /books : trouve la cause.
```

**Ce que fait l'agent** : il vérifie les prérequis (Docker, CLI Aspire, certificat, secrets de l'AppHost sans jamais afficher leurs valeurs), contrôle que le port 8080 est libre, démarre avec `aspire start`, attend que l'API soit saine, obtient un token avec `scripts/get-token.*` et appelle l'API sur `https://localhost:7246`. Pour une erreur, il relève le `traceId` du Problem Details et lit la trace correspondante, par le [serveur MCP Aspire](/Harnais-agentique/MCP-Aspire.md) s'il est disponible, sinon avec `aspire otel`. Il termine en annonçant l'état final de l'environnement.

**Critères de fin et interdits**

**Fichier :** `.claude/skills/run-local/SKILL.md`

```markdown
## Terminé quand

- `aspire describe` montre toutes les ressources saines et `scripts/smoke-test.*` finit sans `FAIL` (ou la cause de l'échec est identifiée et expliquée).
- Le comportement testé (code HTTP, corps, persona) est constaté, pas supposé.
- Pour un bug : la trace ou le log qui prouve la cause est cité (`traceId`, ressource, ligne).
- L'état final de l'environnement (laissé en marche ou arrêté) est annoncé à l'utilisateur.

## Ne jamais faire sans demander

- `aspire stop ... --force --volumes` ou toute suppression de volume ou de conteneur : efface la base et le realm.
- Arrêter ou supprimer le conteneur Keycloak (ou toute ressource) d'un autre projet.
- Modifier le realm Keycloak : il faut ensuite le réimporter (voir `manage-access`).
- Écrire des user-secrets manquants (`dotnet user-secrets set`) : accord préalable, valeurs jamais affichées.
- Approuver le certificat de développement (`dotnet dev-certs https --trust`).
- Afficher, journaliser ou committer un secret ou un token.
- Envoyer les identifiants des personas ailleurs que vers l'API et Keycloak locaux.
```

### Le smoke test

Le skill `run-local` lance `scripts/smoke-test.sh` (ou `.ps1`) : un contrôle rapide de l'API en cours d'exécution. Il demande un token à deux personas, vérifie que `/me` répond 401 sans token puis 200 pour chacun, et, si la route `/admin/ping` existe dans ce projet, 200 pour l'administrateur, 403 pour l'utilisateur et 401 sans token. Si la route n'est pas générée (modèle `Authenticated`), cette partie est ignorée avec un message `SKIP`. Le script sort avec un code non nul au moindre `FAIL`.

**Fichier :** `scripts/smoke-test.sh`

```bash
ping_anonymous="$(status_of "$api/admin/ping")"
if [ "$ping_anonymous" = "404" ]; then
  echo "SKIP /admin/ping (not generated for this authorization model)"
else
  expect "GET /admin/ping without token" 401 "$ping_anonymous"
  expect "GET /admin/ping as $admin" 200 "$(status_of -H "Authorization: Bearer $admin_token" "$api/admin/ping")"
  expect "GET /admin/ping as $user" 403 "$(status_of -H "Authorization: Bearer $user_token" "$api/admin/ping")"
fi
```

## À essayer

1. Dans un projet généré avec Aspire, demandez à l'agent : « Démarre l'environnement local et lance le smoke test. » Quelles commandes `aspire` a-t-il lancées, dans quel ordre ?
2. Demandez : « Ajoute un persona viewer@local.test sans aucun droit. » Vérifie-t-il que l'opération est cohérente avec le modèle d'autorisation avant d'écrire ?
3. Lisez la section « Ne jamais faire sans demander » de `run-local` : quelle commande efface la base SQL locale, et pourquoi l'agent doit-il vous interroger avant ?
4. Demandez une petite feature avec `add-feature` et vérifiez que l'agent a lancé l'agent `reviewer` avant de conclure.

## Pour aller plus loin

- [Ajouter une feature](/Guides/Ajouter-une-feature.md) : la même procédure, à la main.
- [Rôles, groupes et personas](/Guides/Rôles,-groupes-et-personas.md)
- [Dashboard et CLI](/Aspire/Dashboard-et-CLI.md)
- [Agent de revue](/Harnais-agentique/Agent-de-revue.md)
