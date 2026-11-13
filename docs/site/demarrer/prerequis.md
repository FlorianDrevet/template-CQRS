# Prérequis

## Le concept simplement

Avant de générer et de lancer un projet, votre poste doit disposer de quelques outils. Chacun a un rôle précis, et chacun se vérifie avec une commande. Le tableau ci-dessous les résume.

| Outil | À quoi il sert | Obligatoire ? |
|-------|----------------|---------------|
| SDK .NET 10 | Compiler et exécuter l'API, fournir `dotnet new` | Oui |
| Docker Desktop ou Podman | Exécuter SQL Server, Azurite et Keycloak en conteneurs | Un des deux avec Aspire (option par défaut) |
| Aspire CLI | Démarrer et arrêter l'ensemble avec `aspire start` / `aspire stop` | Oui avec Aspire |
| Certificat de développement HTTPS | Faire confiance à `https://localhost` | Oui avec Aspire |
| Python 3.12 ou plus | Générer et construire cette documentation | Seulement pour contribuer à la doc |

## Pourquoi c'est important

La plupart des échecs au premier lancement viennent d'un prérequis manquant : conteneurs qui ne démarrent pas (moteur de conteneurs éteint), erreur de certificat (certificat non approuvé), version du SDK trop ancienne. Vérifier chaque outil avant de commencer vous fait gagner du temps.

## Comment le template le fait

### SDK .NET 10

Le dépôt fixe la version du SDK dans `global.json` :

```json title="global.json"
--8<-- "global.json"
```

`rollForward: latestMajor` autorise une version plus récente que 10.0.404, et `allowPrerelease` accepte les préversions. Le bloc `test` demande le runner `Microsoft.Testing.Platform` pour `dotnet test`.

### Un moteur de conteneurs : Docker ou Podman

Aspire démarre trois conteneurs : SQL Server, Keycloak et l'émulateur Azurite (voir [Émulateurs](../aspire/emulateurs.md)). Il lui faut donc un **moteur de conteneurs** lancé avant `aspire start`. Vous en choisissez **un seul** :

| | Docker Desktop | Podman |
|---|----------------|--------|
| Statut pour Aspire | choix par défaut, recommandé par la documentation d'Aspire | alternative prise en charge |
| Licence | payante pour les grandes entreprises (plus de 250 salariés ou 10 M$ de chiffre d'affaires) | libre |
| Fonctionnement | service qui tourne en permanence | sans service central ; une machine virtuelle sous Windows et macOS, aucune sous Linux |
| Option du template | `--ContainerRuntime Docker` (défaut) | `--ContainerRuntime Podman` |

Le choix se fait à la génération : avec `Podman`, le projet généré pose `ASPIRE_CONTAINER_RUNTIME=podman` dans le profil de lancement de l'AppHost (`Properties/launchSettings.json`), et les commandes `docker` du README et des skills deviennent `podman`. Sans Aspire (`--EnableAspire false`), aucun moteur n'est nécessaire.

#### Installer Docker Desktop

=== "Windows"

    ```powershell
    winget install -e --id Docker.DockerDesktop
    ```

=== "macOS"

    ```bash
    brew install --cask docker-desktop
    ```

=== "Linux"

    Suivez la [procédure officielle](https://docs.docker.com/engine/install/) de votre distribution.

Lancez ensuite Docker Desktop et attendez qu'il indique qu'il tourne. Sous Windows, l'installation demande les droits administrateur et peut demander un redémarrage.

#### Installer Podman

Trois choses sont nécessaires : l'outil `podman`, une machine virtuelle qui exécute les conteneurs (**Windows et macOS seulement**) et la variable d'environnement qui dit à Aspire d'utiliser Podman. **Podman Desktop**, une interface graphique pour voir vos conteneurs et vos volumes, est facultatif : Aspire n'en a pas besoin.

=== "Windows"

    Podman utilise WSL 2 pour sa machine virtuelle. `wsl --status` doit répondre ; sinon `wsl --install` l'installe (droits administrateur, puis redémarrage). L'installeur de Podman peut lui aussi demander une élévation.

    ```powershell
    winget install -e --id RedHat.Podman
    winget install -e --id RedHat.Podman-Desktop   # facultatif
    ```

    Ouvrez un **nouveau terminal** (le `PATH` n'est rechargé qu'à ce moment), puis créez et démarrez la machine :

    ```powershell
    podman machine init
    podman machine start
    ```

=== "macOS"

    ```bash
    brew install podman
    brew install --cask podman-desktop   # facultatif
    podman machine init
    podman machine start
    ```

=== "Linux"

    Pas de machine virtuelle : Podman tourne directement.

    ```bash
    sudo apt-get install -y podman   # Debian, Ubuntu ; sudo dnf install -y podman sur Fedora
    ```

Le premier `podman machine init` télécharge une image de plusieurs centaines de Mo. Il faut ensuite démarrer la machine (`podman machine start`) à chaque ouverture de session si elle ne tourne pas : `podman machine list` indique son état.

Enfin, indiquez à Aspire d'utiliser Podman. La variable est lue par l'Aspire CLI dans **votre** environnement, en plus du profil de lancement du projet généré ; posez-la une fois pour toutes :

=== "PowerShell"

    ```powershell
    [System.Environment]::SetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME", "podman", "User")
    ```

    Ouvrez un nouveau terminal pour qu'elle soit prise en compte.

=== "Bash"

    ```bash
    echo 'export ASPIRE_CONTAINER_RUNTIME=podman' >> ~/.bashrc   # ~/.zshrc sous macOS
    ```

#### Vérifier le moteur

Une fois l'Aspire CLI installée (voir plus bas), `aspire doctor` montre le moteur qu'Aspire utilisera réellement :

```bash
aspire doctor
```

Dans la section **Container Runtime**, le moteur que vous avez choisi doit être `running` et porter la mention `← active`. Si Docker et Podman sont tous deux installés, seul celui qui est marqué `← active` sert à Aspire : un Docker « installed but not running » est sans importance quand Podman est actif.

!!! tip "Le skill `init-project` fait tout cela pour vous"
    Dans le dépôt du template, le skill `init-project` vous demande Docker ou Podman, vérifie la configuration avec `aspire doctor` et propose d'installer et de configurer ce qui manque (Podman, Podman Desktop, machine, variable d'environnement), toujours après votre accord. Voir [Initialiser un projet](../harnais/initialiser-un-projet.md).

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
    docker version --format '{{.Server.Version}}'   # répond si Docker est lancé (Podman : podman info)
    aspire --version
    dotnet dev-certs https --check --trust # certificat présent et approuvé
    python --version                       # seulement pour la doc
    ```

=== "Bash"

    ```bash
    dotnet --version                       # 10.0.x
    docker version --format '{{.Server.Version}}'   # répond si Docker est lancé (Podman : podman info)
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
3. Ouvrez `global.json` et changez temporairement `rollForward` en `disable` : que dit `dotnet --version` si votre SDK n'est pas exactement 10.0.404 ? Remettez la valeur d'origine ensuite.

Poursuivez avec [Créer un projet](creer-un-projet.md).

## Pour aller plus loin

- [Installer le SDK .NET](https://learn.microsoft.com/dotnet/core/install/)
- [Approuver le certificat de développement ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/enforcing-ssl#trust-the-aspnet-core-https-development-certificate-on-windows-and-macos)
