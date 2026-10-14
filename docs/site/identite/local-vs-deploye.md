# Configuration locale et déployée

Sur votre poste, l'identité vient de Keycloak, configuré par Aspire. Une fois déployée, elle vient toujours de Microsoft Entra ID. Cette page est le **comparatif exhaustif** : pour chaque clé de configuration liée à l'identité, sa valeur locale, sa valeur déployée, et où la fournir. Elle complète la page [Aspire : local et déployé](../aspire/local-vs-deploye.md) et s'appuie sur [Keycloak en local](keycloak-local.md) et [Entra ID](entra-id.md).

## Le concept simplement

L'API lit sa configuration dans cet ordre, la source suivante **écrasant** la précédente :

1. `appsettings.json` : les valeurs de base, ici celles d'Entra (vides, à remplir).
2. `appsettings.{Environment}.json` : par exemple `appsettings.Development.json`, lu uniquement quand l'environnement ASP.NET Core est `Development`.
3. Les **variables d'environnement** : `Auth__Authority` correspond à la clé `Auth:Authority` (le double souligné remplace les deux-points).

Le template exploite cette mécanique : `appsettings.json` décrit la cible (Entra), `appsettings.Development.json` et l'AppHost surchargent pour Keycloak en local, et le déploiement fournit les vraies valeurs Entra par variables d'environnement. **Le code n'est pas modifié.**

## Pourquoi c'est important

- La première cause d'un 401 après déploiement est une valeur d'identité copiée depuis le local (audience `template-api`, autorité `localhost`) ou oubliée.
- Les clés sont peu nombreuses mais dépendent les unes des autres : savoir lesquelles sont propres à Entra, lesquelles à Keycloak, évite de chercher au mauvais endroit.

## Comment le template le fait

### Les valeurs de base : Entra, à remplir

```json title="src/Web.Template.CQRS.Api/appsettings.json"
--8<-- "src/Web.Template.CQRS.Api/appsettings.json" start='"Auth": {' before='"ConnectionStrings"'
```

!!! info "Directives dans les extraits"
    Les lignes `//#if (UseGroups)` et `//#endif` des fichiers JSON sont des directives du template. Dans un projet généré, seules les branches correspondant à vos options sont conservées : par exemple, sans groupes, la section `Authorization` n'existe pas.

### Les surcharges locales

```json title="src/Web.Template.CQRS.Api/appsettings.Development.json"
--8<-- "src/Web.Template.CQRS.Api/appsettings.Development.json" start='//#if (EnableAspire)' before='"ConnectionStrings"'
```

Avec Aspire, l'AppHost fournit en plus `Auth__Provider`, `Auth__Authority` et `Auth__Audience` à l'API par variables d'environnement (voir [Keycloak en local](keycloak-local.md)). Les deux sources donnent les mêmes valeurs : les variables de l'AppHost s'appliquent quand Aspire lance l'API, et `appsettings.Development.json` permet de la lancer seule depuis l'IDE (Keycloak doit alors tourner par ailleurs).

!!! note "Si vous avez généré le projet sans Aspire"
    La section `Auth` locale n'existe pas dans `appsettings.Development.json` (directive `EnableAspire`) : sans Keycloak, vous configurez un tenant Entra de développement ou votre propre fournisseur, avec les mêmes clés que celles du tableau ci-dessous.

### Le tableau clé par clé

| Clé | Valeur en local | Valeur déployée (Entra) | Où la fournir en production |
|-----|-----------------|-------------------------|-----------------------------|
| `Auth:Provider` | `Keycloak` (AppHost et `appsettings.Development.json`) | `Entra` (valeur de `appsettings.json`, rien à fournir) | `Auth__Provider` seulement si vous voulez l'écrire explicitement |
| `Auth:Authority` | `https://localhost:8080/realms/template` | **non utilisée** : Entra déduit l'autorité de `Instance` et `TenantId` | rien |
| `Auth:Audience` | `template-api` | *Application ID URI* de l'API (ou vide pour la valeur par défaut `api://<ClientId>`) | `Auth__Audience` |
| `Auth:Instance` | non utilisée par Keycloak | `https://login.microsoftonline.com/` (déjà dans `appsettings.json`) | `Auth__Instance` si le cloud n'est pas le cloud public |
| `Auth:TenantId` | non utilisée par Keycloak | *Directory (tenant) ID* | `Auth__TenantId` |
| `Auth:ClientId` | non utilisée par Keycloak | *Application (client) ID* de l'inscription de l'API | `Auth__ClientId` |
| `Auth:OpenApiClientId` | non utilisée : le client Scalar local est la constante `template-scalar` | *Application (client) ID* de l'inscription cliente | `Auth__OpenApiClientId` (seulement si Scalar est utilisé) |
| `Auth:OpenApiScope` | non utilisée : Scalar demande `openid profile email` | `api://<client-id>/access_as_user` | `Auth__OpenApiScope` (seulement si Scalar est utilisé) |
| `Authorization:Groups:Administrators` | `6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11` (groupe du realm local) | *Object ID* du groupe Entra des administrateurs | `Authorization__Groups__Administrators` |
| `Cors:AllowedOrigins` | `["http://localhost:4200"]` (`appsettings.Development.json`) | URL(s) du ou des frontends | `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, … |

Précisions sur le tableau :

- `Authorization:Groups:Administrators` n'existe que pour les modèles `Groups` et `AppRolesAndGroups` (voir [Modèles d'autorisation](modeles-d-autorisation.md)). Valeur vide ou non GUID : l'API refuse de démarrer.
- Une **liste** dans une variable d'environnement s'écrit élément par élément avec un indice : `Cors__AllowedOrigins__0=https://app.contoso.com`. Voir [CORS](../fonctionnalites/cors.md).
- `Auth:Authority` est exigée en HTTPS hors développement (sauf adresse locale en développement) ; `Auth:Audience` est obligatoire avec Keycloak, facultative avec Entra.
- Les valeurs d'identité (identifiants de tenant et de client, GUID) ne sont **pas des secrets** au sens strict, mais elles sont propres à un environnement : ne les mettez pas dans `appsettings.json` du dépôt.
- Aucune clé d'identité n'est un secret côté API : elle ne détient ni mot de passe ni *client secret*. Elle valide seulement des signatures avec des clés publiques.

### Ce qui change, ce qui ne change pas

| Ne change pas | Change |
|---------------|--------|
| Le code de l'API et des couches | Les valeurs de `Auth:*` |
| Les policies et le modèle d'autorisation | Le fournisseur (Keycloak devient Entra) |
| Les noms de claims lus (`oid`, `roles`, `groups`, `name`, `email`) | L'émetteur (`iss`) et l'audience (`aud`) des tokens |
| Les valeurs de rôles (`Admin`, `User`) | L'origine des comptes : personas locaux, puis vrais utilisateurs et attributions Entra |
| Le format du GUID de groupe | La valeur du GUID de groupe |
| `ICurrentUser` | La disponibilité de Scalar (uniquement en `Development`) |
| La validation de signature, d'issuer, d'audience, d'expiration | Les clés de signature (JWKS) : celles du tenant Entra, téléchargées automatiquement |

Ce qui n'existe **pas** en production : le conteneur Keycloak, le script `get-token`, le client `template-cli`, les personas `admin@local.test` et `user@local.test`, le mot de passe `Passw0rd!`. Rien de cela ne doit être déployé ni référencé.

### Check-list de passage en production

1. Créer l'inscription de l'API et l'inscription cliente dans Entra ([Entra ID](entra-id.md)).
2. Créer les app roles `Admin` et `User` (valeurs identiques à `AppRoles`) et les attribuer dans *Enterprise applications*.
3. Si le modèle utilise les groupes : créer le groupe, ajouter le claim de groupes (*Group ID*), relever l'*Object ID*.
4. Renseigner `Auth__TenantId`, `Auth__ClientId`, `Auth__Audience` (si nécessaire) dans l'environnement de l'hébergeur.
5. Renseigner `Authorization__Groups__Administrators` (modèles avec groupes).
6. Renseigner `Cors__AllowedOrigins__N` avec l'adresse de chaque frontend.
7. Vérifier que l'environnement n'est pas `Development` (sinon Scalar est exposé et `RequireHttpsMetadata` est désactivé en Keycloak).
8. Démarrer : un échec de validation au démarrage nomme la clé manquante.
9. Appeler `/me` avec un vrai token Entra : vérifier `oid`, `roles`, `groups` ; appeler `/admin/ping` avec un compte administrateur (200) et un compte ordinaire (403).

Pour la mise en production de l'ensemble de l'application (hébergement, secrets, base), voir le [guide de configuration d'un déploiement](../guides/configurer-un-deploiement.md) et [Aspire : local et déployé](../aspire/local-vs-deploye.md).

## À essayer

1. Dans un projet généré, lancez l'API seule avec `ASPNETCORE_ENVIRONMENT=Production` et sans variables `Auth__*` : relevez le message de validation qui nomme les clés manquantes.
2. Prenez un token local (`get-token`) et décodez-le : quelles valeurs de `iss` et `aud` ne correspondraient pas à votre tenant Entra ?
3. Complétez, pour votre futur environnement, une copie du tableau ci-dessus avec vos vraies valeurs, hors dépôt.
