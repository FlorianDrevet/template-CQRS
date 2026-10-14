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

!!! note "Pourquoi des symboles calculés ?"
    Plusieurs choix d'autorisation partagent le même comportement. Plutôt que de répéter `AuthorizationModel == "Groups" || AuthorizationModel == "AppRolesAndGroups"` partout, le template le calcule une fois dans `UseGroups`.

Le fichier complet est reproduit ci-dessous (dépliez l'encadré). Les symboles sont dans le bloc `symbols`, les règles d'exclusion et de renommage dans `sources`, puis `modifiers`. Les sections suivantes y renvoient.

??? example "template.json en entier"
    ```json title=".template.config/template.json"
    --8<-- ".template.config/template.json"
    ```

### Les directives `#if` dans le code

Les fichiers C# contiennent des directives de préprocesseur `#if (Symbole)` ... `#endif`. À la génération, le SDK évalue la condition : si elle est fausse, tout le bloc est supprimé, directives comprises. Le dépôt source compile aussi tel quel, car `Directory.Build.props` définit tous les symboles comme constantes de compilation : le dépôt représente la variante la plus complète.

Selon le type de fichier, la syntaxe change :

| Type de fichier | Syntaxe |
|-----------------|---------|
| C# | `#if (EnableAspire)` ... `#endif` |
| Markdown, XML, HTML | `<!--#if (EnableAspire) -->` ... `<!--#endif -->` |
| JSON, JavaScript | `//#if (EnableAspire)` ... `//#endif` |

Voici un extrait réel du point d'entrée de l'API :

```csharp title="src/Web.Template.CQRS.Api/Program.cs"
--8<-- "src/Web.Template.CQRS.Api/Program.cs" start="builder.Services.AddInfrastructure(builder.Configuration);" end="var app = builder.Build();"
```

!!! info "Le code du dépôt est la source du template"
    Dans un projet généré, seules les branches correspondant à vos options sont conservées. Avec `--EnableRateLimiting false`, le bloc `#if (EnableRateLimiting)` et son contenu disparaissent entièrement.

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

Passez ensuite aux [Prérequis](prerequis.md).

## Pour aller plus loin

- [Custom templates for dotnet new](https://learn.microsoft.com/dotnet/core/tools/custom-templates)
- [Référence de template.json](https://github.com/dotnet/templating/wiki/Reference-for-template.json)
