# Paramètres et secrets

Cette page explique comment l'AppHost reçoit ses mots de passe sans qu'ils soient jamais écrits dans le dépôt.

## Le concept simplement

Un **secret** est une valeur qui donne accès à quelque chose : mot de passe de base de données, clé d'API, chaîne de connexion. Règle d'or : un secret ne se commite jamais. Un dépôt Git garde tout l'historique et sa copie circule (clones, CI, forks) : un mot de passe commité doit être considéré comme **compromis**, même supprimé au commit suivant.

Les **User Secrets** de .NET sont la solution pour le développement local. C'est un fichier JSON stocké **dans votre profil utilisateur**, hors du dépôt, associé à un projet par un identifiant (`UserSecretsId`). Le code les lit comme n'importe quelle autre configuration, mais le fichier ne peut pas être commité : il n'est pas dans le dossier du projet.

Un **paramètre Aspire** est une valeur que l'AppHost attend de la configuration. Quand il est déclaré `secret: true`, Aspire le traite comme sensible.

## Pourquoi c'est important

- Aucun mot de passe de conteneur local dans le dépôt, ni dans le code, ni dans un fichier `appsettings`.
- Chaque développeur choisit les siens.
- Le dépôt reste partageable sans fuite.

!!! warning "Local seulement"
    Les User Secrets sont une commodité de **développement**. Ils ne sont pas chiffrés et ne servent pas en production : une application déployée reçoit ses secrets d'une autre source (voir [Local et déployé](local-vs-deploye.md)).

## Comment le template le fait

### Déclarer les paramètres

L'AppHost déclare deux paramètres secrets :

```csharp title="src/Web.Template.CQRS.AppHost/Program.cs"
--8<-- "src/Web.Template.CQRS.AppHost/Program.cs" start="var sqlPassword" end="var sqlPassword"
```

```csharp title="src/Web.Template.CQRS.AppHost/Program.cs"
--8<-- "src/Web.Template.CQRS.AppHost/Program.cs" start="var keycloakAdminPassword" end="var keycloakAdminPassword"
```

Un paramètre nommé `sql-password` est lu dans la configuration sous la clé `Parameters:sql-password` (le préfixe `Parameters:` est ajouté par Aspire). Si la valeur est absente, les ressources qui en dépendent ne peuvent pas démarrer.

### Le lien avec le projet : `UserSecretsId`

```xml title="src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj"
--8<-- "src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj" start="<UserSecretsId>" end="<UserSecretsId>"
```

Cet identifiant (un GUID) désigne le fichier de secrets de l'AppHost. L'API possède **le sien** dans son `.csproj`, utile quand vous la lancez sans Aspire (par exemple pour renseigner `Auth:*` et les chaînes de connexion), voir [Configuration](../reference/configuration.md).

```xml title="src/Web.Template.CQRS.Api/Web.Template.CQRS.Api.csproj"
--8<-- "src/Web.Template.CQRS.Api/Web.Template.CQRS.Api.csproj" start="<UserSecretsId>" end="<UserSecretsId>"
```

!!! info "Dans un projet généré"
    Chaque projet généré reçoit ses propres `UserSecretsId`, différents de ceux du dépôt du template.

### Où est le fichier ?

| Système | Emplacement |
|---------|-------------|
| Windows | `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json` |
| Linux et macOS | `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json` |

Ce fichier est en clair : protégez votre session, et ne le copiez pas dans le dépôt.

### Les commandes

Avec le CLI .NET, en visant le projet de l'AppHost :

```bash
# définir (ou remplacer) un secret
dotnet user-secrets set "Parameters:sql-password" "Choose-A-Strong-Local-Password!" --project src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj

# lister les clés seulement (la sortie brute affiche aussi les valeurs : ne la copiez pas)
dotnet user-secrets list --project src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj | sed 's/ = .*//'
# PowerShell : dotnet user-secrets list --project src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj | ForEach-Object { ($_ -split ' = ')[0] }

# supprimer un secret
dotnet user-secrets remove "Parameters:sql-password" --project src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj
```

Le CLI Aspire a des commandes équivalentes pour les secrets de l'AppHost : `aspire secret set <clé> <valeur>`, `aspire secret list`, `aspire secret get <clé>`, `aspire secret delete <clé>` et `aspire secret path` (affiche l'emplacement du fichier).

!!! warning "Choisissez des mots de passe acceptés par SQL Server"
    SQL Server refuse les mots de passe faibles (longueur minimale, mélange de majuscules, minuscules et chiffres). Un mot de passe trop simple fait échouer le démarrage du conteneur.

!!! danger "Pas de secret réel dans la documentation ni dans le dépôt"
    Les valeurs de cette page sont des exemples locaux. N'y mettez jamais un secret qui protège autre chose que votre poste.

## À essayer

1. Exécutez `aspire secret path --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj` et repérez le dossier correspondant à l'identifiant de l'AppHost.
2. Définissez les deux secrets, puis `dotnet user-secrets list --project … | sed 's/ = .*//'` (clés seulement) : quelles clés voyez-vous ?
3. Supprimez `Parameters:sql-password` et lancez l'application : que se passe-t-il ? Rétablissez ensuite le secret.
4. Question : pourquoi un secret supprimé dans un commit ultérieur reste-t-il un problème s'il a été commité ?

## Pour aller plus loin

- [Secrets utilisateur ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/app-secrets)
