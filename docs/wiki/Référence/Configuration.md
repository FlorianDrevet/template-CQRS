# Configuration

Cette page liste toutes les clés de configuration que lit le template : section, clé, type, valeur par défaut, caractère obligatoire, exemple et variable d'environnement équivalente. Pour les parcours expliqués (passer de Keycloak à Entra ID, préparer un déploiement), voir [Configuration locale et déployée](/Identité/Configuration-locale-et-déployée.md) et [Configurer un déploiement](/Guides/Configurer-un-déploiement.md).

## Comment la configuration est lue

ASP.NET Core assemble la configuration à partir de plusieurs sources ; la dernière qui définit une clé l'emporte.

| Ordre | Source | Usage typique |
|-------|--------|----------------|
| 1 | `appsettings.json` | valeurs de base du dépôt (Entra, valeurs vides à renseigner) |
| 2 | `appsettings.{Environment}.json` | surcharges locales (`appsettings.Development.json`) |
| 3 | User Secrets (environnement `Development` seulement) | secrets de poste, hors dépôt |
| 4 | variables d'environnement | configuration déployée, et valeurs injectées par l'AppHost Aspire |

Dans une variable d'environnement, le séparateur de niveau `:` s'écrit `__` (double souligné) : la clé `Auth:TenantId` devient `Auth__TenantId`. Un tableau s'écrit élément par élément avec un indice : `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`.

## Authentification : `Auth`

Section liée à la classe `AuthOptions`. Le fournisseur choisit le reste : `Entra` (valeur par défaut) pour un déploiement, `Keycloak` pour le développement local avec Aspire. Au démarrage, l'API valide ces options et refuse de démarrer si elles sont incohérentes.

| Clé | Type | Défaut du code | Obligatoire | Exemple | Variable d'environnement |
|-----|------|----------------|-------------|---------|--------------------------|
| `Auth:Provider` | `Entra` ou `Keycloak` | `Entra` | non | `Keycloak` | `Auth__Provider` |
| `Auth:Instance` | chaîne (URL) | aucun (`https://login.microsoftonline.com/` dans `appsettings.json`) | Entra | `https://login.microsoftonline.com/` | `Auth__Instance` |
| `Auth:TenantId` | chaîne (GUID) | aucun | Entra | `11111111-2222-3333-4444-555555555555` | `Auth__TenantId` |
| `Auth:ClientId` | chaîne (GUID) | aucun | Entra | `aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee` | `Auth__ClientId` |
| `Auth:Audience` | chaîne | aucun | Keycloak ; facultative avec Entra | `template-api` | `Auth__Audience` |
| `Auth:Authority` | chaîne (URL absolue) | aucun | Keycloak ; ignorée par Entra | `https://localhost:8080/realms/template` | `Auth__Authority` |
| `Auth:OpenApiClientId` | chaîne | aucun | non (Scalar avec Entra) | `aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee` | `Auth__OpenApiClientId` |
| `Auth:OpenApiScope` | chaîne | aucun | non (Scalar avec Entra) | `api://<client-id>/access_as_user` | `Auth__OpenApiScope` |

Règles de validation appliquées au démarrage (messages du code) :

- `Auth:Provider` doit être `Entra` ou `Keycloak`.
- Avec Entra : `Auth:Instance`, `Auth:TenantId` et `Auth:ClientId` sont requis.
- Avec Keycloak : `Auth:Audience` est requis, et `Auth:Authority` doit être une URL absolue en HTTPS (le HTTP n'est toléré que pour une adresse locale, en environnement `Development`). L'API exige aussi des métadonnées OpenID Connect en HTTPS hors `Development`.
- Avec Keycloak, le client Scalar est la constante `template-scalar` et les scopes demandés sont `openid`, `profile` et `email` ; `Auth:OpenApiClientId` et `Auth:OpenApiScope` ne servent qu'avec Entra.

## Groupes : `Authorization:Groups`

| Clé | Type | Défaut | Obligatoire | Exemple | Variable d'environnement |
|-----|------|--------|-------------|---------|--------------------------|
| `Authorization:Groups:Administrators` | GUID | chaîne vide | oui, si le modèle est `Groups` ou `AppRolesAndGroups` | `6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11` | `Authorization__Groups__Administrators` |

Cette clé n'existe que dans les projets générés avec le modèle `Groups` ou `AppRolesAndGroups` (symbole `UseGroups`). Une valeur vide ou qui n'est pas un GUID empêche l'API de démarrer (`Authorization:Groups:Administrators must be the object ID (GUID) of an Entra security group.`). Voir [Modèles d'autorisation](/Identité/Modèles-d'autorisation.md).

## CORS : `Cors`

| Clé | Type | Défaut | Obligatoire | Exemple | Variable d'environnement |
|-----|------|--------|-------------|---------|--------------------------|
| `Cors:AllowedOrigins` | tableau de chaînes (origines) | tableau vide (`appsettings.json`), `["http://localhost:4200"]` (`appsettings.Development.json`) | non | `["https://app.contoso.com"]` | `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ... |

Avec un tableau vide, la stratégie CORS n'autorise aucune origine précise. Voir [CORS](/Fonctionnalités/CORS.md).

## Données et stockage

| Clé | Type | Défaut | Obligatoire | Exemple | Variable d'environnement |
|-----|------|--------|-------------|---------|--------------------------|
| `ConnectionStrings:projectdb` | chaîne de connexion SQL Server | chaîne vide | oui | `Server=sql.contoso.net;Database=orders;User Id=api;Password=...;Encrypt=True` | `ConnectionStrings__projectdb` |
| `ConnectionStrings:blobs` | chaîne de connexion Azure Blob Storage | chaîne vide | oui | `DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...` | `ConnectionStrings__blobs` |
| `BlobSettings:ContainerName` | chaîne | `uploads` | non | `uploads` | `BlobSettings__ContainerName` |

Précisions :

- Avec Aspire, l'AppHost injecte `ConnectionStrings__projectdb` et `ConnectionStrings__blobs` : vous n'avez rien à écrire en local (voir [AppHost](/Aspire/AppHost.md)).
- Pour les blobs, si `ConnectionStrings:blobs` est absente, le code essaie ensuite `ConnectionStrings:AzureBlobStorageConnectionString`, puis une chaîne vide. Ce repli ne joue que si `ConnectionStrings:blobs` est **absente** : `appsettings.json` et `appsettings.Development.json` la définissent (vide, `""`), donc avec les fichiers livrés le repli ne se déclenche jamais.
- Ne mettez jamais un mot de passe dans un fichier versionné : utilisez les User Secrets en local (voir [Paramètres et secrets](/Aspire/Paramètres-et-secrets.md)) et un coffre ou des variables d'environnement en production.

## Journalisation : `Logging`

| Clé | Type | Défaut | Obligatoire | Exemple | Variable d'environnement |
|-----|------|--------|-------------|---------|--------------------------|
| `Logging:LogLevel:Default` | niveau (`Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`) | `Information` | non | `Warning` | `Logging__LogLevel__Default` |
| `Logging:LogLevel:Microsoft.AspNetCore` | niveau | `Warning` | non | `Information` | `Logging__LogLevel__Microsoft.AspNetCore` |

D'autres catégories de journaux se règlent de la même façon (`Logging:LogLevel:<Catégorie>`). Sur certains shells, un point dans un nom de variable d'environnement est refusé ; dans ce cas utilisez le fichier de configuration.

## Hôtes : `AllowedHosts`

| Clé | Type | Défaut | Obligatoire | Exemple | Variable d'environnement |
|-----|------|--------|-------------|---------|--------------------------|
| `AllowedHosts` | chaîne (liste séparée par des `;`) | `*` (`appsettings.json`) | non | `api.contoso.com` | `AllowedHosts` |

Clé standard d'ASP.NET Core qui restreint les noms d'hôte acceptés. Elle est dans `appsettings.json`, pas dans `appsettings.Development.json`.

## Télémétrie (projets avec Aspire)

Ces deux paramètres sont des **noms de clés complets**, sans section, lus par le projet `ServiceDefaults` (donc présents seulement avec `EnableAspire`). Ils ne sont écrits dans aucun fichier du dépôt : on les fournit par l'environnement.

| Clé | Type | Défaut | Obligatoire | Exemple | Effet |
|-----|------|--------|-------------|---------|-------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | URL | non définie | non | `http://localhost:4317` | si non vide, active l'export OpenTelemetry par OTLP ; Aspire la fournit automatiquement en local |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | chaîne de connexion | non définie | non | `InstrumentationKey=...;IngestionEndpoint=...` | si non vide, active l'export vers Azure Monitor (Application Insights) |

Les deux peuvent être actives en même temps. Voir [Observabilité](/Fonctionnalités/Observabilité.md).

**Fichier :** `src/Web.Template.CQRS.ServiceDefaults/Extensions.cs`

```csharp
var hasOtlpEndpoint = !string.IsNullOrWhiteSpace(
    builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
if (hasOtlpEndpoint)
{
    builder.Services.AddOpenTelemetry().UseOtlpExporter();
}

var applicationInsightsConnectionString =
    builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(applicationInsightsConnectionString))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}
```

## L'environnement : `ASPNETCORE_ENVIRONMENT`

Variable standard d'ASP.NET Core (`Development`, `Staging`, `Production`...). Elle pilote plusieurs comportements du template :

| Comportement | Condition |
|--------------|-----------|
| `/openapi/v1.json` et Scalar exposés | `Development` |
| `/health` et `/alive` exposés (avec Aspire) | `Development` ou `Testing` |
| HTTPS obligatoire pour les métadonnées OpenID Connect (Keycloak) | tout sauf `Development` |
| `appsettings.Development.json` chargé, User Secrets chargés | `Development` |

Les profils de `launchSettings.json` positionnent `Development` ; un déploiement doit positionner sa propre valeur.

## Les fichiers de configuration du dépôt

Ces fichiers contiennent des directives du template (`//#if (EnableAspire)`, `//#if (UseGroups)`) qui sont évaluées à la génération.

> ℹ️ **Directives dans les extraits**
>
> Dans un projet généré, seules les branches correspondant à vos options sont conservées : par exemple, sans groupes, la section `Authorization` n'existe pas, et sans Aspire la section `Auth` locale de `appsettings.Development.json` disparaît. Les commentaires `//#if` et `//#endif` n'apparaissent plus dans le fichier généré.

**Fichier :** `src/Web.Template.CQRS.Api/appsettings.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Auth": {
    "Provider": "Entra",
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "",
    "ClientId": "",
    "Audience": "",
    "OpenApiClientId": "",
    "OpenApiScope": ""
  },
  //#if (UseGroups)
  "Authorization": {
    "Groups": {
      "Administrators": ""
    }
  },
  //#endif
  "Cors": {
    "AllowedOrigins": []
  },
  "ConnectionStrings": {
    "projectdb": "",
    "blobs": ""
  },
  "BlobSettings": {
    "ContainerName": "uploads"
  },
  "AllowedHosts": "*"
}
```

**Fichier :** `src/Web.Template.CQRS.Api/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  //#if (EnableAspire)
  "Auth": {
    "Provider": "Keycloak",
    "Authority": "https://localhost:8080/realms/template",
    "Audience": "template-api"
  },
  //#if (UseGroups)
  "Authorization": {
    "Groups": {
      "Administrators": "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"
    }
  },
  //#endif
  //#endif
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:4200"
    ]
  },
  "ConnectionStrings": {
    "projectdb": "",
    "blobs": ""
  },
  "BlobSettings": {
    "ContainerName": "uploads"
  }
}
```

## À essayer

1. Dans un projet généré, lancez l'API seule avec `Auth__Provider=Keycloak` et `Auth__Audience=` (vide, explicitement, car `appsettings.Development.json` fournit sinon une valeur) : quel message d'erreur obtenez-vous au démarrage ?
2. Passez `Logging__LogLevel__Default` à `Debug` pour une exécution et observez la différence dans la console.
3. Retrouvez, dans le dashboard Aspire, la valeur effective de `ConnectionStrings__projectdb` injectée dans la ressource `api`.
