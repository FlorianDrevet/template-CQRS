# Keycloak en local

Cette page est la **référence Keycloak** du template : ce que c'est, ce qu'il permet de faire sur votre poste, comment il est configuré, comment il imite Microsoft Entra ID, et comment l'utiliser au quotidien. Elle est longue parce qu'elle sert aussi de cours. Si les mots *token*, *issuer* ou *audience* ne vous disent rien, lisez d'abord [Identité](/Identité.md).

> ℹ️ **Keycloak n'est qu'un décor local**
>
> En production, l'identité vient toujours de Microsoft Entra ID (voir [Entra ID](/Identité/Entra-ID.md)). Keycloak ne sert qu'à développer et tester **sans tenant Azure**. Il n'est jamais déployé, et rien dans le code de l'API ne dépend de lui : seule la configuration change (voir [Configuration locale et déployée](/Identité/Configuration-locale-et-déployée.md)).

## Le concept simplement

### Qu'est-ce que Keycloak ?

**Keycloak** est un serveur d'identité open source. Il sait authentifier des utilisateurs (formulaire de connexion, mot de passe) et délivrer des tokens OAuth2 / OpenID Connect, exactement ce que fait Entra ID pour votre entreprise. Il tourne dans un **conteneur** démarré par Aspire (voir [Émulateurs](/Aspire/Émulateurs.md)) : vous n'installez rien.

### Ce que cela vous permet de faire en local

- Vous connecter à l'API **avec de vrais tokens JWT signés**, sans compte Azure.
- Tester le contrôle d'accès avec deux **personas** : un administrateur et un utilisateur ordinaire.
- Utiliser **Scalar** (la documentation interactive) avec un vrai bouton « Authorize », qui exécute le flux Authorization Code avec PKCE.
- Obtenir un token en ligne de commande pour `curl`, Postman ou un script.
- Valider que l'API refuse bien un token invalide, expiré ou sans droit (401 / 403).

### Le vocabulaire Keycloak

| Terme | Signification | Équivalent Entra |
|-------|---------------|------------------|
| **Realm** | Un espace isolé qui regroupe utilisateurs, clients, rôles et groupes. Le template utilise le realm `template`. | Un *tenant* |
| **Client** | Une application déclarée auprès du realm (l'API, Scalar, un script). | Une *App Registration* |
| **User** | Un compte (identifiant, mot de passe, attributs). | Un utilisateur |
| **Realm role** | Un rôle global du realm, que l'on attribue à des utilisateurs. | Un *app role* |
| **Group** | Un ensemble d'utilisateurs, avec des attributs. | Un groupe de sécurité |
| **Protocol mapper** | Une règle qui ajoute une information (claim) dans le token. | Les claims optionnels et la configuration du token |

## Pourquoi c'est important

- Sans Keycloak, il faudrait un tenant Entra pour chaque développeur et chaque pipeline, avec des comptes réels.
- Un décor trop différent de la cible ferait passer en local des tests qui échoueraient en production. Le realm est donc écrit pour que les tokens locaux **aient la même forme** que ceux d'Entra (mêmes noms de claims, même format des groupes).
- Comprendre le realm vous permet de le modifier : ajouter un persona, un rôle, un groupe.

## Comment le template le fait

### Le démarrage de Keycloak par l'AppHost

**Fichier :** `src/Web.Template.CQRS.AppHost/Program.cs`

```csharp
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var keycloak = builder
    .AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
    .WithRealmImport("./Realms")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
```

`AddKeycloak("keycloak", port: 8080, …)` démarre le conteneur sur le port 8080 de l'hôte. `WithRealmImport("./Realms")` indique à Keycloak d'importer les fichiers JSON du dossier `Realms`. `WithDataVolume()` conserve les données dans un volume Docker, et le mot de passe d'administration vient du paramètre `keycloak-admin-password` (voir [Paramètres et secrets](/Aspire/Paramètres-et-secrets.md)).

L'API reçoit sa configuration d'identité de l'AppHost par des variables d'environnement :

**Fichier :** `src/Web.Template.CQRS.AppHost/Program.cs`

```csharp
var api = builder
    .AddProject("api", "../Web.Template.CQRS.Api/Web.Template.CQRS.Api.csproj")
    .WithReference(projectDatabase)
    .WithReference(blobs)
    .WithReference(keycloak)
    .WaitFor(sqlServer)
    .WaitFor(blobs)
    .WaitFor(keycloak)
    .WithEnvironment("Auth__Provider", "Keycloak")
    .WithEnvironment("Auth__Authority", "https://localhost:8080/realms/template")
    .WithEnvironment("Auth__Audience", "template-api")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/alive");
```

Et le fichier `appsettings.Development.json` porte les mêmes valeurs pour un lancement sans Aspire (par exemple depuis l'IDE) :

**Fichier :** `src/Web.Template.CQRS.Api/appsettings.Development.json`

```json
"Auth": {
  "Provider": "Keycloak",
  "Authority": "https://localhost:8080/realms/template",
  "Audience": "template-api"
},
```

> ℹ️ **Directives dans les extraits**
>
> Le dépôt est la **source du template** : certains fichiers contiennent des directives comme `//#if (UseGroups)`. Dans un projet généré, seules les branches correspondant à vos options sont conservées.

### Ce que valide l'API en mode Keycloak

**Fichier :** `src/Web.Template.CQRS.Api/Authentication/AuthenticationSetup.cs`

```csharp
var authentication = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
if (authOptions.Provider == AuthProvider.Entra)
{
    authentication.AddMicrosoftIdentityWebApi(authSection);
    services.Configure<JwtBearerOptions>(
        JwtBearerDefaults.AuthenticationScheme,
        ConfigureCommonJwtOptions);
}
else
{
    authentication.AddJwtBearer(options =>
    {
        ConfigureCommonJwtOptions(options);
        options.Authority = authOptions.Authority;
        options.Audience = authOptions.Audience;
        options.RequireHttpsMetadata = !environment.IsDevelopment();
    });
}
```

- `Authority` est l'URL du realm : l'API en déduit le document de découverte (`<Authority>/.well-known/openid-configuration`), l'**issuer** attendu et l'adresse des clés publiques de signature.
- `Audience` est `template-api` : un token dont `aud` ne contient pas cette valeur est refusé.
- `RequireHttpsMetadata` vaut `false` en développement seulement : en production, l'autorité doit être en HTTPS. Le contrôle de démarrage n'accepte l'HTTP que pour une adresse locale (*loopback*) en développement.

### Le realm `template` : rôles et groupe

Le realm est décrit dans un seul fichier, importé par Keycloak :

**Fichier :** `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`

```json
"roles": {
  "realm": [
    { "name": "Admin", "description": "Mirrors the Entra app role Admin." },
    { "name": "User", "description": "Mirrors the Entra app role User." }
  ]
},
"groups": [
  {
    "name": "Administrators",
    "path": "/Administrators",
    "attributes": {
      "objectId": [ "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11" ]
    }
  }
],
```

- Les deux **rôles** `Admin` et `User` portent exactement les valeurs des constantes `AppRoles` de l'API (voir [Modèles d'autorisation](/Identité/Modèles-d'autorisation.md)).
- Le **groupe** `Administrators` a un attribut `objectId`. Dans Entra, un groupe est identifié par un GUID dans le claim `groups` ; ici le GUID est stocké dans cet attribut, et un mapper le placera dans le token. C'est la valeur à fournir à `Authorization:Groups:Administrators` côté API (elle figure dans `appsettings.Development.json`).

### Les personas

**Fichier :** `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`

```json
"users": [
  {
    "username": "admin@local.test",
    "email": "admin@local.test",
    "emailVerified": true,
    "enabled": true,
    "firstName": "Alice",
    "lastName": "Admin",
    "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ],
    "realmRoles": [ "Admin" ],
    "groups": [ "/Administrators" ]
  },
  {
    "username": "user@local.test",
    "email": "user@local.test",
    "emailVerified": true,
    "enabled": true,
    "firstName": "Bob",
    "lastName": "User",
    "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ],
    "realmRoles": [ "User" ]
  }
],
```

| Persona | Nom affiché | Rôle | Groupe | Mot de passe |
|---------|-------------|------|--------|--------------|
| `admin@local.test` | Alice Admin | `Admin` | `Administrators` | `Passw0rd!` |
| `user@local.test` | Bob User | `User` | aucun | `Passw0rd!` |

> ⚠️ **Mot de passe public**
>
> `Passw0rd!` est écrit dans le dépôt : il n'est valable que pour ce realm local. Ne le réutilisez jamais ailleurs.

### Les trois clients

| Client | Rôle | Particularité |
|--------|------|---------------|
| `template-api` | Représente l'API | `bearerOnly` : il ne peut pas servir à se connecter, seulement à identifier la ressource (c'est l'audience des tokens) |
| `template-scalar` | Scalar | Client public, flux Authorization Code avec PKCE (S256), redirections limitées aux adresses de l'API |
| `template-cli` | Le script `get-token` | Client public avec *password grant* : **réservé au développement local** |

**Fichier :** `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`

```json
"clientId": "template-api",
"name": "Template API",
"enabled": true,
"bearerOnly": true,
"protocol": "openid-connect",
"publicClient": false,
"standardFlowEnabled": false,
"directAccessGrantsEnabled": false
```

L'extrait ci-dessus (une partie seulement de l'objet) montre que `template-api` est « bearer only » : pas de flux interactif (`standardFlowEnabled` à `false`), pas de mot de passe direct.

**Fichier :** `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`

```json
"clientId": "template-scalar",
"name": "Template Scalar API reference",
"enabled": true,
"protocol": "openid-connect",
"publicClient": true,
"standardFlowEnabled": true,
"directAccessGrantsEnabled": false,
"attributes": { "pkce.code.challenge.method": "S256" },
"redirectUris": [ "http://localhost:5257/*", "https://localhost:7246/*" ],
"webOrigins": [ "http://localhost:5257", "https://localhost:7246" ],
```

Cet extrait (propriétés seulement, sans accolades ni mappers) montre que `template-scalar` active le flux Authorization Code (`standardFlowEnabled`), impose PKCE avec `S256`, interdit le password grant, et n'accepte de redirection que vers `http://localhost:5257/*` et `https://localhost:7246/*` : un token ne peut pas être livré à une autre adresse. Ses `protocolMappers` suivent dans le fichier.

**Fichier :** `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`

```json
"clientId": "template-cli",
"name": "Template local CLI (password grant, local development only)",
"enabled": true,
"protocol": "openid-connect",
"publicClient": true,
"standardFlowEnabled": false,
"directAccessGrantsEnabled": true,
```

`template-cli` est l'inverse : pas d'interface (`standardFlowEnabled` à `false`), mais `directAccessGrantsEnabled` à `true`, ce qui autorise l'envoi direct de l'identifiant et du mot de passe au point d'accès `/token`. Pratique pour un script, déconseillé ailleurs.

### Comment le realm imite Entra

Le travail important est dans les **protocol mappers** : chacun des deux clients `template-scalar` et `template-cli` en déclare quatre, identiques. Ils fabriquent dans l'access token les claims que l'API attend de la part d'Entra.

| Claim attendu (Entra) | Mapper Keycloak | Type de mapper | Ce qu'il fait |
|-----------------------|-----------------|----------------|---------------|
| `aud` = l'API | `template-api-audience` | `oidc-audience-mapper` | Ajoute `template-api` à l'audience (`included.client.audience`) |
| `oid` | `entra-oid` | `oidc-usermodel-property-mapper` | Copie l'identifiant interne de l'utilisateur (`user.attribute: id`) dans le claim `oid` |
| `roles` | `entra-roles` | `oidc-usermodel-realm-role-mapper` | Place les rôles du realm de l'utilisateur dans le claim `roles` (multivalué) |
| `groups` | `entra-groups` | `oidc-usermodel-attribute-mapper` | Place l'attribut `objectId` du groupe dans le claim `groups` ; `aggregate.attrs` agrège les attributs des groupes de l'utilisateur |
| `name`, `email` | (aucun mapper ajouté) | portées standard `profile` et `email` de Keycloak | Fournissent `name` (prénom et nom) et `email` |
| `iss` | (natif) | | L'URL du realm : `https://localhost:8080/realms/template` |
| `scp` | (non reproduit) | | L'API n'utilise pas les scopes pour autoriser |

Chaque mapper a un nom, un type et une `config`. Pour `entra-oid`, `entra-roles` et `entra-groups`, elle précise le nom du claim (`claim.name`) et s'il est multivalué (`multivalued`) ; `template-api-audience` utilise à la place `included.client.audience`. Tous activent `access.token.claim` et désactivent `id.token.claim` : seul l'access token, celui que lit l'API, les contient. Voici l'extrait complet du client `template-cli`, mappers compris (les accolades finales ne sont pas reprises) :

**Fichier :** `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`

```json
"clientId": "template-cli",
"name": "Template local CLI (password grant, local development only)",
"enabled": true,
"protocol": "openid-connect",
"publicClient": true,
"standardFlowEnabled": false,
"directAccessGrantsEnabled": true,
"protocolMappers": [
  {
    "name": "template-api-audience",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-audience-mapper",
    "consentRequired": false,
    "config": { "included.client.audience": "template-api", "id.token.claim": "false", "access.token.claim": "true", "introspection.token.claim": "true" }
  },
  {
    "name": "entra-oid",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-usermodel-property-mapper",
    "consentRequired": false,
    "config": { "user.attribute": "id", "claim.name": "oid", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
  },
  {
    "name": "entra-roles",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-usermodel-realm-role-mapper",
    "consentRequired": false,
    "config": { "claim.name": "roles", "multivalued": "true", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
  },
  {
    "name": "entra-groups",
    "protocol": "openid-connect",
    "protocolMapper": "oidc-usermodel-attribute-mapper",
    "consentRequired": false,
    "config": { "user.attribute": "objectId", "claim.name": "groups", "multivalued": "true", "aggregate.attrs": "true", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
```

Résultat observé sur un token réel du template (token d'administrateur) : `aud` contient `template-api`, `oid` est un UUID, `roles` vaut exactement `["Admin"]`, `groups` vaut `["6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"]`, `name` est « Alice Admin » et `email` est `admin@local.test`. Pour `user`, `roles` vaut `["User"]` et il n'y a pas de claim `groups`. Keycloak n'ajoute aucun rôle par défaut au claim `roles`.

### Les variantes de realm par modèle d'autorisation

Au moment de la génération du projet, vous choisissez un [modèle d'autorisation](/Identité/Modèles-d'autorisation.md). Le dossier `RealmVariants/` contient un realm par modèle, et le générateur ne garde que celui qui correspond, renommé en `Realms/template-realm.json` :

| Modèle | Fichier source | Contenu du realm |
|--------|----------------|------------------|
| `AppRolesAndGroups` | `Realms/template-realm.json` | rôles, groupe, mappers `entra-roles` et `entra-groups` (celui décrit sur cette page) |
| `AppRoles` | `RealmVariants/template-realm.approles.json` | rôles et mapper `entra-roles`, sans groupe |
| `Groups` | `RealmVariants/template-realm.groups.json` | groupe et mapper `entra-groups`, sans rôles |
| `Authenticated` | `RealmVariants/template-realm.authenticated.json` | ni rôles ni groupe, ni leurs mappers : deux personas qui se connectent seulement |

Ainsi un projet généré n'embarque que les claims dont son modèle a besoin. Dans le dépôt template, les quatre fichiers coexistent.

### Obtenir un token

Le script `get-token` interroge le point d'accès `/token` du realm avec le client `template-cli` et affiche l'access token. Avec l'AppHost lancé :

**PowerShell**


```powershell
./scripts/get-token.ps1 -Persona admin
# pour appeler l'API directement :
$token = ./scripts/get-token.ps1 -Persona admin
Invoke-RestMethod https://localhost:7246/me -Headers @{ Authorization = "Bearer $token" }
```

**Bash**


```bash
scripts/get-token.sh admin
# pour appeler l'API directement :
curl -H "Authorization: Bearer $(scripts/get-token.sh admin)" https://localhost:7246/me
```

Le nom du persona peut être court (`admin` devient `admin@local.test`) ou complet. Un mot de passe différent se passe en second argument (Bash) ou avec `-Password` (PowerShell). Les paramètres `-KeycloakUrl`, `-Realm`, `-ClientId` (PowerShell) et les variables `KEYCLOAK_URL`, `KEYCLOAK_REALM`, `KEYCLOAK_CLIENT_ID` (Bash) permettent de viser un autre serveur. En cas d'échec, le script sort avec un code non nul et affiche la réponse de Keycloak.

Pour **décoder** le token et lire ses claims, collez-le dans [jwt.ms](https://jwt.ms) ou [jwt.io](https://jwt.io).

> 🛑 **Ne collez dans un décodeur en ligne que des tokens locaux**
>
> Un token est une clé d'accès : un token de production, collé dans un site tiers, peut être volé et rejoué jusqu'à son expiration. Les tokens du realm `template` sont sans danger : ils ne valent que sur votre poste.

L'appel à `/me` renvoie l'identifiant, le nom, l'email, les rôles et les groupes lus par l'API ; `/admin/ping` répond 200 pour `admin`, 403 pour `user` et 401 sans token.

### La console d'administration de Keycloak

Ouvrez `https://localhost:8080`, puis la console d'administration. L'identifiant est `admin` (valeur par défaut d'Aspire pour Keycloak) et le mot de passe est celui du paramètre `keycloak-admin-password` (voir [Premier lancement](/Démarrer/Premier-lancement.md) pour le définir). Choisissez ensuite le realm `template` dans le sélecteur en haut à gauche : n'agissez pas dans le realm `master`, qui sert à administrer Keycloak lui-même.

Vous y verrez *Users*, *Groups*, *Realm roles*, *Clients* (les mappers d'un client se trouvent dans *Clients > le client > Client scopes > `<client>-dedicated` > Mappers*). C'est un bon endroit pour comprendre : changez un attribut, redemandez un token, observez le claim.

### Créer un utilisateur à la main

1. *Users > Create new user* : renseignez *Username* (par exemple `claire@local.test`), *Email*, *First name*, *Last name*, activez *Email verified*, puis *Create*.
2. Onglet *Credentials > Set password* : saisissez un mot de passe et **désactivez** *Temporary*, sinon la connexion exigera une action supplémentaire et échouera avec `invalid_grant`.
3. Onglet *Role mapping > Assign role* : choisissez `Admin` ou `User`.
4. Onglet *Groups > Join group* : choisissez `Administrators` si l'utilisateur doit être administrateur par groupe.
5. Demandez un token : `scripts/get-token.sh claire@local.test <mot de passe>`.

Cet utilisateur vit dans le **volume** de Keycloak : il survit aux redémarrages mais disparaît à la réinitialisation ci-dessous. Pour un persona durable et partagé avec l'équipe, ajoutez-le plutôt dans `template-realm.json` (voir le [guide pour ajouter un rôle, un groupe ou un persona](/Guides/Rôles,-groupes-et-personas.md)).

### Le realm n'est importé qu'au premier démarrage

C'est le piège le plus fréquent. Keycloak importe le dossier `Realms` **uniquement quand le realm n'existe pas encore**, c'est-à-dire au premier démarrage avec un volume vide. Une fois le volume rempli, **modifier `template-realm.json` n'a aucun effet** : Keycloak considère le realm comme déjà importé et ne le relit pas.

Pour appliquer vos changements, réinitialisez les volumes. Cela efface aussi la base SQL locale :

```bash
aspire stop --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj --force --volumes
```

Au prochain lancement, Keycloak repart d'un volume vide et réimporte le realm. Pour ne réinitialiser que Keycloak sans perdre la base SQL, utilisez la méthode Docker de la page [Émulateurs](/Aspire/Émulateurs.md).

### HTTPS et certificat de développement

Keycloak est servi en **HTTPS** sur `https://localhost:8080`, avec le certificat de développement ASP.NET Core. L'API, `curl` et le navigateur doivent lui faire confiance : si ce n'est pas le cas, exécutez une fois `dotnet dev-certs https --trust` (voir [Prérequis](/Démarrer/Prérequis.md)). Sans cela, `get-token`, Scalar ou le téléchargement des clés par l'API échouent avec une erreur de certificat.

### Dépannage

| Symptôme | Cause probable | Solution |
|----------|----------------|----------|
| **401** et un message d'issuer invalide dans les logs de l'API | L'`iss` du token diffère de l'issuer attendu. Keycloak construit l'issuer à partir de l'adresse utilisée pour demander le token : un token obtenu via `https://127.0.0.1:8080` (ou un nom d'hôte de conteneur) ne correspond pas à `https://localhost:8080/realms/template` | Demandez le token à l'adresse exacte de `Auth:Authority` (`https://localhost:8080`), vérifiez l'`iss` dans [jwt.ms](https://jwt.ms) |
| **401** quand vous appelez `http://localhost:5257` | Ce port HTTP répond par une redirection vers HTTPS, et les clients abandonnent l'en-tête `Authorization` en suivant une redirection | Appelez directement `https://localhost:7246` |
| **401** et `aud` ne contient pas `template-api` | Token obtenu avec un autre client, ou mapper d'audience absent | Utilisez `template-cli` ou `template-scalar` ; vérifiez le mapper `template-api-audience` |
| **403** pour l'administrateur | Le claim attendu manque : `roles` ou `groups` absent ou différent | Décodez le token ; vérifiez le persona, le modèle d'autorisation et le GUID du groupe |
| `get-token` affiche `invalid_grant` | Mauvais mot de passe, utilisateur inexistant (realm pas importé, ou variante sans ce persona), compte avec une action requise (mot de passe temporaire) | Vérifiez l'identifiant et le mot de passe ; en cas de doute, réinitialisez le volume |
| Vos changements du realm sont sans effet | Realm déjà importé | Réinitialisez les volumes (section précédente) |
| Erreur de certificat | Certificat de développement non approuvé | `dotnet dev-certs https --trust` |
| Keycloak ne démarre pas | Le port 8080 est occupé | Voir « Conflits de ports » dans [Émulateurs](/Aspire/Émulateurs.md) |

## À essayer

1. Lancez l'AppHost, récupérez les tokens de `admin` et de `user`, et comparez leurs claims `roles` et `groups`.
2. Appelez `/admin/ping` avec chacun des deux tokens, puis sans token : retrouvez 200, 403 et 401.
3. Ouvrez la console d'administration, allez dans *Clients > template-cli > Client scopes > template-cli-dedicated > Mappers*, ouvrez le mapper `entra-roles`, et repérez le champ `claim.name`.
4. Créez un utilisateur à la main, donnez-lui le rôle `Admin`, obtenez son token et appelez `/admin/ping`.
5. Modifiez le prénom d'Alice dans `template-realm.json`, relancez sans réinitialiser : rien ne change. Réinitialisez, relancez : le changement apparaît dans le claim `name`.

## Pour aller plus loin

- [Documentation Keycloak](https://www.keycloak.org/documentation)
- [Documentation Aspire](https://aspire.dev) (rubrique des intégrations, Keycloak)
- [Import d'un realm](https://www.keycloak.org/server/importExport)
