# Fichiers par opération et par modèle

## Reconnaître le modèle

| Modèle | `AppRoles.cs` | `GroupOptions.cs` | `Policies.cs` / `AuthorizationSetup.cs` (policies) | Realm (`Realms/template-realm.json`) |
|--------|:---:|:---:|:---:|---|
| `AppRoles` | oui | non | oui | `roles.realm` : `Admin`, `User` ; utilisateurs avec `realmRoles` ; mapper `entra-roles` |
| `Groups` | non | oui | oui | pas de rôles ; `groups` avec `objectId` ; utilisateurs avec `groups` ; mapper `entra-groups` |
| `AppRolesAndGroups` | oui | oui | oui | rôles + groupes ; mappers `entra-roles` et `entra-groups` |
| `Authenticated` | non | non | non (`RequireAuthorization()` seul) | ni rôles ni groupes ; utilisateurs sans `realmRoles` ni `groups` |

Realm fourni par défaut : `admin@local.test` (Alice Admin, rôle `Admin` et/ou groupe `/Administrators`) et `user@local.test` (Bob User, rôle `User` ou aucun droit). Le groupe local `Administrators` a le GUID `6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11`. Les mappers `entra-roles` et `entra-groups` mettent tous les rôles du realm de l'utilisateur dans `roles` et l'attribut `objectId` de ses groupes dans `groups` : il n'y a rien d'autre à déclarer.

Tout projet sans Aspire n'a ni `AppHost/` ni realm : sautez les lignes « realm » ci-dessous.

## Tableau opération x fichiers

| Opération | Fichiers |
|-----------|----------|
| Ajouter un rôle (`AppRoles`, `AppRolesAndGroups`) | `src/Web.Template.CQRS.Api/Authorization/AppRoles.cs`, `Policies.cs`, `AuthorizationSetup.cs` (si le rôle a sa policy), l'endpoint concerné, `Realms/template-realm.json` (`roles.realm`), tests, `project-profile.json` |
| Ajouter un groupe (`Groups`, `AppRolesAndGroups`) | `GroupOptions.cs`, `AuthorizationSetup.cs` (validation + policy), `src/Web.Template.CQRS.Api/appsettings.json` (vide), `appsettings.Development.json` (GUID local), `Realms/template-realm.json` (`groups`), `tests/Web.Template.CQRS.Api.IntegrationTests/Infrastructure/ApiFactory.cs`, tests, `project-profile.json` |
| Ajouter un persona | `Realms/template-realm.json` (`users`), `project-profile.json`. Aucun code. |
| Renommer / retirer un rôle | `AppRoles.cs`, `AuthorizationSetup.cs`, endpoints et tests qui le citent, realm (`roles.realm` et `realmRoles` des utilisateurs), profil |
| Renommer / retirer un groupe | `GroupOptions.cs` (propriété), `AuthorizationSetup.cs`, `appsettings.json`, `appsettings.Development.json`, `ApiFactory.cs`, realm (`groups` et `groups` des utilisateurs), tests, profil. Une clé de configuration restée dans `appsettings` sans propriété est ignorée sans erreur : retirez-la quand même. |
| Retirer / modifier un persona | `Realms/template-realm.json`, profil |

Dans `AppRolesAndGroups`, la policy d'un rôle ou d'un groupe accepte en général les deux voies (rôle OU groupe), comme `RequireAdmin`.

## Code attendu

Les extraits ci-dessous prennent l'exemple du rôle `Editor` et du groupe `Editors`. Adaptez les noms.

### Constante de rôle (`AppRoles.cs`)

La valeur doit être identique, caractère pour caractère, au rôle du realm et à l'app role Entra.

```csharp
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string Editor = "Editor";
}
```

### Policy (`Policies.cs`)

```csharp
public static class Policies
{
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireEditor = "RequireEditor";
}
```

### Propriété de groupe (`GroupOptions.cs`)

```csharp
public sealed class GroupOptions
{
    public const string SectionName = "Authorization:Groups";

    public string Administrators { get; set; } = string.Empty;

    public string Editors { get; set; } = string.Empty;
}
```

### Validation au démarrage (`AuthorizationSetup.cs`, `AddTemplateAuthorization`)

Une règle `Validate` par groupe, avant `.ValidateOnStart()`. Une valeur absente ou qui n'est pas un GUID empêche l'API de démarrer, avec un message qui nomme la clé.

```csharp
services.AddOptions<GroupOptions>()
    .Bind(configuration.GetSection(GroupOptions.SectionName))
    .Validate(options => Guid.TryParse(options.Administrators, out _),
        "Authorization:Groups:Administrators must be the object ID (GUID) of an Entra security group.")
    .Validate(options => Guid.TryParse(options.Editors, out _),
        "Authorization:Groups:Editors must be the object ID (GUID) of an Entra security group.")
    .ValidateOnStart();
```

### Enregistrement de la policy

Le projet généré enregistre `RequireAdmin` par une expression : `.Configure<IServiceProvider>((options, serviceProvider) => options.AddPolicy(...))`. Pour ajouter une seconde policy, transformez le délégué en bloc :

```csharp
services.AddOptions<AuthorizationOptions>()
    .Configure<IServiceProvider>((options, serviceProvider) =>
    {
        options.AddPolicy(Policies.RequireAdmin, policy => ConfigureRequireAdmin(policy, serviceProvider));
        options.AddPolicy(Policies.RequireEditor, policy => ConfigureRequireEditor(policy, serviceProvider));
    });
```

**Modèle `AppRoles`** : pas besoin de méthode dédiée.

```csharp
options.AddPolicy(Policies.RequireEditor, policy =>
    policy.RequireRole(AppRoles.Editor, AppRoles.Admin));
```

`RequireRole` avec plusieurs valeurs accepte l'utilisateur qui en possède au moins une.

**Modèles `Groups` / `AppRolesAndGroups`** : une méthode à côté de `ConfigureRequireAdmin`, qui réutilise `IsMemberOf` (déjà présent). Les GUID sont comparés en tant que GUID, la casse n'importe pas.

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

Dans `Groups` seul, retirez les deux lignes `IsInRole` (il n'y a pas de rôles). Le fichier du projet généré est déjà résolu (plus de directives `#if`) : n'ajoutez aucune directive.

### Appliquer la policy à un endpoint

Sur la route concernée, dans la méthode `Map*Endpoints` de la ressource :

```csharp
endpoints.MapGet("/editor/ping", () => TypedResults.Ok("pong"))
    .RequireAuthorization(Policies.RequireEditor);
```

Si le projet a le rate limiting (`RateLimitingPolicies` existe), ajoutez aussi `.RequireRateLimiting(RateLimitingPolicies.Read)` (ou `Write`).

### Configuration

`src/Web.Template.CQRS.Api/appsettings.json` : valeur **vide** (fournie par l'environnement de déploiement : `Authorization__Groups__Editors`).

```json
"Authorization": {
  "Groups": {
    "Administrators": "",
    "Editors": ""
  }
},
```

`src/Web.Template.CQRS.Api/appsettings.Development.json` : GUID **local**, le même que l'attribut `objectId` du groupe dans le realm. Générez-le : `python -c "import uuid; print(uuid.uuid4())"` (ou `uuidgen`, ou `[guid]::NewGuid()` en PowerShell). Ne réutilisez pas `6f1c2a4e-...` (réservé à `Administrators`).

```json
"Authorization": {
  "Groups": {
    "Administrators": "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11",
    "Editors": "b2d7e9a1-3c54-4f68-8e0a-5d1f7c9b2a36"
  }
},
```

### Tests

`tests/Web.Template.CQRS.Api.IntegrationTests/Infrastructure/ApiFactory.cs` : constante et réglage de configuration. Sans eux, la validation au démarrage fait échouer **tous** les tests d'intégration.

```csharp
public const string AdministratorsGroupId = "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11";
public const string EditorsGroupId = "b2d7e9a1-3c54-4f68-8e0a-5d1f7c9b2a36";
```

```csharp
// dans ConfigureWebHost
builder.UseSetting("Authorization:Groups:Administrators", AdministratorsGroupId);
builder.UseSetting("Authorization:Groups:Editors", EditorsGroupId);
```

Fichier de tests de policy (modèle `AppRolesAndGroups` ; retirez le test de groupe sans groupes, et ceux de rôle sans rôles) :

```csharp
using System.Net;
using Web.Template.CQRS.Api.Authorization;
using Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

namespace Web.Template.CQRS.Api.IntegrationTests.Editors;

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
    public async Task Ping_AsMemberOfEditorsGroup_Returns200()
    {
        var client = factory.CreateClient().AsUser("editor-by-group", groups: [ApiFactory.EditorsGroupId]);
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

    [Fact]
    public async Task Ping_WithoutAuthentication_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/editor/ping", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

Dans un projet `Groups` seul, `AsUser("standard-user")` sans paramètre (pas de `AppRoles`). Vérifiez la signature de `AsUser` dans `tests/Web.Template.CQRS.Api.IntegrationTests/Infrastructure/` et imitez `tests/Web.Template.CQRS.Api.IntegrationTests/Admin/AdminEndpointTests.cs`.

Un test qui ne vise que le persona (ajout d'un utilisateur au realm sans nouveau droit) n'a pas de test unitaire à ajouter.
