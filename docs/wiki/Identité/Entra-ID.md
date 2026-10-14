# Entra ID

Microsoft Entra ID est le fournisseur d'identité **cible** du template : c'est lui qui authentifie les utilisateurs de l'application déployée. Cette page liste ce qu'il faut créer dans Entra (deux App Registrations, des app roles, éventuellement un groupe), puis montre comment l'API les consomme. Pour le cours sur les notions (JWT, audience, scopes), voir [Identité](/Identité.md) ; pour la version locale, voir [Keycloak en local](/Identité/Keycloak-en-local.md).

> ℹ️ **Cette page décrit des étapes dans le portail Azure**
>
> Les écrans du portail évoluent. Les noms de menus ci-dessous sont ceux de la documentation Microsoft au moment de la rédaction ; en cas de doute, la rubrique « Pour aller plus loin » renvoie aux pages officielles.

## Le concept simplement

Dans Entra, une application se déclare par une **App Registration** (« inscription d'application »). C'est la carte d'identité de l'application dans votre tenant : son identifiant (*Application (client) ID*), ses URL de retour, les rôles qu'elle définit, les permissions qu'elle expose. Pour ce template, il y a **deux** inscriptions, parce qu'il y a deux acteurs :

| Inscription | Représente | Sert à |
|-------------|------------|--------|
| **API** | `Web.Template.CQRS.Api`, la ressource protégée | Définir l'audience des tokens, les app roles et le scope exposé |
| **Client** (Scalar, puis votre frontend) | L'application qui appelle l'API pour l'utilisateur | Déclarer l'URL de retour (*redirect URI*) du flux Authorization Code avec PKCE |

À côté de l'App Registration existe l'**Enterprise application** (le *service principal* de l'API dans votre tenant) : c'est là que l'on **attribue** les app roles aux utilisateurs et groupes. Retenez : *App Registration = définition ; Enterprise application = attribution.*

## Pourquoi c'est important

- Chaque valeur copiée au mauvais endroit se traduit par un 401 (audience ou issuer incorrects) ou un 403 (rôle ou groupe manquant dans le token), jamais par une erreur explicite côté utilisateur.
- L'API démarre en **échec immédiat** si une clé obligatoire manque : mieux vaut savoir quelles clés viennent de quelle inscription.

## Comment le template le fait

### 1. L'inscription de l'API

Dans le portail Azure : *Microsoft Entra ID > App registrations > New registration*. Créez une application nommée par exemple `contoso-orders-api`, comptes de ce répertoire uniquement. Relevez :

- **Application (client) ID** : valeur de `Auth:ClientId`.
- **Directory (tenant) ID** : valeur de `Auth:TenantId`.

Puis *Expose an API* :

1. Définissez l'**Application ID URI**. Par défaut, le portail propose `api://<client-id>`.
2. Ajoutez un scope délégué, par exemple `access_as_user`. Son nom complet (`api://<client-id>/access_as_user`) est la valeur de `Auth:OpenApiScope`, que Scalar demandera.

L'**audience** (`aud`) des tokens destinés à l'API est cet Application ID URI (ou le client ID selon la version du token). `Auth:Audience` doit lui correspondre quand vous le renseignez : l'API refuse (401) un token dont l'audience ne correspond pas. Avec Microsoft.Identity.Web, si `Audience` est vide, l'audience attendue par défaut est `api://<ClientId>` (selon la version du token, le `ClientId` seul peut aussi être accepté ; ne vous y fiez pas).

> ⚠️ **Personnalisez `Auth:Audience` avec prudence**
>
> Renseigner une audience personnalisée peut modifier les valeurs de `aud` acceptées. Gardez de préférence la valeur par défaut `api://<ClientId>` (laissez `Auth:Audience` vide), ou faites-la correspondre exactement au `aud` des tokens que vous décodez.

### 2. Les app roles

Les app roles sont définis dans l'inscription de l'API : *App roles > Create app role*, avec *Allowed member types* = *Users/Groups*. La **valeur** de chaque rôle est ce qui apparaît dans le claim `roles`, et elle doit être égale, caractère pour caractère, aux constantes du code :

**Fichier :** `src/Web.Template.CQRS.Api/Authorization/AppRoles.cs`

```csharp
namespace Web.Template.CQRS.Api.Authorization;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
}
```

Créez donc deux rôles, de valeurs `Admin` et `User`. Ils n'ont d'effet qu'une fois **attribués** : *Enterprise applications > votre API > Users and groups > Add user/group*, puis choisissez l'utilisateur (ou un groupe) et le rôle. Au prochain token de cet utilisateur, `roles` contiendra la valeur.

> 📝 **Modèle sans app roles**
>
> Si vous avez généré le projet avec le modèle `Authenticated` ou `Groups`, aucun app role n'est nécessaire : voir [Modèles d'autorisation](/Identité/Modèles-d'autorisation.md).

### 3. Le claim de groupes (modèles Groups et AppRolesAndGroups)

Pour que les tokens portent les groupes : dans l'inscription de l'API, *Token configuration > Add groups claim*, cochez **Security groups**, et pour les tokens d'accès choisissez le format **Group ID**. Le claim `groups` contient alors le GUID (*Object ID*) de chaque groupe de sécurité de l'utilisateur. Relevez le **Object ID** du groupe d'administrateurs dans *Microsoft Entra ID > Groups* : c'est la valeur de `Authorization:Groups:Administrators`.

> ⚠️ **Limite des 200 groupes**
>
> Un JWT Entra contient au plus **200 groupes**. Au-delà, Entra retire le claim `groups` et le remplace par une référence (`_claim_names` / `_claim_sources`) vers Microsoft Graph : l'utilisateur n'est alors plus reconnu comme membre, et l'accès est refusé. Le template ne gère pas ce cas. La parade recommandée par Microsoft pour les grands annuaires est l'option **Groups assigned to the application**, qui limite le claim aux groupes attribués à l'application, ou d'utiliser les app roles.

### 4. L'inscription cliente pour Scalar

Créez une seconde App Registration (par exemple `contoso-orders-scalar`) de type **Single-page application (SPA)** (et non *Web*), avec les *Redirect URIs* de l'endroit où Scalar est servi (en développement, `https://localhost:7246/scalar/v1` ou l'URL que votre navigateur affiche). Autorisez-la à appeler l'API : *API permissions > Add a permission > My APIs >* votre API *>* le scope `access_as_user`, puis accordez le consentement administrateur si nécessaire. Le type SPA est requis : Scalar exécute le flux Authorization Code avec PKCE depuis le navigateur, sans secret. La plateforme *Web*, elle, exige un *client secret* lors de l'échange du code, que Scalar ne peut pas fournir. Son **Application (client) ID** est la valeur de `Auth:OpenApiClientId`.

> ℹ️ **Scalar n'existe qu'en développement**
>
> Le bloc Scalar de `Program.cs` n'est enregistré qu'en environnement `Development`. `Auth:OpenApiClientId` et `Auth:OpenApiScope` ne servent donc que si vous utilisez Scalar contre un vrai tenant Entra, par exemple depuis votre poste. Voir [OpenAPI et Scalar](/Fonctionnalités/OpenAPI-et-Scalar.md).

### 5. Les clés `Auth:*` et leur validation au démarrage

La configuration est portée par une classe d'options :

**Fichier :** `src/Web.Template.CQRS.Api/Authentication/AuthOptions.cs`

```csharp
namespace Web.Template.CQRS.Api.Authentication;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public const string LocalKeycloakScalarClientId = "template-scalar";

    public AuthProvider Provider { get; set; } = AuthProvider.Entra;

    public string? Authority { get; set; }

    public string? Audience { get; set; }

    public string? Instance { get; set; }

    public string? TenantId { get; set; }

    public string? ClientId { get; set; }

    public string? OpenApiClientId { get; set; }

    public string? OpenApiScope { get; set; }
}
```

Et `appsettings.json` livre les valeurs par défaut, c'est-à-dire **Entra, avec des clés vides à remplir** :

**Fichier :** `src/Web.Template.CQRS.Api/appsettings.json`

```json
"Auth": {
  "Provider": "Entra",
  "Instance": "https://login.microsoftonline.com/",
  "TenantId": "",
  "ClientId": "",
  "Audience": "",
  "OpenApiClientId": "",
  "OpenApiScope": ""
},
```

| Clé | Origine dans Entra |
|-----|--------------------|
| `Auth:Provider` | `Entra` (valeur par défaut) |
| `Auth:Instance` | `https://login.microsoftonline.com/` (cloud public ; un autre cloud souverain a une autre URL) |
| `Auth:TenantId` | *Directory (tenant) ID* |
| `Auth:ClientId` | *Application (client) ID* de l'inscription de l'API |
| `Auth:Audience` | *Application ID URI* de l'API (laissé vide : valeur par défaut de Microsoft.Identity.Web) |
| `Auth:OpenApiClientId` | *Application (client) ID* de l'inscription cliente (Scalar) |
| `Auth:OpenApiScope` | Nom complet du scope exposé, `api://<client-id>/access_as_user` |

Le code d'enregistrement valide ces valeurs **au démarrage** (`ValidateOnStart`) :

**Fichier :** `src/Web.Template.CQRS.Api/Authentication/AuthenticationSetup.cs`

```csharp
services.AddOptions<AuthOptions>()
    .Bind(authSection)
    .Validate(options => Enum.IsDefined(options.Provider),
        "Auth:Provider must be Entra or Keycloak.")
    .Validate(options => options.Provider != AuthProvider.Entra
        || (!string.IsNullOrWhiteSpace(options.Instance)
            && !string.IsNullOrWhiteSpace(options.TenantId)
            && !string.IsNullOrWhiteSpace(options.ClientId)),
        "Auth:Instance, Auth:TenantId and Auth:ClientId are required for Entra.")
    .Validate(options => options.Provider != AuthProvider.Keycloak
        || (Uri.TryCreate(options.Authority, UriKind.Absolute, out var authority)
            && (authority.Scheme == Uri.UriSchemeHttps
                || (environment.IsDevelopment()
                    && authority.Scheme == Uri.UriSchemeHttp
                    && authority.IsLoopback))),
        "Auth:Authority must use HTTPS, except for loopback HTTP during development.")
    .Validate(options => options.Provider != AuthProvider.Keycloak
        || !string.IsNullOrWhiteSpace(options.Audience),
        "Auth:Audience is required for Keycloak.")
    .ValidateOnStart();
```

Pour Entra, `Instance`, `TenantId` et `ClientId` sont **obligatoires** : l'API refuse de démarrer sinon, avec le message `Auth:Instance, Auth:TenantId and Auth:ClientId are required for Entra.` C'est volontaire : une API qui démarrerait sans pouvoir valider de token rejetterait silencieusement toutes les requêtes. Vous voyez l'erreur dans les logs de démarrage (ou, avec Aspire, dans le [dashboard](/Aspire/Dashboard-et-CLI.md)).

### 6. Le choix du fournisseur

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

Avec `Entra`, l'API appelle `AddMicrosoftIdentityWebApi` : la bibliothèque **Microsoft.Identity.Web** lit la section `Auth` (`Instance`, `TenantId`, `ClientId`, `Audience`), en déduit l'issuer et l'adresse des clés de signature, et valide signature, issuer, audience et expiration. Avec `Keycloak`, c'est le middleware JWT Bearer standard, avec `Authority` et `Audience`. Dans les deux cas, `ConfigureCommonJwtOptions` désactive la transformation automatique des noms de claims (`MapInboundClaims = false`) et déclare `name` comme claim du nom et `roles` comme claim des rôles. C'est ce qui fait que `User.IsInRole("Admin")` fonctionne avec le claim `roles` tel qu'Entra l'émet.

### 7. Lire l'utilisateur courant

Le code applicatif ne manipule jamais les claims directement : il demande un `ICurrentUser`, implémenté dans l'API.

**Fichier :** `src/Web.Template.CQRS.Api/Authentication/HttpCurrentUser.cs`

```csharp
public string? Id => Principal?.FindFirst(ClaimNames.ObjectId)?.Value
    ?? Principal?.FindFirst(ClaimNames.Subject)?.Value
    ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

public string? DisplayName => Principal?.FindFirst(ClaimNames.Name)?.Value
    ?? Principal?.Identity?.Name;

public string? Email => Principal?.FindFirst(ClaimNames.Email)?.Value
    ?? Principal?.FindFirst("preferred_username")?.Value
    ?? Principal?.FindFirst("upn")?.Value;

public IReadOnlyList<string> Roles => ClaimValues(ClaimNames.Roles);

public IReadOnlyList<string> Groups => ClaimValues(ClaimNames.Groups);
```

L'identifiant privilégie `oid` (stable dans tout le tenant) puis `sub`. L'email se replie sur `preferred_username` ou `upn` : selon la configuration, un token Entra peut ne pas porter `email`.

## À essayer

1. Dans un projet généré avec Aspire, `appsettings.Development.json` force `Auth:Provider` à `Keycloak`. Pour voir la validation Entra, lancez l'API seule avec `Auth__Provider=Entra` (par exemple `$env:Auth__Provider="Entra"` en PowerShell) et `Auth:TenantId` vide : l'API s'arrête au démarrage avec le message fixe `Auth:Instance, Auth:TenantId and Auth:ClientId are required for Entra.`, qui ne dit pas laquelle des trois clés manque. Avec `ASPNETCORE_ENVIRONMENT=Production`, `appsettings.Development.json` n'est plus lu et `appsettings.json` (Entra, clés vides) s'applique : même échec.
2. Dans votre tenant, créez les deux inscriptions et remplissez les sept clés du tableau ; vérifiez dans [jwt.ms](https://jwt.ms) (avec un token de test, pas de production) que `aud` correspond à `Auth:Audience`.
3. Attribuez le rôle `Admin` à votre compte dans *Enterprise applications*, reconnectez-vous, et vérifiez qu'il apparaît dans `roles`.

Et pour la liste des valeurs locales et déployées côte à côte : [Configuration locale et déployée](/Identité/Configuration-locale-et-déployée.md).

## Pour aller plus loin

- [Inscrire une application (Microsoft Learn)](https://learn.microsoft.com/entra/identity-platform/quickstart-register-app)
- [Protéger une API web avec Microsoft.Identity.Web](https://learn.microsoft.com/entra/identity-platform/scenario-protected-web-api-overview)
- [Ajouter des app roles](https://learn.microsoft.com/entra/identity-platform/howto-add-app-roles-in-apps)
- [Claims optionnels et claim de groupes](https://learn.microsoft.com/entra/identity-platform/optional-claims)
