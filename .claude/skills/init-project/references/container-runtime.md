# Moteur de conteneurs : choisir, vérifier, installer

Utilisé à l'étape 1 de `init-project`, **seulement si l'utilisateur veut Aspire** (sans Aspire, aucun conteneur n'est lancé : sautez tout ce fichier). Aspire lance SQL Server, Azurite et Keycloak dans des conteneurs ; il faut donc un moteur de conteneurs qui fonctionne.

## 1. Poser la question

Demandez : « Docker ou Podman pour les conteneurs locaux ? » et expliquez, comme à un débutant :

- **Docker** (Docker Desktop) : choix par défaut, le plus répandu, celui que la documentation d'Aspire recommande. Licence payante pour les grandes entreprises (plus de 250 salariés ou 10 M$ de chiffre d'affaires).
- **Podman** : libre et sans service central (sans daemon), alternative **officiellement prise en charge** par Aspire. Sous Windows et macOS, il tourne dans une petite machine virtuelle (`podman machine`) qu'il faut créer et démarrer. **Podman Desktop** est une interface graphique facultative (liste des conteneurs, volumes, images) : Aspire n'en a pas besoin.

Si l'utilisateur hésite, ne tranchez pas à sa place : si un seul des deux est déjà installé et fonctionne (voir ci-dessous), proposez celui-là, sinon proposez Docker (défaut). Mémorisez la réponse : elle devient `--container-runtime Docker|Podman`, et la question 5 du questionnaire n'est pas reposée.

## 2. Vérifier l'existant (lecture seule)

Déterminez le système (`$env:OS -eq 'Windows_NT'` en PowerShell, `uname -s` sinon), puis :

| Vérification | Commande | OK si |
|--------------|----------|-------|
| Outil présent | `podman --version` (ou `docker --version`) | affiche une version |
| Moteur joignable | `podman info` (ou `docker info`) | répond sans erreur |
| Machine Podman (Windows, macOS) | `podman machine list` | une machine est en `Currently running` |
| Vue d'Aspire (CLI installée) | `aspire doctor` | section « Container Runtime » : le moteur voulu est `running` et marqué `← active` |

`aspire doctor` est la référence (libellés observés avec l'Aspire CLI 13.6 ; s'ils diffèrent dans votre version, retombez sur `podman info` / `docker info` et sur la variable `ASPIRE_CONTAINER_RUNTIME`) : il indique le moteur **réellement utilisé par Aspire** et pourquoi (par exemple « configured via ASPIRE_CONTAINER_RUNTIME=podman »). Un moteur voulu mais non actif signifie que la variable `ASPIRE_CONTAINER_RUNTIME` manque dans cet environnement (voir § 4). Si Docker est « installed but not running » alors que vous choisissez Podman, c'est sans importance tant que Podman est `← active`.

Tout est vert : continuez l'étape 1, rien à installer.

## 3. Proposer la configuration complète

Ordre : **Aspire CLI, puis moteur, puis `aspire doctor`** (`aspire doctor` n'existe qu'une fois la CLI installée).

- Aspire CLI (avec Aspire) : `dotnet tool install -g Aspire.Cli` (outil global .NET, sans droits administrateur). Si `aspire` reste introuvable ensuite, ouvrez un nouveau terminal.
- Certificat de développement (avec Aspire) : `dotnet dev-certs https --trust` (boîte de dialogue système sous Windows et macOS).

Si quelque chose manque, **listez d'abord tout ce qui serait fait** (outil à installer, machine à créer, variable à poser) avec les commandes exactes, et demandez l'accord. Puis exécutez **une étape à la fois**, en redemandant avant celles qui touchent le système (droits administrateur, redémarrage, `sudo`, variable d'environnement permanente). Sans accord : ne contournez pas ; proposez à la place de générer le projet **sans Aspire** (`--aspire false`, aucun conteneur nécessaire) ou laissez l'utilisateur configurer lui-même, puis relancer le skill.

### Podman, Windows

Prérequis : WSL 2 (moteur par défaut de la machine Podman). Vérifiez avec `wsl --status`. S'il manque, `wsl --install` demande les droits administrateur et un **redémarrage** : expliquez-le, demandez l'accord, puis arrêtez-vous en demandant de relancer la session après le redémarrage.

```powershell
winget install -e --id RedHat.Podman                 # CLI Podman ; l'installeur peut demander une élévation (UAC)
winget install -e --id RedHat.Podman-Desktop         # facultatif : interface graphique ; idem
podman machine init                                  # crée la machine (WSL) ; ignorez si `podman machine list` en montre une
podman machine start
```

Après l'installation, le `PATH` n'est mis à jour que dans un **nouveau terminal** : rouvrez-en un (ou appelez `podman` par son chemin complet) avant de revérifier. Le premier `podman machine init` / `start` télécharge une image de plusieurs centaines de Mo.

### Podman, macOS

```bash
brew install podman
brew install --cask podman-desktop     # facultatif
podman machine init                    # ignorez si `podman machine list` en montre une
podman machine start
```

### Podman, Linux

Pas de machine : Podman tourne directement (mode « rootless »).

```bash
sudo apt-get install -y podman         # Debian, Ubuntu
sudo dnf install -y podman             # Fedora, RHEL
sudo pacman -S podman                  # Arch
flatpak install flathub io.podman_desktop.PodmanDesktop   # facultatif
```

`sudo` demande un mot de passe : laissez l'utilisateur le saisir (`! <commande>` dans le prompt), ne le demandez pas.

### Docker

```powershell
winget install -e --id Docker.DockerDesktop          # Windows : droits administrateur, redémarrage possible
```

```bash
brew install --cask docker-desktop                   # macOS
```

Linux : suivez https://docs.docker.com/engine/install/ (dépend de la distribution) et ne l'automatisez pas. Après installation, **lancez Docker Desktop** (Windows, macOS) et attendez que `docker info` réponde.

## 4. Faire utiliser Podman à Aspire

Aspire choisit son moteur avec la variable d'environnement `ASPIRE_CONTAINER_RUNTIME` (`docker` par défaut). Le projet généré avec `--container-runtime Podman` la pose pour son AppHost, mais la CLI `aspire` (et `aspire doctor`) la lit dans **votre** environnement : posez-la aussi de façon permanente, avec l'accord de l'utilisateur.

```powershell
[System.Environment]::SetEnvironmentVariable("ASPIRE_CONTAINER_RUNTIME", "podman", "User")   # Windows : nouveau terminal requis
```

```bash
grep -q ASPIRE_CONTAINER_RUNTIME ~/.zshrc 2>/dev/null || echo 'export ASPIRE_CONTAINER_RUNTIME=podman' >> ~/.zshrc   # macOS ; ~/.bashrc sous Linux avec bash
```

Pour la session courante, sans attendre : `$env:ASPIRE_CONTAINER_RUNTIME = "podman"` (PowerShell) ou `export ASPIRE_CONTAINER_RUNTIME=podman` (Bash). Vérifiez l'existant avant d'écrire (`[Environment]::GetEnvironmentVariable('ASPIRE_CONTAINER_RUNTIME','User')`) : si la valeur est déjà `podman`, ne touchez à rien. Si elle vaut autre chose, demandez avant de la remplacer.

## 5. Revérifier

Rejouez le tableau du § 2, en commençant par `aspire doctor`. Le skill n'avance que lorsque le moteur choisi est `running` et `← active`. Sinon, montrez la ligne de `aspire doctor` et proposez la correction correspondante :

| Constat | Cause | Remède |
|---------|-------|--------|
| `podman: command not found` juste après l'installation | `PATH` pas rechargé | nouveau terminal |
| `podman info` : « Cannot connect to Podman » (Windows, macOS) | machine absente ou arrêtée | `podman machine list`, puis `podman machine init` / `podman machine start` |
| `podman machine init` échoue sous Windows | WSL 2 absent ou désactivé | `wsl --status` ; `wsl --install` (administrateur + redémarrage), avec accord |
| `aspire doctor` : Docker `← active` alors que Podman tourne | `ASPIRE_CONTAINER_RUNTIME` absente de cet environnement | § 4 (puis nouveau terminal) |
| `aspire doctor` : Podman `← active` alors que l'utilisateur veut Docker | variable `ASPIRE_CONTAINER_RUNTIME=podman` posée globalement | demandez avant de la supprimer ou de la passer à `docker` |
