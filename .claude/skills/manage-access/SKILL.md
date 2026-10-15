---
name: manage-access
description: "Gérer les accès de l'API : ajouter, renommer ou retirer un rôle (app role Entra), un groupe de sécurité ou un persona Keycloak local ; garder cohérents le code, le realm Keycloak, la configuration, les tests, la doc Entra et project-profile.json. À utiliser dès qu'une demande parle de rôle, groupe, droit, permission, persona ou compte de test, y compris juste après la génération d'un projet (rien ne tourne encore)."
---

# Gérer les accès

Un rôle, un groupe ou un persona vit à trois endroits qui doivent rester identiques : le **code** de l'API (`src/Web.Template.CQRS.Api/Authorization/`), le **realm Keycloak local** (`src/Web.Template.CQRS.AppHost/Realms/template-realm.json`) et **Entra ID** (déployé, hors dépôt). Oublier l'un des trois est la première cause de « ça marche en local mais pas déployé ». Les chemins s'écrivent avec `Web.Template.CQRS` : c'est le nom de votre projet.

Détails (lisez le fichier concerné avant d'écrire) :

- [references/files-by-model.md](references/files-by-model.md) : fichiers et code par opération et par modèle d'autorisation.
- [references/realm.md](references/realm.md) : JSON du realm, règles et pièges.
- [references/entra.md](references/entra.md) : étapes du portail Entra à communiquer.
- [references/project-profile.md](references/project-profile.md) : schéma de `project-profile.json` et mise à jour.

## Deux situations

- **Projet existant** (Keycloak peut tourner) : le realm n'est importé qu'au premier démarrage d'un volume vide ; une modification du fichier n'a aucun effet tant que le volume existe (étape 9).
- **Projet fraîchement généré** (rien ne tourne, aucun volume) : appliquez les étapes 1 à 8 ; l'étape 9 est inutile, le realm sera importé tel quel au premier `aspire start`. Si `init-project` vous appelle, il fournit déjà la liste des rôles, groupes et personas voulus : traitez-les en une passe, sans reposer les questions.

## Étapes

1. **Établir l'état actuel.** Lisez `project-profile.json` (racine) s'il existe ; sinon reconstruisez-le depuis le code : `AppRoles.cs`, `GroupOptions.cs`, `Policies.cs`, `AuthorizationSetup.cs`, `Realms/template-realm.json` (absent dans un projet sans Aspire). Déduisez le **modèle d'autorisation** des fichiers présents (tableau dans `files-by-model.md`). En cas de divergence profil / code / realm, le code et le realm font foi : signalez l'écart et corrigez le profil.
2. **Confirmer l'opération et ses impacts.** Précisez avec l'utilisateur : opération (ajouter, renommer, retirer ; rôle, groupe, persona), nom exact (PascalCase pour un rôle ou un groupe, `prenom@local.test` pour un persona), sens métier (une phrase), et pour un rôle ou un groupe la policy ou les endpoints concernés. Annoncez la **liste des fichiers** qui seront modifiés (selon `files-by-model.md`) avant d'écrire. Modèle `Authenticated` : voir la section dédiée plus bas.
3. **Appliquer le code** selon `references/files-by-model.md` : constante `AppRoles`, propriété `GroupOptions` et sa règle de validation, constante `Policies` et enregistrement dans `AuthorizationSetup.cs`, valeurs de `appsettings.json` (vide) et `appsettings.Development.json` (GUID local). Une policy ne sert à rien tant qu'elle n'est pas appliquée à un endpoint avec `.RequireAuthorization(Policies.X)` : demandez lequel, ou n'en ajoutez un que si l'utilisateur le veut. Un nouvel endpoint dans un nouveau fichier `Map<X>Endpoints` doit être appelé dans `Program.cs` (à côté de `app.MapAdminEndpoints();`). Un rôle sans policy (lecture seule, par exemple) n'a besoin que de la constante et du realm. Le rôle `User` fourni par le template reste en place sauf demande contraire.
4. **Appliquer le realm** selon `references/realm.md` (si `Realms/template-realm.json` existe) : rôle dans `roles.realm`, groupe avec attribut `objectId` (GUID **neuf**, identique à `appsettings.Development.json`), utilisateur avec `firstName`, `lastName`, `email`, `emailVerified`, `credentials`. Ne référencez que des rôles et groupes existants dans ce realm. Validez le JSON :
   ```bash
   pwsh -NoProfile -Command "Get-Content 'src/Web.Template.CQRS.AppHost/Realms/template-realm.json' -Raw | ConvertFrom-Json | Out-Null; 'JSON OK'"
   ```
   (Linux/macOS sans `pwsh` : `jq . src/Web.Template.CQRS.AppHost/Realms/template-realm.json`.)
5. **Tests** (si `tests/` existe). Pour un nouveau groupe : ajoutez la constante et le `UseSetting` dans `tests/Web.Template.CQRS.Api.IntegrationTests/Infrastructure/ApiFactory.cs`, **sinon tous les tests d'intégration échouent au démarrage** (`ValidateOnStart`). Ajoutez des tests de policy (rôle accepté, groupe accepté, utilisateur standard refusé en 403, anonyme en 401) dans `tests/Web.Template.CQRS.Api.IntegrationTests/<Nom>/`. Pour un rôle renommé ou retiré, mettez à jour les tests existants qui le citent (`grep -rn "<Ancien>" src tests`).
6. **Build et tests.**
   ```bash
   dotnet build Web.Template.CQRS.slnx
   dotnet test --solution Web.Template.CQRS.slnx
   ```
   Corrigez la cause d'un échec ; ne supprimez ni n'assouplissez un test pour le faire passer.
7. **Mettre à jour `project-profile.json`** (`references/project-profile.md`) : rôles, groupes (avec `localObjectId`), personas, `purpose`. Créez-le s'il n'existe pas.
8. **Communiquer les étapes Entra** (`references/entra.md`), adaptées à l'opération : valeur de l'app role à créer dans le manifest et assignations, groupe de sécurité et claim `groups`, object ID à fournir en configuration `Authorization__Groups__<Nom>`. Ce sont des actions humaines dans le portail : ne les exécutez pas et ne les supposez pas faites.
9. **Réimporter le realm si Keycloak tourne déjà** (projet existant avec Aspire et un volume Keycloak). Procédure de référence, aussi décrite dans <https://floriandrevet.github.io/template-CQRS/aspire/emulateurs/>. L'opération est **destructive : elle efface aussi les données SQL locales**. Demandez un accord explicite, puis :
   ```bash
   aspire stop --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj --force --volumes
   docker volume ls --filter name=<nom de l'AppHost en minuscules>
   ```
   Le filtre `name=` est une recherche de sous-chaîne : il peut renvoyer les volumes d'autres projets. **Ne supprimez jamais par filtre.** Constat sur l'Aspire CLI 13.6 (l'aide dit pourtant que `--volumes` les supprime) : `<projet>.apphost-<hash>-keycloak-data` et `…-sqlserver-data` restent, donc le realm n'est pas réimporté. S'ils restent, affichez à l'utilisateur les **noms exacts** de ce projet, obtenez son accord explicite sur cette liste, puis `docker volume rm <nom> <nom>`. Ensuite :
   ```bash
   aspire start
   aspire wait keycloak --timeout 300
   aspire wait api --timeout 300
   ```
   Si le port 8080 reste occupé par un conteneur Keycloak persistant d'un autre projet, expliquez-le à l'utilisateur et proposez une remédiation sans l'appliquer d'office. Sans accord, laissez le fichier modifié et dites que le changement ne sera visible qu'après réinitialisation. Si rien ne tourne ou si le projet vient d'être généré : passez cette étape.
10. **Vérifier un token live** (seulement si l'environnement tourne après l'étape 9) : obtenez un token du persona et décodez le payload ; `roles` et `groups` doivent contenir les valeurs attendues.
    ```bash
    token=$(bash ./scripts/get-token.sh claire)
    TOKEN="$token" pwsh -NoProfile -Command '$p = $env:TOKEN.Split(".")[1].Replace("-","+").Replace("_","/"); $p = $p.PadRight($p.Length + (4 - $p.Length % 4) % 4, "="); [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)) | ConvertFrom-Json | Select-Object roles, groups | ConvertTo-Json'
    ```
    (PowerShell : `$token = ./scripts/get-token.ps1 -Persona claire`, puis le même décodage sur `$token`. Sans `pwsh` : `echo "$token" | cut -d. -f2 | tr '_-' '/+' | base64 -d 2>/dev/null`, le remplissage `=` est toléré par GNU `base64 -d`.) Le claim `groups` contient le GUID de l'attribut `objectId`, pas le nom du groupe.

## Retirer ou renommer

- **Retirer un rôle ou un groupe** : cherchez d'abord ses usages (`grep -rn "<Nom>" src tests`). S'il est utilisé par une policy ou un endpoint, **demandez** avant, puis retirez dans l'ordre : tests, policy, constante ou propriété, validation, configuration, realm (rôle, groupe, références des personas). Attention : retirer le seul rôle de `RequireAdmin` rend l'API inaccessible aux administrateurs.
- **Renommer** = ajouter le nouveau + migrer les usages + retirer l'ancien. La valeur doit changer partout en même temps (code, realm, Entra) ; côté Entra, indiquez qu'il faut créer le nouvel app role, réassigner les utilisateurs, puis désactiver l'ancien.
- **Retirer un persona** : retirez l'utilisateur du realm et du profil ; vérifiez que `scripts/smoke-test.*` (admin, user) et la doc n'en dépendent pas. Ne retirez jamais `admin@local.test` ni `user@local.test` sans demander.

## Modèle `Authenticated`

Le modèle n'a ni rôle, ni groupe, ni policy : tout utilisateur connecté a les mêmes droits. Pour ajouter un rôle ou un groupe, il faut d'abord **changer de modèle d'autorisation**, c'est-à-dire régénérer le projet avec une autre valeur de `--AuthorizationModel` : cela change les claims, le realm et la configuration. Ne tentez pas de le simuler à la main. Expliquez-le à l'utilisateur, renvoyez à https://floriandrevet.github.io/template-CQRS/identite/modeles-d-autorisation/ et demandez comment il veut procéder. Seuls les **personas** (sans `realmRoles` ni `groups`) restent possibles.

## Terminé quand

- `dotnet build` sans erreur et `dotnet test` entièrement vert.
- Constantes, validation, policies, `appsettings*.json`, `ApiFactory` et realm sont cohérents entre eux (mêmes noms, même GUID local) ; le JSON du realm est valide ; aucun persona ne cite un rôle ou un groupe absent du realm.
- Si une policy a été ajoutée : des tests la couvrent (accepté, refusé en 403). Un rôle ou un persona sans policy n'en demande pas.
- `project-profile.json` est à jour.
- Les étapes Entra ont été communiquées à l'utilisateur.
- Si l'environnement tourne : le token du persona contient les `roles` / `groups` attendus. Sinon, dites que ce contrôle reste à faire au prochain `aspire start`.

## Ne jamais faire sans demander

- Supprimer des volumes (`aspire stop ... --volumes` ou `docker volume rm` : efface aussi la base SQL locale) : accord explicite à chaque fois ; `docker volume rm` seulement sur des noms exacts confirmés, jamais par filtre.
- Retirer ou renommer un rôle ou un groupe utilisé par une policy ou un endpoint.
- Changer le modèle d'autorisation (régénération du projet).
- Retirer `admin@local.test` / `user@local.test` ou changer leur mot de passe local.
- Mettre un secret ou un vrai mot de passe dans le realm (le mot de passe local `Passw0rd!` est public), ou référencer un compte Entra réel.
- Désactiver une validation, une policy ou un test pour faire passer le build.
