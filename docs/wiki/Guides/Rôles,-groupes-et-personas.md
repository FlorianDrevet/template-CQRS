# Rôles, groupes et personas

Ce guide explique comment étendre les droits d'un projet généré : ajouter un **rôle** (par exemple `Editor`), un **groupe** de sécurité et un **persona** local (un utilisateur de test). Chaque opération touche à la fois le code, le realm Keycloak local et la configuration Entra ID : l'oubli d'un des trois est la cause la plus fréquente de « ça marche en local mais pas déployé ». Les noms de projet ci-dessous utilisent `Contoso.Orders`.

## Le concept simplement

Rappel (voir [Identité](/Identité.md) et [Modèles d'autorisation](/Identité/Modèles-d'autorisation.md)) :

- un **rôle** est une étiquette portée par l'utilisateur dans le claim `roles` ; l'API compare sa **valeur** à une constante du code ;
- un **groupe** est identifié par un **GUID** (*Object ID* dans Entra) porté par le claim `groups` ; l'API compare ce GUID à une valeur de configuration ;
- un **persona** est un utilisateur de test défini dans le realm Keycloak local ; il n'existe pas en production.

Chaque ajout se répète à trois endroits qui doivent rester cohérents :

| Où | Rôle | Groupe | Persona |
|----|------|--------|---------|
| Code de l'API | constante `AppRoles`, policy | propriété `GroupOptions`, validation, policy | rien |
| Keycloak local (`template-realm.json`) | rôle du realm | groupe avec attribut `objectId` | utilisateur |
| Entra ID (déployé) | app role | groupe de sécurité | vrais utilisateurs |

Les exemples qui suivent ajoutent le rôle `Editor` et le groupe `Editors`, autorisés à créer du contenu sans être administrateurs, puis le persona `claire@local.test`.

> ℹ️ **Selon votre modèle d'autorisation**
>
> Les étapes sur les rôles concernent les projets `AppRoles` et `AppRolesAndGroups`. Celles sur les groupes concernent `Groups` et `AppRolesAndGroups` : dans les autres projets, `GroupOptions` et la section `Authorization:Groups` n'existent pas. Avec `Authenticated`, il n'y a ni rôle ni groupe : voir la fin de la page. Les extraits de code ci-dessous sont ceux d'un projet `AppRolesAndGroups`, où les deux mécanismes coexistent.

## Pourquoi c'est important

Un rôle ajouté seulement dans le code donne 403 à tout le monde : Keycloak ne le met dans aucun token. Un rôle ajouté dans Keycloak mais pas dans Entra fonctionne en local et échoue en production. Un groupe dont le GUID n'est pas valide empêche l'API de démarrer. Ce guide donne l'ordre à suivre et la liste de contrôle à parcourir.

## Comment le template le fait

### Ajouter un rôle

**1. La constante.** Les valeurs de `AppRoles` doivent être identiques, caractère pour caractère, à celles des rôles dans Keycloak et dans Entra.

**Fichier :** `src/Contoso.Orders.Api/Authorization/AppRoles.cs`

```csharp
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string Editor = "Editor";
}
```

**2. Le rôle dans le realm local.** Dans `src/Contoso.Orders.AppHost/Realms/template-realm.json`, ajoutez le rôle dans `roles.realm` :

**Fichier :** `src/Contoso.Orders.AppHost/Realms/template-realm.json`

```json
"roles": {
  "realm": [
    { "name": "Admin", "description": "Mirrors the Entra app role Admin." },
    { "name": "User", "description": "Mirrors the Entra app role User." },
    { "name": "Editor", "description": "Mirrors the Entra app role Editor." }
  ]
},
```

Le mapper `entra-roles` du realm place tous les rôles du realm de l'utilisateur dans le claim `roles` : il n'y a rien d'autre à déclarer. Pour qu'un persona ait ce rôle, ajoutez son nom à la liste `realmRoles` de l'utilisateur (voir « Ajouter un persona » plus bas).

**3. L'app role dans Entra.** Dans l'inscription de l'API : *App roles > Create app role*, avec la valeur `Editor` et *Allowed member types* = *Users/Groups*. Puis attribuez-le : *Enterprise applications > votre API > Users and groups > Add user/group* (voir [Entra ID](/Identité/Entra-ID.md)).

**4. La policy.** Une **policy** donne un nom à une règle et s'applique à un endpoint. Ajoutez le nom dans `Policies` :

**Fichier :** `src/Contoso.Orders.Api/Authorization/Policies.cs`

```csharp
public static class Policies
{
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireEditor = "RequireEditor";
}
```

puis enregistrez-la dans `AuthorizationSetup.cs`, à côté de `RequireAdmin`. Dans un projet `AppRoles` seul, la règle tient en une ligne :

**Fichier :** `src/Contoso.Orders.Api/Authorization/AuthorizationSetup.cs (modèle AppRoles)`

```csharp
options.AddPolicy(Policies.RequireEditor, policy =>
    policy.RequireRole(AppRoles.Editor, AppRoles.Admin));
```

`RequireRole` avec plusieurs valeurs accepte l'utilisateur qui en possède **au moins une** : ici un éditeur ou un administrateur. Ajoutez cette instruction dans le délégué de `AddOptions<AuthorizationOptions>().Configure(...)`, qui devient un bloc `{ ... }` si ce n'est pas déjà le cas.

**5. Protéger un endpoint.** Appliquez la policy à la route concernée :

**Fichier :** `src/Contoso.Orders.Api/Controllers/AdminController.cs (exemple)`

```csharp
endpoints.MapGet("/editor/ping", () => TypedResults.Ok("pong"))
    .RequireAuthorization(Policies.RequireEditor);
```

**6. Les tests.** Les tests d'intégration fabriquent l'identité avec `AsUser` (voir [Tests](/Tests.md)). Ajoutez un fichier qui couvre les trois cas : le rôle, le refus et, pour un projet avec groupes, l'appartenance au groupe (étape suivante).

**Fichier :** `tests/Contoso.Orders.Api.IntegrationTests/Editors/EditorPolicyTests.cs`

```csharp
using System.Net;
using Contoso.Orders.Api.Authorization;
using Contoso.Orders.Api.IntegrationTests.Infrastructure;

namespace Contoso.Orders.Api.IntegrationTests.Editors;

public sealed class EditorPolicyTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Ping_WithEditorRole_Returns200()
    {
        var client = factory.CreateClient().AsUser("editor-by-role", roles: [AppRoles.Editor]);

        var response = await client.GetAsync("/editor/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ping_AsStandardUser_Returns403()
    {
        var client = factory.CreateClient().AsUser("standard-user", roles: [AppRoles.User]);

        var response = await client.GetAsync("/editor/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

### Ajouter un groupe

Cette section s'applique aux modèles `Groups` et `AppRolesAndGroups`.

**1. La propriété de configuration.** `GroupOptions` reçoit un GUID par groupe, lu dans la section `Authorization:Groups`.

**Fichier :** `src/Contoso.Orders.Api/Authorization/GroupOptions.cs`

```csharp
public sealed class GroupOptions
{
    public const string SectionName = "Authorization:Groups";

    public string Administrators { get; set; } = string.Empty;

    public string Editors { get; set; } = string.Empty;
}
```

**2. La validation au démarrage.** Dans `AddTemplateAuthorization`, ajoutez une règle à côté de celle des administrateurs. Avec `ValidateOnStart`, une valeur absente ou qui n'est pas un GUID empêche l'API de démarrer, avec un message qui nomme la clé : mieux vaut cet échec immédiat qu'un 403 inexplicable plus tard.

**Fichier :** `src/Contoso.Orders.Api/Authorization/AuthorizationSetup.cs`

```csharp
services.AddOptions<GroupOptions>()
    .Bind(configuration.GetSection(GroupOptions.SectionName))
    .Validate(options => Guid.TryParse(options.Administrators, out _),
        "Authorization:Groups:Administrators must be the object ID (GUID) of an Entra security group.")
    .Validate(options => Guid.TryParse(options.Editors, out _),
        "Authorization:Groups:Editors must be the object ID (GUID) of an Entra security group.")
    .ValidateOnStart();
```

**3. La policy.** Pour un groupe, la règle compare les GUID portés par le claim `groups` au GUID configuré. Le template fournit déjà la méthode `IsMemberOf` ; la policy de l'exemple accepte un administrateur, un éditeur (rôle) ou un membre du groupe `Editors` :

**Fichier :** `src/Contoso.Orders.Api/Authorization/AuthorizationSetup.cs`

```csharp
services.AddOptions<AuthorizationOptions>()
    .Configure<IServiceProvider>((options, serviceProvider) =>
    {
        options.AddPolicy(Policies.RequireAdmin, policy => ConfigureRequireAdmin(policy, serviceProvider));
        options.AddPolicy(Policies.RequireEditor, policy => ConfigureRequireEditor(policy, serviceProvider));
    });
```

**Fichier :** `src/Contoso.Orders.Api/Authorization/AuthorizationSetup.cs`

```csharp
private static void ConfigureRequireEditor(AuthorizationPolicyBuilder policy, IServiceProvider serviceProvider)
{
    var editorsGroupId = Guid.Parse(serviceProvider
        .GetRequiredService<IOptions<GroupOptions>>()
        .Value
        .Editors);
    policy.RequireAssertion(context =>
        context.User.IsInRole(AppRoles.Admin)
        || context.User.IsInRole(AppRoles.Editor)
        || IsMemberOf(context.User, editorsGroupId));
}
```

Dans un projet `Groups` seul, il n'y a pas de rôles : retirez les deux lignes `IsInRole`. Les GUID sont comparés en tant que GUID, pas en tant que texte : la casse ou des accolades n'ont pas d'importance.

**4. La configuration locale et déployée.** Une valeur dans chaque fichier de configuration. `appsettings.json` garde une valeur vide : elle sera fournie par l'environnement de déploiement (variable `Authorization__Groups__Editors`). `appsettings.Development.json` contient le GUID du groupe du realm local.

**Fichier :** `src/Contoso.Orders.Api/appsettings.json`

```json
"Authorization": {
  "Groups": {
    "Administrators": "",
    "Editors": ""
  }
},
```

**Fichier :** `src/Contoso.Orders.Api/appsettings.Development.json`

```json
"Authorization": {
  "Groups": {
    "Administrators": "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11",
    "Editors": "b2d7e9a1-3c54-4f68-8e0a-5d1f7c9b2a36"
  }
},
```

Choisissez un GUID neuf pour le groupe local (par exemple avec `[guid]::NewGuid()` en PowerShell ou `uuidgen` sous Linux et macOS).

**5. Le groupe dans le realm local.** Dans `template-realm.json`, ajoutez le groupe avec son GUID dans l'attribut `objectId`. Le mapper `entra-groups` met cet attribut dans le claim `groups` des membres.

**Fichier :** `src/Contoso.Orders.AppHost/Realms/template-realm.json`

```json
"groups": [
  {
    "name": "Administrators",
    "path": "/Administrators",
    "attributes": {
      "objectId": [ "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11" ]
    }
  },
  {
    "name": "Editors",
    "path": "/Editors",
    "attributes": {
      "objectId": [ "b2d7e9a1-3c54-4f68-8e0a-5d1f7c9b2a36" ]
    }
  }
],
```

**6. Le groupe dans Entra.** Créez un groupe de sécurité dans *Microsoft Entra ID > Groups*, ajoutez-y ses membres et relevez son **Object ID** : c'est la valeur de `Authorization__Groups__Editors`. Le claim `groups` doit déjà être activé sur l'inscription de l'API (*Token configuration > Add groups claim*, format *Group ID*) ; rappel de la limite de 200 groupes par token dans [Entra ID](/Identité/Entra-ID.md).

**7. Les tests : à ne pas oublier.** `ApiFactory` fournit les réglages de configuration des tests. Comme la validation est exécutée au démarrage, **tous** les tests d'intégration échouent avec `Authorization:Groups:Editors must be the object ID (GUID)...` tant que `ApiFactory` ne donne pas de valeur à la nouvelle clé :

**Fichier :** `tests/Contoso.Orders.Api.IntegrationTests/Infrastructure/ApiFactory.cs`

```csharp
public const string AdministratorsGroupId = "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11";
public const string EditorsGroupId = "b2d7e9a1-3c54-4f68-8e0a-5d1f7c9b2a36";
```

**Fichier :** `tests/Contoso.Orders.Api.IntegrationTests/Infrastructure/ApiFactory.cs (dans ConfigureWebHost)`

```csharp
builder.UseSetting("Authorization:Groups:Administrators", AdministratorsGroupId);
builder.UseSetting("Authorization:Groups:Editors", EditorsGroupId);
```

Puis un test pour l'appartenance au groupe, à ajouter à `EditorPolicyTests` :

**Fichier :** `tests/Contoso.Orders.Api.IntegrationTests/Editors/EditorPolicyTests.cs`

```csharp
[Fact]
public async Task Ping_AsMemberOfEditorsGroup_Returns200()
{
    var client = factory.CreateClient().AsUser("editor-by-group", groups: [ApiFactory.EditorsGroupId]);

    var response = await client.GetAsync("/editor/ping", TestContext.Current.CancellationToken);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

### Ajouter un persona

Un persona est un utilisateur du realm local. Ajoutez-le dans le tableau `users` de `template-realm.json` :

**Fichier :** `src/Contoso.Orders.AppHost/Realms/template-realm.json`

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

Chaque champ compte :

- `firstName` et `lastName` sont **obligatoires en pratique**. Sans eux, Keycloak considère le profil de l'utilisateur incomplet et exige une mise à jour du profil à la première connexion : le flux « password grant » utilisé par `get-token` échoue alors. Ils fournissent aussi le claim `name`.
- `emailVerified: true` et `"temporary": false` évitent d'autres actions requises à la connexion.
- `realmRoles` contient les **noms** des rôles du realm, `groups` les **chemins** des groupes (`/Editors`, avec la barre oblique).
- Omettez `realmRoles` ou `groups` pour un persona sans rôle ou sans groupe.

Le mot de passe `Passw0rd!` est public (écrit dans le dépôt) : il ne sert que pour ce realm local.

> 💡 **Pour un essai rapide**
>
> Vous pouvez aussi créer un utilisateur à la main dans la console d'administration de Keycloak (voir [Keycloak en local](/Identité/Keycloak-en-local.md)), mais il vit dans le volume et disparaît à la réinitialisation. Pour un persona partagé avec l'équipe, écrivez-le dans le fichier du realm.

### Réimporter le realm

Keycloak n'importe le realm qu'au premier démarrage d'un volume vide : modifier `template-realm.json` n'a **aucun effet** tant que le volume existe. Pour appliquer vos changements, réinitialisez les volumes, ce qui efface aussi la base SQL locale :

```bash
aspire stop --apphost src/Contoso.Orders.AppHost/Contoso.Orders.AppHost.csproj --force --volumes
```

Relancez ensuite avec `aspire start`. Pour ne réinitialiser que Keycloak, voir la méthode Docker de la page [Émulateurs](/Aspire/Émulateurs.md).

Vérifiez enfin le résultat en demandant un token pour le nouveau persona et en décodant ses claims (voir [Keycloak en local](/Identité/Keycloak-en-local.md)) :

**PowerShell**


```powershell
$token = ./scripts/get-token.ps1 -Persona claire
```

**Bash**


```bash
token=$(bash ./scripts/get-token.sh claire)
```

Le claim `roles` doit contenir `Editor` et le claim `groups` le GUID du groupe `Editors`.

### Avec le modèle Authenticated

Le modèle `Authenticated` ne définit ni rôle, ni groupe, ni policy : tout utilisateur connecté a les mêmes droits. Pour ajouter des droits différenciés, il faut d'abord changer de modèle, c'est-à-dire **régénérer** le projet avec une autre valeur de `--AuthorizationModel` : le modèle conditionne les claims, le realm local et la configuration, il ne se bascule pas par une option (voir [Modèles d'autorisation](/Identité/Modèles-d'autorisation.md)). Les personas, eux, s'ajoutent comme ci-dessus (sans `realmRoles` ni `groups`).

### Check-list finale

- [ ] La constante de rôle (`AppRoles`) a la même valeur que le rôle du realm et que l'app role Entra.
- [ ] Le rôle est déclaré dans `roles.realm` du realm local, et attribué au persona (`realmRoles`).
- [ ] L'app role est créé et attribué dans Entra.
- [ ] La policy est enregistrée dans `AuthorizationSetup` et appliquée à l'endpoint.
- [ ] Pour un groupe : propriété dans `GroupOptions`, règle de validation, valeur dans `appsettings.json` (vide) et `appsettings.Development.json` (GUID local).
- [ ] Le groupe local a un attribut `objectId` ; le groupe Entra a été créé et son *Object ID* fourni à l'environnement déployé.
- [ ] Le persona a `firstName`, `lastName`, `emailVerified` et un mot de passe non temporaire.
- [ ] `ApiFactory` fournit la nouvelle clé de configuration, et des tests couvrent le rôle, le groupe et le refus (403).
- [ ] Les volumes Keycloak ont été réinitialisés pour réimporter le realm.
- [ ] Le token d'un persona porte bien le rôle et le groupe attendus.

## À essayer

1. Ajoutez le rôle `Auditor` à un projet généré en suivant les étapes 1 à 6, avec un endpoint `GET /audit/ping`. Faites passer vos tests.
2. Ajoutez un persona `dave@local.test` qui n'a que le rôle `Auditor`. Obtenez son token et vérifiez qu'il reçoit 200 sur `/audit/ping` et 403 sur `/admin/ping`.
3. Retirez `firstName` et `lastName` du persona de la question 2, réinitialisez le volume et demandez un token. Quelle erreur obtenez-vous ?
4. Que se passe-t-il si vous ajoutez la propriété `Editors` à `GroupOptions` et sa règle de validation, mais pas la clé dans `appsettings.Development.json` ? Lancez les tests puis l'API pour le constater.

## Pour aller plus loin

- [Autorisation basée sur les policies dans ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authorization/policies)
- [Rôles d'application Microsoft Entra](https://learn.microsoft.com/entra/identity-platform/howto-add-app-roles-in-apps)
- [Importer un realm dans Keycloak](https://www.keycloak.org/server/importExport)
