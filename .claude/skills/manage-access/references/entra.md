# Étapes Entra à communiquer

Entra ID est la cible de production. Ces actions se font dans le portail par un humain ayant les droits sur le tenant : ne les exécutez pas, listez-les à l'utilisateur à la fin de l'opération, avec les valeurs exactes (nom du rôle, nom du groupe, clé de configuration). Référence détaillée : `docs/site/identite/entra-id.md` du dépôt du template, sections « Les app roles » et « Le claim de groupes » (cette doc n'est pas copiée dans le projet généré).

## Nouveau rôle (app role)

1. Portail Azure > *Microsoft Entra ID* > *App registrations* > inscription de **l'API** > *App roles* > *Create app role*.
2. *Display name* libre ; **Value** = la valeur de la constante `AppRoles` (par exemple `Editor`), caractère pour caractère ; *Allowed member types* = *Users/Groups* ; *Do you want to enable this app role?* coché.
3. L'app role n'a d'effet qu'une fois attribué : *Enterprise applications* > l'API > *Users and groups* > *Add user/group* > choisir l'utilisateur (ou un groupe) et le rôle.
4. Au prochain token de l'utilisateur, `roles` contient la valeur. Pour vérifier : décoder un token de test (pas de production) dans un outil comme jwt.ms.

## Nouveau groupe

1. *Microsoft Entra ID* > *Groups* > *New group* : type **Security**, nom lisible, ajouter les membres.
2. Relever l'**Object ID** du groupe. C'est la valeur à fournir à l'environnement déployé :
   - variable d'environnement `Authorization__Groups__<Nom>` (par exemple `Authorization__Groups__Editors`), ou le mécanisme de configuration de l'hébergeur (App Service, Container Apps, Key Vault, pipeline). Jamais dans `appsettings.json` (qui garde la valeur vide).
3. Le claim `groups` doit être activé une fois par inscription : inscription de l'API > *Token configuration* > *Add groups claim* > **Security groups**, format **Group ID** pour les tokens d'accès. Déjà fait si `Administrators` fonctionne.
4. Limite : un token Entra contient au plus 200 groupes ; au-delà le claim est remplacé par une référence et l'utilisateur n'est plus reconnu. Pour de grands annuaires, utilisez *Groups assigned to the application* ou préférez les app roles.

## Renommer ou retirer

- Renommer un rôle : créer le nouvel app role, réassigner les utilisateurs, déployer le code, puis désactiver (décocher *enabled*) puis supprimer l'ancien. Désactiver avant de déployer casserait les accès.
- Retirer un rôle : retirer d'abord les assignations, puis désactiver et supprimer l'app role.
- Retirer un groupe : retirer la variable `Authorization__Groups__<Nom>` en même temps que le déploiement du code qui ne la lit plus (sinon aucune erreur ; l'inverse fait échouer le démarrage).

## Persona

Un persona n'existe qu'en local (Keycloak). Il n'a pas d'équivalent Entra : rien à communiquer, sauf à rappeler que les vrais utilisateurs reçoivent leurs droits par les assignations ci-dessus.

## Modèle de message de fin

Terminez l'opération par une liste courte, par exemple :

- À faire dans Entra : créer l'app role `Editor` (valeur `Editor`) sur l'inscription de l'API et l'attribuer aux utilisateurs concernés.
- À faire dans Entra : créer le groupe de sécurité `Editors`, relever son Object ID, le fournir au déploiement via `Authorization__Groups__Editors`.
- Rien n'a été fait dans Entra par cet outil.
