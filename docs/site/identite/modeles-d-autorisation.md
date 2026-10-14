# Modèles d'autorisation

L'authentification établit **qui** appelle ; l'autorisation décide **ce qu'il a le droit de faire**. Le template propose quatre manières de répondre à la seconde question, à choisir une fois pour toutes au moment de générer le projet. Cette page les compare, explique comment le code les applique, comment protéger un nouvel endpoint, et comment tout cela est testé. Pour les notions de base (rôle, groupe, claim), voir [Identité](index.md).

## Le concept simplement

### Les quatre modèles

L'option `AuthorizationModel` du template (voir [Options du template](../reference/options-du-template.md)) détermine d'où viennent les droits d'un utilisateur :

| Modèle | Idée | Claim lu | Policy `RequireAdmin` |
|--------|------|----------|-----------------------|
| `Authenticated` | Tout utilisateur connecté a les mêmes droits | aucun | **n'existe pas** (pas d'endpoint `/admin/ping`) |
| `AppRoles` (défaut) | Les droits viennent des **app roles** attribués dans Entra | `roles` | le rôle `Admin` |
| `Groups` | Les droits viennent de l'**appartenance à un groupe** de sécurité | `groups` | être membre du groupe d'administrateurs |
| `AppRolesAndGroups` | Les deux sont acceptés | `roles` et `groups` | rôle `Admin` **ou** membre du groupe |

Comment choisir : les **app roles** sont définis par l'application et attribués dans l'annuaire, ce qui les rend précis et lisibles dans le code. Les **groupes** réutilisent une organisation qui existe déjà (par exemple « Équipe finance ») mais sont limités à 200 par token (voir [Entra ID](entra-id.md)). Si votre entreprise gère déjà les droits par groupes, prenez `Groups` ; sinon `AppRoles`. `AppRolesAndGroups` sert à migrer de l'un à l'autre. `Authenticated` convient à une application où il suffit d'être connecté.

### La politique par défaut : tout est protégé

Une **policy** (politique d'autorisation) est une règle nommée que l'on applique à un endpoint. Le template définit en plus une règle par défaut, la **fallback policy** : *tout endpoint qui ne déclare rien exige un utilisateur authentifié*. Un nouvel endpoint est donc privé tant que vous ne l'ouvrez pas explicitement (`AllowAnonymous`). C'est le bon réflexe de sécurité : on oublie d'ouvrir, pas de fermer.

## Pourquoi c'est important

- Un modèle d'autorisation choisi trop tard coûte cher : il conditionne les claims que le fournisseur doit émettre, le realm Keycloak local et la configuration Entra.
- La comparaison des groupes est un point classique de bug : un GUID peut s'écrire en majuscules, avec des accolades, avec des espaces. Le template compare des **GUID**, pas des chaînes.

## Comment le template le fait

### Les constantes

```csharp title="src/Web.Template.CQRS.Api/Authorization/AppRoles.cs"
--8<-- "src/Web.Template.CQRS.Api/Authorization/AppRoles.cs"
```

```csharp title="src/Web.Template.CQRS.Api/Authorization/Policies.cs"
--8<-- "src/Web.Template.CQRS.Api/Authorization/Policies.cs"
```

```csharp title="src/Web.Template.CQRS.Api/Authorization/GroupOptions.cs"
--8<-- "src/Web.Template.CQRS.Api/Authorization/GroupOptions.cs"
```

Les valeurs de `AppRoles` doivent correspondre exactement aux valeurs des rôles dans Entra et dans le realm Keycloak. `GroupOptions` reçoit le GUID du groupe d'administrateurs, lu dans la section de configuration `Authorization:Groups`.

### L'enregistrement des policies

```csharp title="src/Web.Template.CQRS.Api/Authorization/AuthorizationSetup.cs"
--8<-- "src/Web.Template.CQRS.Api/Authorization/AuthorizationSetup.cs" start="public static IServiceCollection AddTemplateAuthorization" end="        return services;"
```

!!! info "Directives de template"
    Ce fichier est un extrait de la **source du template** : les lignes `#if (UseGroups)`, `#if (UseAuthorizationPolicies)` et `#endif` sont des directives traitées lors de la génération. Dans un projet généré, seules les branches correspondant à vos options sont conservées, et les directives disparaissent.

Les directives s'appuient sur trois symboles calculés à partir de `AuthorizationModel` :

| Symbole | Vrai pour |
|---------|-----------|
| `UseAppRoles` | `AppRoles`, `AppRolesAndGroups` |
| `UseGroups` | `Groups`, `AppRolesAndGroups` |
| `UseAuthorizationPolicies` | tout sauf `Authenticated` |

Ce que cela donne, ligne par ligne :

- **`#if (UseGroups)` (premier bloc)** : enregistre `GroupOptions`, exige que `Authorization:Groups:Administrators` soit un GUID, et fait **échouer le démarrage** (`ValidateOnStart`) s'il ne l'est pas. Un groupe mal configuré se voit immédiatement, pas à la première requête d'un administrateur.
- **`AddAuthorization` avec `FallbackPolicy`** : la règle « utilisateur authentifié » qui s'applique à tous les endpoints sans déclaration.
- **`#if (UseAuthorizationPolicies)`** : déclare la policy nommée `RequireAdmin`, construite par `ConfigureRequireAdmin`. Avec le modèle `Authenticated`, ce bloc disparaît.

### `RequireAdmin` selon le modèle

```csharp title="src/Web.Template.CQRS.Api/Authorization/AuthorizationSetup.cs"
--8<-- "src/Web.Template.CQRS.Api/Authorization/AuthorizationSetup.cs" start="private static void ConfigureRequireAdmin" end="    }"
```

Les directives `#if` / `#elif` / `#else` choisissent **une seule** des trois formes selon le modèle :

| Modèle | Code conservé dans un projet généré | Effet |
|--------|-------------------------------------|-------|
| `AppRolesAndGroups` | `policy.RequireAssertion(… IsInRole(Admin) \|\| IsMemberOf(…))` | rôle `Admin` **ou** groupe |
| `AppRoles` | `policy.RequireRole(AppRoles.Admin)` | rôle `Admin` seulement |
| `Groups` | `policy.RequireAssertion(… IsMemberOf(…))` | groupe seulement |
| `Authenticated` | la méthode n'existe pas | pas de policy `RequireAdmin` |

`RequireRole` et `IsInRole` fonctionnent parce que l'authentification déclare `roles` comme claim des rôles (`RoleClaimType = "roles"`, voir [Entra ID](entra-id.md)). Sans cette déclaration, ASP.NET Core chercherait un autre nom de claim et aucun rôle ne serait reconnu.

### Pourquoi comparer des GUID plutôt que des chaînes

```csharp title="src/Web.Template.CQRS.Api/Authorization/AuthorizationSetup.cs"
--8<-- "src/Web.Template.CQRS.Api/Authorization/AuthorizationSetup.cs" start="private static bool IsMemberOf" end="id == groupObjectId);"
```

Un même groupe peut s'écrire `6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11`, `6F1C2A4E-0D3B-4C1E-9A57-2B8F0E6D4C11`, `{6f1c2a4e-…}` ou précédé d'un espace dans une variable d'environnement mal recopiée. Une comparaison de chaînes classerait ces écritures comme différentes et refuserait l'accès à un administrateur légitime, sans message explicite. Le template analyse les deux côtés avec `Guid.TryParse` (qui accepte la casse, les accolades et les espaces de bord) et compare les **valeurs**. Un claim qui n'est pas un GUID valide est ignoré. Les tests décrits plus bas couvrent la casse, les accolades et les espaces.

### Protéger un endpoint

L'endpoint `/admin/ping` est l'exemple fourni :

```csharp title="src/Web.Template.CQRS.Api/Controllers/AdminController.cs"
--8<-- "src/Web.Template.CQRS.Api/Controllers/AdminController.cs" start="var pingEndpoint = endpoints.MapGet" end=".RequireAuthorization(Policies.RequireAdmin);"
```

Trois niveaux de protection, du plus ouvert au plus fermé :

| Souhait | Comment |
|---------|---------|
| Ouvert à tous, sans token | `.AllowAnonymous()` |
| Utilisateur connecté (comportement par défaut) | rien à ajouter : la fallback policy s'applique |
| Réservé aux administrateurs | `.RequireAuthorization(Policies.RequireAdmin)` |

Pour un nouveau rôle ou une nouvelle policy, voir le [guide pour ajouter un rôle, un groupe ou un persona](../guides/ajouter-role-groupe-persona.md). Côté code applicatif, l'utilisateur courant se lit via `ICurrentUser` (identifiant, nom, email, rôles, groupes), voir [Entra ID](entra-id.md).

### Comment c'est testé

Les tests d'intégration ne contactent ni Entra ni Keycloak. L'`ApiFactory` remplace le schéma d'authentification par un `TestAuthHandler` qui fabrique un utilisateur à partir d'en-têtes de requête :

```csharp title="tests/Web.Template.CQRS.Api.IntegrationTests/Infrastructure/TestAuthHandler.cs"
--8<-- "tests/Web.Template.CQRS.Api.IntegrationTests/Infrastructure/TestAuthHandler.cs" start="protected override Task<AuthenticateResult> HandleAuthenticateAsync" end="return Task.FromResult(AuthenticateResult.Success(ticket));"
```

Sans en-tête `X-Test-User`, l'authentification ne renvoie aucun résultat : la requête est anonyme, donc 401 sur un endpoint protégé. Avec l'en-tête, l'utilisateur de test porte les claims `oid`, `sub`, `name`, `email`, plus les `roles` et `groups` des en-têtes `X-Test-Roles` et `X-Test-Groups`. Les tests restent ainsi rapides, déterministes et indépendants du réseau, tout en passant par la **vraie** policy `RequireAdmin`.

```csharp title="tests/Web.Template.CQRS.Api.IntegrationTests/Admin/AdminEndpointTests.cs"
--8<-- "tests/Web.Template.CQRS.Api.IntegrationTests/Admin/AdminEndpointTests.cs" start="public async Task Ping_WithoutAuthentication_Returns401" end="Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);"
```

Les tests suivants du fichier (compilés selon le modèle choisi) vérifient le rôle `Admin` (200), l'appartenance au groupe (200), un GUID en majuscules (200), un GUID configuré avec des accolades ou des espaces (200), et les cas inverses où seul l'un des deux mécanismes est présent dans un modèle qui n'accepte que l'autre (403). Ce que ces tests ne vérifient pas : le contenu réel d'un token Entra ou Keycloak, et la bonne configuration du fournisseur. Ces points se contrôlent avec un vrai token (voir [Keycloak en local](keycloak-local.md)). Pour l'organisation générale des tests, voir la section [Tests](../tests/index.md).

## À essayer

1. Dans `AuthorizationSetup.cs`, repérez les trois formes de `RequireAdmin` et associez chacune à un modèle du tableau.
2. Lancez l'AppHost et appelez `/admin/ping` avec le token de `admin` (200) puis celui de `user` (403) puis sans token (401).
3. Dans un projet généré avec le modèle `Groups`, mettez dans `Authorization:Groups:Administrators` le GUID en majuscules : l'administrateur local est-il toujours reconnu ? Pourquoi ?
4. Mettez une valeur qui n'est pas un GUID dans cette même clé : que se passe-t-il au démarrage ?
