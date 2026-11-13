# Questionnaire d'initialisation

Les questions 4 (Aspire) et 5 (moteur de conteneurs) sont posées **à l'étape 1 du skill, avant la vérification des prérequis** (voir [container-runtime.md](container-runtime.md)) : ne les reposez pas, reprenez les réponses au récapitulatif. Posez les autres questions dans cet ordre, une à la fois (ou par petits groupes). Expliquez chaque point comme à un débutant, proposez la valeur par défaut, validez la réponse avant de passer à la suivante.

| # | Question | Explication à donner | Défaut | Validation |
|---|----------|----------------------|--------|------------|
| 1 | Nom du projet | Préfixe de tous les projets, namespaces et de la solution (`<Nom>.slnx`, `src/<Nom>.Api`). | aucun | PascalCase, segments séparés par des points, ex. `Contoso.Orders` : `^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)*$` |
| 2 | Dossier cible | Où créer le projet. Un dossier voisin du template évite de mélanger les dépôts. | `../<Nom>` | Doit ne pas exister ou être vide ; sinon redemandez (le script refuse). |
| 3 | Modèle d'autorisation | Voir les 4 choix ci-dessous. | `AppRoles` | `Authenticated`, `AppRoles`, `Groups` ou `AppRolesAndGroups` |
| 4 | Aspire | Environnement local complet (SQL Server, Azurite, Keycloak, tableau de bord) lancé en une commande. Nécessite Docker ou Podman. Sans Aspire : pas de Keycloak, de personas, de smoke test ni de skill `run-local`. | oui | oui / non |
| 5 | Moteur de conteneurs | Programme qui fait tourner SQL Server, Azurite et Keycloak en local. Docker est le choix par défaut ; Podman est une alternative sans daemon (paramètre `ASPIRE_CONTAINER_RUNTIME=podman` posé dans le profil de lancement de l'AppHost, commandes des skills adaptées). Seulement avec Aspire. Explications détaillées, installation et vérification : [container-runtime.md](container-runtime.md). | Docker | `Docker` ou `Podman` |
| 6 | Rate limiting | Limite le nombre de requêtes par client (protection contre les abus). | oui | oui / non |
| 7 | Tests | Projets de tests unitaires et d'intégration. Fortement conseillé. | oui | oui / non |
| 8 | Rôles métier | Droits fonctionnels de l'application (app roles Entra), p. ex. `Admin`, `Editor`. Nom + une phrase de description. Seulement pour `AppRoles` et `AppRolesAndGroups`. | `Admin` | PascalCase, sans espace ni accent ; description non vide ; pas de doublon. Le projet généré contient déjà `Admin` et `User` : demandez si `User` doit rester (les tests existants le citent) |
| 9 | Groupes | Groupes de sécurité Entra donnant des droits. Nom logique + object ID Entra si déjà connu (sinon `null`, à renseigner plus tard). Seulement pour `Groups` et `AppRolesAndGroups`. | un groupe `Administrators` | PascalCase ; object ID = GUID valide ou vide ; ne jamais en inventer un |
| 10 | Personas locaux | Comptes de test Keycloak pour essayer l'API sans Entra. Identifiant, prénom, nom, rôles, groupes, usage. Seulement avec Aspire. | `admin@local.test` et `user@local.test`, selon le modèle : `Authenticated` sans rôle ni groupe ; `AppRoles` avec rôles seulement ; `Groups` avec groupes seulement ; `AppRolesAndGroups` avec les deux. `admin@local.test` reçoit les droits d'administration, `user@local.test` aucun droit particulier | identifiant `xxx@local.test` en minuscules ; rôles et groupes existants uniquement ; mot de passe local `Passw0rd!` (jamais demandé) |
| 11 | URL du remote git | Dépôt distant où pousser plus tard (facultatif). Rien n'est poussé sans accord. | aucun | URL git ou vide |

## Les 4 modèles d'autorisation

- **`Authenticated`** : tout utilisateur connecté a les mêmes droits. Pas de rôles ni de groupes. Le plus simple.
- **`AppRoles`** : les droits viennent de rôles d'application définis dans Entra (claim `roles`).
- **`Groups`** : les droits viennent de l'appartenance à des groupes de sécurité Entra (claim `groups`, GUID).
- **`AppRolesAndGroups`** : combine les deux (rôle **ou** groupe selon la policy).

Questions 5, 8, 9 et 10 : sautez celles qui ne s'appliquent pas au modèle ou aux options choisis (la question 5 sans Aspire), et dites pourquoi.

## Récapitulatif (à afficher avant toute écriture)

```text
Projet            : Contoso.Orders
Dossier           : C:\...\Contoso.Orders   (n'existe pas / vide)
Autorisation      : AppRolesAndGroups
Options           : Aspire=oui  Conteneurs=Docker  Rate limiting=oui  Tests=oui
Rôles             : Admin (Administre le catalogue) ; Editor (Modifie le catalogue)
Groupes           : Administrators (Entra: inconnu)
Personas locaux   : admin@local.test [Admin; Administrators] ; user@local.test []
Remote git        : (aucun)
```

Demandez : « Je génère ce projet ? ». Sans « oui » explicite, n'écrivez rien. Si l'utilisateur corrige un point, réaffichez le récapitulatif.
