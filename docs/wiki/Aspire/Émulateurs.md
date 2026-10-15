# Émulateurs

Cette page est un petit cours : qu'est-ce qu'un émulateur, qu'est-ce qu'un conteneur et un volume, comment les données survivent (ou non) d'un lancement à l'autre, et comment tout remettre à zéro.

## Le concept simplement

### Émulateur ou service réel

En production, l'application parle à de vrais services : Azure SQL, Azure Blob Storage, Microsoft Entra ID. Les utiliser pour développer coûterait de l'argent, demanderait un accès réseau et des droits, et exposerait de vraies données. On utilise donc en local un **substitut** qui parle le même langage :

| Rôle | En local (Aspire) | Une fois déployé | Pourquoi ça marche |
|------|-------------------|------------------|--------------------|
| Base de données | **SQL Server** dans un conteneur | Azure SQL | même protocole et même dialecte SQL : seule la chaîne de connexion change |
| Stockage de fichiers | **Azurite**, émulateur d'Azure Blob Storage | Azure Blob Storage | même API : le SDK `Azure.Storage.Blobs` ne voit pas la différence |
| Identité | **Keycloak** | Microsoft Entra ID | même standard (OAuth2 / OpenID Connect) : l'API valide un token JWT dans les deux cas |

Notez la nuance : SQL Server en conteneur est **le vrai moteur SQL Server**, tandis qu'Azurite est une **réimplémentation** de l'API Azure Storage, et Keycloak est un **autre produit** qui reproduit le comportement d'Entra ID dont l'API a besoin (rôles, groupes, tokens). Plus le substitut est éloigné du service réel, plus il faut tester dans l'environnement déployé avant de conclure.

### Qu'est-ce qu'un conteneur ?

Un **conteneur** est un processus isolé qui embarque tout ce dont il a besoin (programme, bibliothèques, configuration) : on le démarre à partir d'une **image** téléchargée une fois. Docker est le programme qui les fait tourner sur votre poste. L'analogie est celle d'un appareil électroménager livré dans sa boîte, branché tel quel : vous n'installez rien de SQL Server sur Windows, vous « branchez » son conteneur.

Un conteneur est **jetable** : si on le supprime, tout ce qu'il contenait disparaît. D'où la notion de volume.

### Qu'est-ce qu'un volume ?

Un **volume** est un espace disque géré par Docker, **indépendant du conteneur**. On l'attache à un dossier du conteneur (par exemple celui où SQL Server écrit ses fichiers). Le conteneur peut être détruit et recréé : les données restent dans le volume.

## Pourquoi c'est important

- Sans volume, vous perdriez votre base locale et la configuration de Keycloak à chaque arrêt.
- Avec un volume, une donnée « fantôme » peut vous surprendre : une modification du realm Keycloak ou d'un script d'initialisation semble sans effet parce que l'ancien état est resté dans le volume. Savoir **réinitialiser** est donc une compétence de base.
- Comprendre ce qui est émulé évite les mauvaises surprises au déploiement.

## Comment le template le fait

### Persistance : `WithDataVolume` et `ContainerLifetime.Persistent`

SQL Server et Keycloak combinent deux réglages :

**Fichier :** `src/Web.Template.CQRS.AppHost/Program.cs`

```csharp
var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sqlServer = builder
    .AddSqlServer("sqlserver", password: sqlPassword)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var projectDatabase = sqlServer.AddDatabase("projectdb");
```

- `WithDataVolume()` : les données vont dans un volume Docker nommé.
- `WithLifetime(ContainerLifetime.Persistent)` : le **conteneur** n'est pas supprimé à l'arrêt de l'AppHost : il est réutilisé au lancement suivant, ce qui accélère les relances. Pour le supprimer vraiment, utilisez `aspire stop --force` (voir plus bas).

L'émulateur de stockage (`RunAsEmulator()`) n'a ni volume ni durée de vie persistante dans le template : ses blobs de test ne sont pas conservés.

### Keycloak : realm importé une seule fois, en HTTPS

Le realm `template` (clients, rôles, personas) est importé depuis le dossier `Realms` **uniquement au premier démarrage d'un volume vide**. Modifier un fichier de realm ne change donc rien tant que le volume existe : voir ci-dessous comment le réinitialiser. Le détail du contenu est dans [Keycloak en local](/Identité/Keycloak-en-local.md).

Keycloak est servi en **HTTPS** sur `https://localhost:8080`, avec le certificat de développement ASP.NET Core. Si votre machine ne lui fait pas confiance, `get-token`, `curl` et Scalar échouent avec une erreur de certificat : exécutez `dotnet dev-certs https --trust` (voir [Prérequis](/Démarrer/Prérequis.md)).

### Réinitialiser les données

Deux méthodes, de la plus simple à la plus fine.

**Tout remettre à zéro avec le CLI Aspire** (SQL Server et Keycloak sont réinitialisés). C'est la procédure de référence, que les autres pages reprennent :

```bash
# 1. arrêter et demander la suppression des ressources persistantes et de leurs volumes
aspire stop --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj --force --volumes

# 2. contrôler ce qui reste (le filtre est une recherche de sous-chaîne : lisez les noms)
docker volume ls --filter name=web.template.cqrs.apphost

# 3. seulement si des volumes de CE projet restent : les supprimer par leur nom exact
docker volume rm <nom-exact-du-volume-keycloak> <nom-exact-du-volume-sqlserver>

# 4. redémarrer et attendre (premier démarrage de Keycloak : jusqu'à plusieurs minutes)
aspire start
aspire wait keycloak --timeout 300
aspire wait api --timeout 300
```

`--force` supprime les ressources persistantes ; `--volumes` (qui exige `--force`) est censé supprimer aussi leurs volumes, et l'aide de la commande le dit. Constat sur l'Aspire CLI 13.6 : la commande se termine avec succès mais les volumes `<apphost>-<hash>-keycloak-data` et `<apphost>-<hash>-sqlserver-data` **restent**, donc le realm n'est pas réimporté. Le filtre `name=` accepte toute sous-chaîne et peut afficher les volumes d'autres projets : ne supprimez jamais « tout ce que le filtre renvoie », seulement les noms exacts de votre projet.

**Réinitialiser une seule ressource avec Docker**, par exemple Keycloak pour réimporter le realm sans perdre la base SQL. Les volumes s'appellent `<nom de l'AppHost en minuscules>-<hash>-<ressource>-data`, par exemple `web.template.cqrs.apphost-1a2b3c4d5e-keycloak-data` (le hash diffère chez vous) :

```bash
# 1. repérer le volume
docker volume ls --filter name=keycloak-data

# 2. arrêter l'AppHost, puis supprimer le conteneur de Keycloak
aspire stop
docker ps -a --filter name=keycloak
docker rm -f <nom-du-conteneur>

# 3. supprimer le volume
docker volume rm <nom-du-volume>
```

Au prochain `aspire start`, un nouveau volume est créé et le realm est réimporté.

> ⚠️ **Opérations destructrices**
>
> `--volumes` et `docker volume rm` effacent vos données locales (base de données comprise pour la première). Ne les lancez pas sur un volume dont vous avez besoin.

### Conflits de ports

Keycloak est fixé sur le port **8080** de l'hôte (`port: 8080` dans l'AppHost). Si un autre programme l'occupe, Keycloak ne démarre pas. Trouvez le coupable puis arrêtez-le :

```powershell
Get-NetTCPConnection -LocalPort 8080 -State Listen | Select-Object OwningProcess
```

Le coupable est souvent le **Keycloak d'un autre projet généré**. Keycloak (comme SQL Server) est déclaré avec `ContainerLifetime.Persistent` : un simple `aspire stop` ne supprime pas son conteneur, qui continue de tourner et de garder le port 8080. Si vous démarrez un second projet (par exemple le tutoriel `Contoso.Catalog` après `Contoso.Orders`), son Keycloak ne peut pas se lier au port 8080 et l'API attend indéfiniment (`WaitFor(keycloak)`). Cherchez et arrêtez le conteneur :

```bash
docker ps --filter name=keycloak
docker stop <nom-du-conteneur>
```

Vous pouvez aussi nettoyer le premier projet avec `aspire stop --apphost <chemin-de-l-autre-AppHost>.csproj --force` : l'option `--force` supprime les ressources persistantes (les volumes sont conservés, sauf avec `--volumes`).

SQL Server et l'émulateur de stockage n'ont pas de port imposé dans l'AppHost : Aspire en attribue (visibles dans le dashboard), ce qui évite ces conflits. Le port fixe de Keycloak est le prix à payer pour une adresse d'émetteur stable dans les tokens.

## À essayer

1. Avec l'application lancée, listez les conteneurs (`docker ps`) et retrouvez `sqlserver-…`, `keycloak-…` et le conteneur de l'émulateur de stockage.
2. Listez les volumes avec `docker volume ls`. Combien portent le nom de votre AppHost ? Pourquoi aucun n'est-il dédié au stockage ?
3. Lancez `aspire stop` puis `docker ps -a` : les conteneurs de SQL Server et de Keycloak existent-ils encore ? Quel réglage l'explique ?
4. Question : vous modifiez un utilisateur dans le fichier du realm, relancez et ne voyez aucun changement. Quelle est la marche à suivre ?

## Pour aller plus loin

- [Documentation Docker : volumes](https://docs.docker.com/engine/storage/volumes/)
- [Azurite, l'émulateur Azure Storage](https://learn.microsoft.com/azure/storage/common/storage-use-azurite)
