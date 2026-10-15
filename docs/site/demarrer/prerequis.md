# Prérequis

## Le concept simplement

Avant de générer et de lancer un projet, votre poste doit disposer de quelques outils. Chacun a un rôle précis, et chacun se vérifie avec une commande. Le tableau ci-dessous les résume.

| Outil | À quoi il sert | Obligatoire ? |
|-------|----------------|---------------|
| SDK .NET 10 | Compiler et exécuter l'API, fournir `dotnet new` | Oui |
| Docker Desktop | Exécuter SQL Server, Azurite et Keycloak en conteneurs | Oui avec Aspire (option par défaut) |
| Aspire CLI | Démarrer et arrêter l'ensemble avec `aspire start` / `aspire stop` | Oui avec Aspire |
| Certificat de développement HTTPS | Faire confiance à `https://localhost` | Oui avec Aspire |
| Python 3.12 ou plus | Générer et construire cette documentation | Seulement pour contribuer à la doc |

## Pourquoi c'est important

La plupart des échecs au premier lancement viennent d'un prérequis manquant : conteneurs qui ne démarrent pas (Docker éteint), erreur de certificat (certificat non approuvé), version du SDK trop ancienne. Vérifier chaque outil avant de commencer vous fait gagner du temps.

## Comment le template le fait

### SDK .NET 10

Le dépôt fixe la version du SDK dans `global.json` :

```json title="global.json"
--8<-- "global.json"
```

`rollForward: latestMajor` autorise une version plus récente que 10.0.203, et `allowPrerelease` accepte les préversions. Le bloc `test` demande le runner `Microsoft.Testing.Platform` pour `dotnet test`.

### Docker Desktop

Aspire démarre trois conteneurs : SQL Server, Keycloak et l'émulateur Azurite (voir [Émulateurs](../aspire/emulateurs.md)). Docker doit donc être lancé avant `aspire start`.

### Aspire CLI

L'Aspire CLI est un outil global .NET. C'est lui qui lit l'AppHost, démarre l'application et ouvre le dashboard (voir [Dashboard et CLI](../aspire/dashboard-et-cli.md)).

### Certificat de développement HTTPS

Keycloak (`https://localhost:8080`) et l'API (`https://localhost:7246`) sont servis en HTTPS avec le certificat de développement de .NET. Si votre machine ne lui fait pas confiance, `get-token` et `curl` échouent avec une erreur de certificat, et Scalar ne peut pas joindre Keycloak. La commande `dotnet dev-certs https --trust` crée le certificat s'il manque et l'approuve.

!!! note "Sous Linux"
    `dotnet dev-certs https --trust` n'approuve pas le certificat pour tous les outils sous Linux. Exportez-le au format PEM, par exemple avec `dotnet dev-certs https --export-path ./devcert.pem --format Pem --no-password`, puis indiquez-le à `curl` avec la variable `CURL_CA_BUNDLE` (`export CURL_CA_BUNDLE=$PWD/devcert.pem`). Le script `scripts/get-token.sh` utilise `curl` et en profite.

### Vérifier chaque prérequis

=== "PowerShell"

    ```powershell
    dotnet --version                       # 10.0.x
    docker version --format '{{.Server.Version}}'   # répond si Docker est lancé
    aspire --version
    dotnet dev-certs https --check --trust # certificat présent et approuvé
    python --version                       # seulement pour la doc
    ```

=== "Bash"

    ```bash
    dotnet --version                       # 10.0.x
    docker version --format '{{.Server.Version}}'   # répond si Docker est lancé
    aspire --version
    dotnet dev-certs https --check --trust # certificat présent et approuvé
    python3 --version                      # seulement pour la doc
    ```

Pour installer l'Aspire CLI et approuver le certificat :

```bash
dotnet tool install -g Aspire.Cli
dotnet dev-certs https --trust
```

!!! tip "Sans Aspire"
    Si vous générez avec `--EnableAspire false`, ni Docker, ni l'Aspire CLI, ni le certificat Keycloak ne sont nécessaires : seul le SDK .NET l'est. Vous devez alors fournir vous-même les chaînes de connexion et la configuration Entra ID (voir [Créer un projet](creer-un-projet.md)).

## À essayer

1. Lancez les commandes de vérification de votre onglet et notez celle qui échoue, s'il y en a une.
2. Arrêtez Docker Desktop puis relancez `docker version` : observez l'erreur que vous obtiendrez si vous oubliez de le démarrer.
3. Ouvrez `global.json` et changez temporairement `rollForward` en `disable` : que dit `dotnet --version` si votre SDK n'est pas exactement 10.0.203 ? Remettez la valeur d'origine ensuite.

Poursuivez avec [Créer un projet](creer-un-projet.md).

## Pour aller plus loin

- [Installer le SDK .NET](https://learn.microsoft.com/dotnet/core/install/)
- [Approuver le certificat de développement ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl#trust-the-aspnet-core-https-development-certificate-on-windows-and-macos)
