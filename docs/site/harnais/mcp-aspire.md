# MCP Aspire

Cette page explique comment un agent peut interroger votre application **pendant qu'elle tourne** grâce au serveur MCP d'Aspire : comment il est configuré dans le template, ce qu'il faut approuver au premier usage, ce que fait `aspire agent init` et comment poser de bonnes questions à l'agent.

## Le concept simplement

Sans outil, un agent devine le comportement de l'application en lisant le code. Avec le **Model Context Protocol (MCP)**, il peut au contraire appeler des outils : « liste les ressources », « donne-moi les logs de l'API », « montre-moi cette trace ». Le CLI Aspire sait jouer le rôle de serveur MCP avec la commande `aspire agent mcp`. L'agent voit alors les mêmes données que le dashboard (voir [Dashboard et CLI](../aspire/dashboard-et-cli.md)), mais sans que vous ayez à les copier à la main.

!!! note "Commande obsolète"
    L'ancienne commande `aspire mcp` est signalée comme obsolète par le CLI. Utilisez `aspire agent mcp`.

## Pourquoi c'est important

- Une erreur 500 se diagnostique à partir de sa trace réelle, pas d'une hypothèse sur le code.
- L'agent peut vérifier qu'une ressource est saine avant de lancer un test, au lieu d'échouer sur un timeout.
- Le serveur est lancé à la demande par l'outil de l'agent, sans service à installer en plus du CLI Aspire.

## Comment le template le fait

### La configuration

Un projet généré **avec Aspire** contient deux fichiers qui déclarent le même serveur, un par outil.

=== "Claude Code (.mcp.json)"
    ```json title=".mcp.json"
    --8<-- ".mcp.json"
    ```

=== "Copilot (.vscode/mcp.json)"
    ```json title=".vscode/mcp.json"
    --8<-- ".vscode/mcp.json"
    ```

Dans les deux cas, l'outil lance `aspire agent mcp` en processus local (transport `stdio`). Le CLI `aspire` doit donc être installé sur votre poste (voir [Prérequis](../demarrer/prerequis.md)). Ces fichiers sont commités par le template : le MCP fonctionne même sans avoir lancé `aspire agent init`. Sans Aspire, ils ne sont pas générés.

### L'approbation au premier usage

Claude Code demande votre approbation avant d'utiliser un serveur MCP déclaré dans un fichier `.mcp.json` de projet : une fois le dossier ouvert, il vous propose d'activer le serveur `aspire`. C'est voulu, car un `.mcp.json` lance une commande sur votre poste ; lisez-le avant d'accepter. Ici il ne lance que `aspire agent mcp`. Copilot lit `.vscode/mcp.json` depuis VS Code.

### `aspire agent init`

Le serveur fonctionne sans cette commande, mais elle complète la configuration. Vous la lancez à la racine du projet :

```bash
aspire agent init --non-interactive --workspace-root . --skill-locations claudecode --skills aspire,aspire-orchestration,aspire-monitoring --mcp
```

Elle fait trois choses :

1. elle écrit (ou met à jour) `.mcp.json` et `.vscode/mcp.json` ;
2. elle installe les **skills officiels Aspire** dans `.claude/skills/` ;
3. elle installe des **hooks de télémétrie**.

!!! warning "Télémétrie"
    Les hooks de télémétrie sont installés côté utilisateur. Pour la refuser, définissez la variable d'environnement `ASPIRE_CLI_TELEMETRY_OPTOUT=true`. Le skill `init-project` ne lance cette commande qu'après votre accord.

Les trois skills officiels sont `aspire` (utilisation générale du CLI), `aspire-orchestration` (déclarer et orchestrer les ressources de l'AppHost) et `aspire-monitoring` (logs, traces, métriques). Ils complètent les skills du template : ceux de cette section parlent de **votre** projet, ceux d'Aspire de l'outil. `aspire agent init --help` détaille toutes les options.

### Ce que l'agent peut faire

D'après la documentation officielle d'Aspire, le serveur expose notamment des outils (selon la version du CLI) pour lister les ressources et leur état, lire les logs console et les logs structurés, lister les traces et exécuter des commandes de ressource (démarrer, arrêter, redémarrer). Le skill `run-local` les utilise en priorité et retombe sur le CLI (`aspire logs`, `aspire otel`) quand ils ne sont pas disponibles.

Voici des questions à poser à l'agent, avec ce qu'elles déclenchent :

| Vous demandez | L'agent consulte |
|---------------|------------------|
| « Quelles ressources tournent, et sont-elles saines ? » | l'état des ressources (équivalent de `aspire describe`) |
| « Pourquoi Keycloak ne démarre pas ? » | les logs console de `keycloak` |
| « Montre-moi la trace de cette erreur 500. » | la trace correspondant au `traceId` du Problem Details, puis les logs structurés corrélés |
| « Y a-t-il des erreurs dans les logs de l'API depuis le démarrage ? » | les logs structurés de la ressource `api`, filtrés sur les erreurs |
| « Redémarre l'API. » | une commande de ressource (demandez-lui d'abord de vous annoncer ce qu'il va faire) |

!!! tip "Donnez le `traceId`"
    Toute réponse d'erreur de l'API est un Problem Details qui contient un `traceId`. Coller cet identifiant dans votre demande permet à l'agent d'aller droit à la bonne trace.

## À essayer

1. Dans un projet généré avec Aspire, ouvrez le dossier dans Claude Code : êtes-vous invité à approuver le serveur MCP `aspire` ?
2. Démarrez l'environnement avec `aspire start`, puis demandez à l'agent : « Quelles ressources tournent et dans quel état ? » Comparez avec `aspire describe`.
3. Provoquez une erreur (par exemple en appelant une route avec un identifiant invalide), copiez le `traceId` de la réponse et demandez : « Montre-moi la trace `<traceId>`. »
4. Lancez `aspire agent init --help` et repérez l'option qui limite les skills installés.

## Pour aller plus loin

- [Dashboard et CLI](../aspire/dashboard-et-cli.md) : les mêmes informations par le dashboard et le terminal.
- [Observabilité](../fonctionnalites/observabilite.md) : logs, traces et métriques de l'API.
- [Le serveur MCP d'Aspire](https://aspire.dev/get-started/aspire-mcp-server/)
- [Claude Code, les serveurs MCP](https://code.claude.com/docs/en/mcp)
