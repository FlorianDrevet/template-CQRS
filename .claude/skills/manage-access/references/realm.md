# Realm Keycloak local

Fichier : `src/Web.Template.CQRS.AppHost/Realms/template-realm.json` (un seul par projet généré, correspondant à son modèle d'autorisation). Il n'existe que si le projet a Aspire. Realm `template`, clients `template-api`, `template-scalar`, `template-cli`.

Ce que contient le realm selon le modèle :

- `AppRoles` : `roles.realm` = `Admin`, `User` ; `admin@local.test` a `realmRoles: ["Admin"]`, `user@local.test` a `["User"]` ; mapper `entra-roles` (claim `roles`). Pas de clé `groups`.
- `Groups` : pas de `roles` ; `groups` = `Administrators` (`objectId` = `6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11`) ; `admin@local.test` a `groups: ["/Administrators"]`, `user@local.test` aucun droit ; mapper `entra-groups` (claim `groups`).
- `AppRolesAndGroups` : les deux.
- `Authenticated` : ni rôles ni groupes ; seulement `admin@local.test` et `user@local.test` sans droits distincts.

Tous : mot de passe local `Passw0rd!` (public, jamais un vrai secret), `emailVerified: true`, `enabled: true`.

## Rôle

Dans `roles.realm` (modèles avec rôles). `name` = valeur de la constante `AppRoles`, exactement.

```json
{ "name": "Editor", "description": "Mirrors the Entra app role Editor." }
```

## Groupe

Dans `groups` (modèles avec groupes). Le `objectId` est le GUID **local**, le même que dans `appsettings.Development.json` ; le mapper `entra-groups` le place dans le claim `groups` des membres. `path` = `/` + `name`.

```json
{
  "name": "Editors",
  "path": "/Editors",
  "attributes": {
    "objectId": [ "b2d7e9a1-3c54-4f68-8e0a-5d1f7c9b2a36" ]
  }
}
```

## Persona

Dans `users`. L'exemple suppose que le rôle `Editor` et le groupe `Editors` ont été ajoutés avant au realm : ne référencez que des rôles et groupes existants.

```json
{
  "username": "claire@local.test",
  "email": "claire@local.test",
  "emailVerified": true,
  "enabled": true,
  "firstName": "Claire",
  "lastName": "Editor",
  "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ],
  "realmRoles": [ "Editor" ],
  "groups": [ "/Editors" ]
}
```

- `realmRoles` : **noms** des rôles du realm. `groups` : **chemins** des groupes (avec la barre oblique). Omettez la clé qui ne correspond pas à votre modèle ou si le persona n'a pas de droit.
- Sur un projet `Authenticated`, n'ajoutez ni `realmRoles` ni `groups`.
- `username` et `email` identiques ; `scripts/get-token.*` accepte `claire` (complété en `claire@local.test`).

## Règles et pièges

- **`firstName` et `lastName` obligatoires** : sans eux Keycloak juge le profil incomplet, exige une mise à jour du profil à la première connexion et le flux « password grant » de `get-token` échoue. Ils fournissent aussi le claim `name`.
- `emailVerified: true` et `"temporary": false` évitent d'autres actions requises à la connexion.
- **Référence à un rôle ou à un groupe absent du realm = l'import échoue** (par exemple `/Editors` dans un projet `AppRoles`, ou `Editor` dans un projet `Groups`). Après chaque modification, relisez chaque `realmRoles` et `groups` des utilisateurs par rapport à `roles.realm` et `groups`.
- Un rôle ou un groupe retiré du realm doit aussi disparaître des utilisateurs qui le portent.
- JSON strict : pas de commentaires, pas de virgule finale. Validez avec `pwsh -NoProfile -Command "Get-Content 'src/Web.Template.CQRS.AppHost/Realms/template-realm.json' -Raw | ConvertFrom-Json | Out-Null; 'JSON OK'"` (ou `jq . <fichier>`).
- **Import au premier démarrage seulement.** Un volume Keycloak existant ignore le fichier modifié. Pour réimporter, suivez l'étape 9 de `manage-access` (`aspire stop … --force --volumes`, contrôle des volumes restants, suppression par noms exacts confirmés, `aspire start`) : **elle efface aussi les données SQL locales**, accord explicite de l'utilisateur d'abord. Dans un projet fraîchement généré (jamais démarré), il n'y a rien à réinitialiser.
- Un conteneur Keycloak persistant d'un autre projet peut garder le port 8080 et servir un ancien realm : si le token ne contient pas le nouveau rôle après réinitialisation, vérifiez quel conteneur répond sur `https://localhost:8080`.
- Un utilisateur créé à la main dans la console Keycloak disparaît avec le volume : pour un persona partagé, écrivez-le dans le fichier.
- Vérification d'un token : `bash ./scripts/get-token.sh <persona>` (PowerShell : `./scripts/get-token.ps1 -Persona <persona>`), puis décodage du payload (partie centrale du JWT, base64url ; commandes pwsh et bash à l'étape 10 de `manage-access`). `roles` contient les valeurs de rôle, `groups` les GUID `objectId`.
