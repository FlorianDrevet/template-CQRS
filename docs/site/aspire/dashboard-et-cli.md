# Dashboard et CLI

Une fois l'application lancée, trois outils vous permettent de voir et de piloter ce qui tourne : le **dashboard** (dans le navigateur), le **CLI** `aspire` (dans le terminal) et le **serveur MCP** (pour un assistant IA).

## Le concept simplement

Aspire ne fait pas que démarrer vos ressources : il les **surveille**. Le dashboard est un tableau de bord web qui regroupe, au même endroit, l'état de chaque ressource et la télémétrie de l'API. Le CLI donne accès aux mêmes informations en ligne de commande, ce qui permet de les utiliser dans un script. Le serveur MCP les met à disposition d'un assistant IA.

## Pourquoi c'est important

Sans ces outils, comprendre pourquoi l'API ne démarre pas revient à ouvrir cinq terminaux et à chercher dans quel conteneur se trouve l'erreur. Avec eux, vous voyez d'un coup d'oeil quelle ressource est en échec et pourquoi, et vous suivez une requête de l'entrée de l'API jusqu'à la base de données.

## Comment le template le fait

### Le dashboard

`aspire start` affiche l'adresse du dashboard à la fin du démarrage (elle contient un jeton de connexion : ne la partagez pas). Il comporte plusieurs pages :

| Page | Ce que vous y trouvez |
|------|-----------------------|
| **Ressources** | la liste des ressources du template (`sqlserver`, `projectdb`, `storage`, `blobs`, `keycloak`, `api`), leur état (démarrage, en cours, sain, en échec), leurs adresses et leurs commandes (démarrer, arrêter, redémarrer) |
| **Console** | la sortie standard de chaque ressource : ce que Keycloak ou l'API écrivent dans leur console |
| **Logs structurés** | les logs de l'API avec leurs champs (`TraceId`, `RequestId`…), filtrables |
| **Traces** | les requêtes avec leurs spans chronométrés (HTTP, base de données) |
| **Métriques** | les compteurs et histogrammes (durée des requêtes, mémoire…) |

La lecture des logs, traces et métriques est détaillée dans [Observabilité](../fonctionnalites/observabilite.md#lire-la-telemetrie-dans-le-dashboard-aspire). L'API envoie sa télémétrie au dashboard grâce à `ServiceDefaults`, qui active l'export OpenTelemetry quand Aspire fournit l'endpoint (variable `OTEL_EXPORTER_OTLP_ENDPOINT`).

!!! tip "Premier réflexe en cas de problème"
    Ouvrez la page **Ressources**. Une ressource en échec ou qui reste à l'état « démarrage » est le suspect : ouvrez sa **Console** pour lire l'erreur. L'API attend ses dépendances (`WaitFor`) : si `keycloak` n'est pas prêt, l'API ne démarre pas.

### Le CLI Aspire

Le CLI `aspire` est installé séparément (voir [Prérequis](../demarrer/prerequis.md)). `aspire --help` liste ses commandes ; `aspire <commande> --help` détaille les options d'une commande. Les plus utiles ici :

| Commande | Rôle |
|----------|------|
| `aspire start` | démarre l'AppHost **en arrière-plan** et rend la main |
| `aspire run` | démarre l'AppHost **au premier plan** (le terminal reste occupé ; `Ctrl+C` arrête) |
| `aspire stop` | arrête l'AppHost ; avec `--force`, supprime aussi les ressources persistantes, et avec `--volumes` (en plus de `--force`) leurs volumes |
| `aspire ps` | liste les AppHost en cours d'exécution |
| `aspire describe [ressource]` | affiche l'état des ressources (`--format Json` pour un script, `--follow` pour suivre les changements) |
| `aspire logs [ressource]` | affiche la sortie console des ressources (`--follow`, `--tail`, `--timestamps`, `--search`) |
| `aspire otel logs\|spans\|traces <ressource>` | interroge la télémétrie (logs structurés, spans, traces) |
| `aspire wait <ressource>` | attend qu'une ressource atteigne un état (`--status`, `--timeout`) |
| `aspire resource <ressource> <commande>` | exécute une commande de ressource |
| `aspire secret …` | gère les secrets de l'AppHost (voir [Paramètres et secrets](parametres-et-secrets.md)) |

Un parcours typique :

```bash
# démarrer en arrière-plan
aspire start --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj

# attendre que l'API soit saine, puis voir l'état de tout
aspire wait api --apphost src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj
aspire describe

# lire les dernières lignes de la console de Keycloak
aspire logs keycloak --tail 50

# arrêter
aspire stop
```

!!! info "`start` ou `run` ?"
    Les deux démarrent l'AppHost. `start` est pratique pour garder votre terminal libre (et c'est la commande du tutoriel [Premier lancement](../demarrer/premier-lancement.md)) ; `run` affiche la sortie en direct. `aspire start --isolated` lance une instance avec des ports aléatoires et des secrets isolés, ce qui permet d'en faire tourner plusieurs en parallèle.

Pour les commandes `aspire otel …` et leurs filtres, voir [Interroger la télémétrie depuis le terminal](../fonctionnalites/observabilite.md#interroger-la-telemetrie-depuis-le-terminal).

### Le serveur MCP

Le **Model Context Protocol (MCP)** est un standard qui permet à un assistant IA (un assistant de code, par exemple) d'appeler des outils externes. Le CLI Aspire sait jouer le rôle de serveur MCP : un assistant peut alors interroger votre application en cours d'exécution plutôt que de deviner à partir du code.

D'après la documentation officielle d'Aspire, ce serveur expose notamment des outils pour lister les ressources et leur état, lire les logs console et les logs structurés, lister les traces, et exécuter des commandes de ressource (démarrer, arrêter, redémarrer). L'assistant voit les mêmes données que le dashboard.

Côté CLI :

- `aspire agent mcp` démarre le serveur MCP ;
- `aspire agent init` prépare la configuration des assistants détectés sur votre poste (l'option `--mcp` y active le serveur MCP) ;
- l'ancienne commande `aspire mcp` est signalée comme obsolète par le CLI, au profit de `aspire agent`.

Le template configure ce serveur pour Claude Code (`.mcp.json`) et pour Copilot (`.vscode/mcp.json`). Pour la configuration, l'approbation au premier usage et des exemples de questions à poser à l'agent, voir [MCP Aspire](../harnais/mcp-aspire.md).

## À essayer

1. Lancez l'application, ouvrez le dashboard et repérez l'état de chacune des six ressources.
2. Dans le terminal, exécutez `aspire describe --format Json` : quelles informations obtenez-vous sur l'API ?
3. Lisez les 20 dernières lignes de la console de `keycloak` avec `aspire logs keycloak --tail 20`.
4. Lancez `aspire --help`, puis `aspire stop --help` : quelle option supprime les volumes, et quelle autre option doit l'accompagner ?
5. Question : quelle différence entre `aspire logs api` et `aspire otel logs api` ?

## Pour aller plus loin

- [Le dashboard Aspire](https://aspire.dev/dashboard/overview/)
- [Le serveur MCP d'Aspire](https://aspire.dev/get-started/aspire-mcp-server/)
