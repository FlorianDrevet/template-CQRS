# Comprendre les templates

## Le concept simplement

`dotnet new` est la commande du SDK .NET qui crée un projet à partir d'un modèle. Un modèle, appelé template, est simplement un dossier de fichiers accompagné d'un fichier de description `.template.config/template.json`. Quand vous lancez `dotnet new templatewebcqrs -n Contoso.Orders`, le SDK copie ces fichiers vers un nouveau dossier en les transformant :

- il remplace le nom du template par le nom que vous avez choisi ;
- il garde ou retire des morceaux de code selon les options que vous avez passées ;
- il exclut ou renomme certains fichiers selon ces mêmes options.

Le dépôt que vous lisez est donc à la fois un projet réel (il compile) et la matière première d'un générateur.

## Pourquoi c'est important

Vous allez lire du code qui contient des lignes comme `#if (EnableAspire)`. Dans un projet généré, ces lignes ont disparu. Si vous ne savez pas que le dépôt est une source de template, ce code paraît étrange, voire erroné. Comprendre le mécanisme vous permet aussi de modifier le template lui-même et de savoir pourquoi un fichier est absent d'un projet généré.

## Comment le template le fait

### Le nom de la source : `sourceName`

Le fichier `.template.config/template.json` déclare `"sourceName": "Web.Template.CQRS"`. Le SDK remplace cette chaîne, partout où elle apparaît (contenu des fichiers, noms de fichiers, noms de dossiers), par le nom du projet généré. Avec `-n Contoso.Orders`, le projet `Web.Template.CQRS.Api` devient `Contoso.Orders.Api`, le namespace `Web.Template.CQRS.Domain` devient `Contoso.Orders.Domain`, et ainsi de suite.

### Les symboles : `parameter` et `computed`

Un symbole est une variable du template. Il en existe deux familles ici :

- `parameter` : une valeur que vous choisissez en ligne de commande (`--EnableAspire false`), avec un type (`bool` ou `choice`) et une valeur par défaut ;
- `computed` : une valeur calculée à partir d'autres symboles, que vous ne saisissez jamais. `UseGroups` vaut par exemple vrai si le modèle d'autorisation est `Groups` ou `AppRolesAndGroups`.

> 📝 **Pourquoi des symboles calculés ?**
>
> Plusieurs choix d'autorisation partagent le même comportement. Plutôt que de répéter `AuthorizationModel == "Groups" || AuthorizationModel == "AppRolesAndGroups"` partout, le template le calcule une fois dans `UseGroups`.

Le fichier complet est reproduit ci-dessous (dépliez l'encadré). Les symboles sont dans le bloc `symbols`, les règles d'exclusion et de renommage dans `sources`, puis `modifiers`. Les sections suivantes y renvoient.

> 🧪 **template.json en entier**
>
> **Fichier :** `.template.config/template.json`
>
> ```json
> {
>   "$schema": "http://json.schemastore.org/template",
>   "author": "Florian Drevet",
>   "classifications": [
>     "Web"
>   ],
>   "name": "Web template using CQRS",
>   "description": "This template creates a new web application using CQRS",
>   "tags": {
>     "language": "C#",
>     "type": "project"
>   },
>   "shortName": "templatewebcqrs",
>   "sourceName": "Web.Template.CQRS",
>   "identity": "Web.Template.CQRS",
>   "preferNameDirectory": true,
>   "symbols": {
>     "EnableAspire": {
>       "type": "parameter",
>       "datatype": "bool",
>       "defaultValue": "true",
>       "description": "Ajoute Aspire, l'observabilité OpenTelemetry et les services locaux SQL Server, Azurite et Keycloak."
>     },
>     "EnableRateLimiting": {
>       "type": "parameter",
>       "datatype": "bool",
>       "defaultValue": "true",
>       "description": "Ajoute les politiques de limitation de débit de l'API."
>     },
>     "AuthorizationModel": {
>       "type": "parameter",
>       "datatype": "choice",
>       "defaultValue": "AppRoles",
>       "description": "Modèle d'autorisation Entra ID reproduit localement par Keycloak.",
>       "choices": [
>         {
>           "choice": "Authenticated",
>           "description": "Utilisateur connecté uniquement."
>         },
>         {
>           "choice": "AppRoles",
>           "description": "App roles Entra (claim roles)."
>         },
>         {
>           "choice": "Groups",
>           "description": "Groupes de sécurité Entra (claim groups)."
>         },
>         {
>           "choice": "AppRolesAndGroups",
>           "description": "App roles et groupes de sécurité."
>         }
>       ]
>     },
>     "EnableTests": {
>       "type": "parameter",
>       "datatype": "bool",
>       "defaultValue": "true",
>       "description": "Ajoute les projets de tests unitaires et d'intégration."
>     },
>     "UseAppRoles": {
>       "type": "computed",
>       "value": "(AuthorizationModel == \"AppRoles\" || AuthorizationModel == \"AppRolesAndGroups\")"
>     },
>     "UseGroups": {
>       "type": "computed",
>       "value": "(AuthorizationModel == \"Groups\" || AuthorizationModel == \"AppRolesAndGroups\")"
>     },
>     "UseAuthorizationPolicies": {
>       "type": "computed",
>       "value": "(AuthorizationModel != \"Authenticated\")"
>     }
>   },
>   "sources": [
>     {
>       "source": "./",
>       "target": "./",
>       "modifiers": [
>         {
>           "exclude": [
>             ".idea/**",
>             ".superpowers/**",
>             "eng/**",
>             "docs/**",
>             "site/**",
>             ".git/**",
>             ".vs/**",
>             ".cache/**",
>             ".pytest_cache/**",
>             "build/**",
>             "**/TestResults/**",
>             ".github/workflows/template-ci.yml",
>             ".github/workflows/docs.yml",
>             "Directory.Build.props",
>             "mkdocs.yml",
>             "README.md",
>             "AGENTS.md",
>             ".claude/skills/init-project/**"
>           ],
>           "rename": {
>             "README.generated.md": "README.md",
>             "AGENTS.generated.md": "AGENTS.md"
>           }
>         },
>         {
>           "condition": "(!EnableAspire)",
>           "exclude": [
>             "src/Web.Template.CQRS.AppHost/**",
>             "src/Web.Template.CQRS.ServiceDefaults/**",
>             "scripts/**",
>             "Web.Template.CQRS.slnx",
>             ".mcp.json",
>             ".vscode/mcp.json",
>             ".claude/skills/run-local/**"
>           ],
>           "rename": {
>             "Web.Template.CQRS.Core.slnx": "Web.Template.CQRS.slnx"
>           }
>         },
>         {
>           "condition": "(EnableAspire)",
>           "exclude": [
>             "Web.Template.CQRS.Core.slnx"
>           ]
>         },
>         {
>           "condition": "(!EnableRateLimiting)",
>           "exclude": [
>             "src/Web.Template.CQRS.Api/Common/RateLimiting/**",
>             "tests/Web.Template.CQRS.Api.IntegrationTests/RateLimiting/**"
>           ]
>         },
>         {
>           "condition": "(!EnableTests)",
>           "exclude": [
>             "tests/**"
>           ]
>         },
>         {
>           "condition": "(!UseAuthorizationPolicies)",
>           "exclude": [
>             "src/Web.Template.CQRS.Api/Authorization/Policies.cs",
>             "src/Web.Template.CQRS.Api/Controllers/AdminController.cs",
>             "src/Web.Template.CQRS.Application/Admin/**",
>             "src/Web.Template.CQRS.Contracts/Admin/**",
>             "tests/Web.Template.CQRS.Application.UnitTests/Admin/**",
>             "tests/Web.Template.CQRS.Api.IntegrationTests/Admin/**"
>           ]
>         },
>         {
>           "condition": "(!UseAppRoles)",
>           "exclude": [
>             "src/Web.Template.CQRS.Api/Authorization/AppRoles.cs"
>           ]
>         },
>         {
>           "condition": "(!UseGroups)",
>           "exclude": [
>             "src/Web.Template.CQRS.Api/Authorization/GroupOptions.cs",
>             "tests/Web.Template.CQRS.Api.IntegrationTests/Authorization/**"
>           ]
>         },
>         {
>           "condition": "(AuthorizationModel == \"Authenticated\")",
>           "exclude": [
>             "src/Web.Template.CQRS.AppHost/Realms/template-realm.json",
>             "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.approles.json",
>             "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.groups.json"
>           ],
>           "rename": {
>             "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.authenticated.json": "src/Web.Template.CQRS.AppHost/Realms/template-realm.json"
>           }
>         },
>         {
>           "condition": "(AuthorizationModel == \"AppRoles\")",
>           "exclude": [
>             "src/Web.Template.CQRS.AppHost/Realms/template-realm.json",
>             "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.authenticated.json",
>             "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.groups.json"
>           ],
>           "rename": {
>             "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.approles.json": "src/Web.Template.CQRS.AppHost/Realms/template-realm.json"
>           }
>         },
>         {
>           "condition": "(AuthorizationModel == \"Groups\")",
>           "exclude": [
>             "src/Web.Template.CQRS.AppHost/Realms/template-realm.json",
>             "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.authenticated.json",
>             "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.approles.json"
>           ],
>           "rename": {
>             "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.groups.json": "src/Web.Template.CQRS.AppHost/Realms/template-realm.json"
>           }
>         },
>         {
>           "condition": "(AuthorizationModel == \"AppRolesAndGroups\")",
>           "exclude": [
>             "src/Web.Template.CQRS.AppHost/RealmVariants/**"
>           ]
>         }
>       ]
>     }
>   ],
>   "guids": [
>     "bdab3963-4400-4b37-9c1d-7148b0a3a96c",
>     "fc205b91-e44d-4eb0-a0dd-8ad940659771"
>   ]
> }
> ```

### Les directives `#if` dans le code

Les fichiers C# contiennent des directives de préprocesseur `#if (Symbole)` ... `#endif`. À la génération, le SDK évalue la condition : si elle est fausse, tout le bloc est supprimé, directives comprises. Le dépôt source compile aussi tel quel, car `Directory.Build.props` définit tous les symboles comme constantes de compilation : le dépôt représente la variante la plus complète.

Selon le type de fichier, la syntaxe change :

| Type de fichier | Syntaxe |
|-----------------|---------|
| C# | `#if (EnableAspire)` ... `#endif` |
| Markdown, XML, HTML | `<!--#if (EnableAspire) -->` ... `<!--#endif -->` |
| JSON, JavaScript | `//#if (EnableAspire)` ... `//#endif` |

Voici un extrait réel du point d'entrée de l'API :

**Fichier :** `src/Web.Template.CQRS.Api/Program.cs`

```csharp
builder.Services.AddInfrastructure(builder.Configuration);

#if (EnableRateLimiting)
builder.Services.AddRateLimiting();
#endif

var app = builder.Build();
```

> ℹ️ **Le code du dépôt est la source du template**
>
> Dans un projet généré, seules les branches correspondant à vos options sont conservées. Avec `--EnableRateLimiting false`, le bloc `#if (EnableRateLimiting)` et son contenu disparaissent entièrement.

### Les `modifiers` : exclure et renommer des fichiers

Certaines options ne suppriment pas des lignes mais des fichiers entiers (un projet, un dossier). La section `sources` du `template.json` ci-dessus décrit des `modifiers` : une liste d'exclusions et de renommages, éventuellement soumis à une condition. Le premier modifier, sans condition, retire tout ce qui ne doit jamais être généré (documentation, outillage, `.git`) et renomme `README.generated.md` en `README.md`.

Dans le fichier ci-dessus, repérez le modifier dont la condition est `(!EnableAspire)` : si `EnableAspire` est faux, le projet AppHost, ServiceDefaults et les scripts sont exclus, et la solution allégée `Web.Template.CQRS.Core.slnx` prend la place de `Web.Template.CQRS.slnx`.

### Installer et désinstaller un template

| Action | Commande |
|--------|----------|
| Installer depuis un dossier local | `dotnet new install ./` (depuis la racine du dépôt) |
| Lister les templates installés | `dotnet new uninstall` (sans argument, affiche la liste et les commandes de désinstallation) |
| Désinstaller | `dotnet new uninstall <chemin du dossier>` |
| Générer | `dotnet new templatewebcqrs -n Contoso.Orders` |

Le nom court `templatewebcqrs` vient de `"shortName"` dans `template.json`.

## À essayer

1. Depuis la racine du dépôt, lancez `dotnet new install ./` puis `dotnet new templatewebcqrs --help`. Repérez dans l'aide la liste des options : retrouvez-les dans le bloc `symbols` ci-dessus.
2. Ouvrez `src/Web.Template.CQRS.Api/Controllers/AdminController.cs`. Combien de blocs `#if` contient-il ? Quelle option les contrôle ?
3. Dans `template.json`, trouvez quel modifier exclut le dossier `RateLimiting` et sous quelle condition.

Passez ensuite aux [Prérequis](/Démarrer/Prérequis.md).

## Pour aller plus loin

- [Custom templates for dotnet new](https://learn.microsoft.com/dotnet/core/tools/custom-templates)
- [Référence de template.json](https://github.com/dotnet/templating/wiki/Reference-for-template.json)
