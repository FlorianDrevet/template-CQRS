# Accueil

Ce template génère une API ASP.NET Core (.NET 10) organisée en CQRS : chaque action de l'application est soit une commande (qui modifie), soit une query (qui lit), traitée par un handler dédié. Il apporte en plus, prêts à l'emploi, des erreurs normalisées (Problem Details), l'observabilité OpenTelemetry, l'authentification Entra ID et une orchestration locale Aspire (SQL Server, Azurite, Keycloak). Vous créez un projet avec `dotnet new templatewebcqrs` et vous obtenez une base qui démarre en une commande.

Cette documentation est à la fois un guide d'utilisation et un support de cours. Elle s'adresse à des développeurs débutants ou juniors en .NET, à ceux qui découvrent CQRS, Aspire ou OAuth2, et aux personnes qui forment une équipe avec ce template. Chaque notion est expliquée depuis zéro, puis illustrée par le code réel du dépôt, puis mise en pratique dans un encadré « À essayer ».

## Trois parcours de lecture

### Je veux créer une API maintenant

1. [Prérequis](/Démarrer/Prérequis.md) : installer ce qu'il faut sur votre poste.
2. [Créer un projet](/Démarrer/Créer-un-projet.md) : choisir vos options et générer.
3. [Premier lancement](/Démarrer/Premier-lancement.md) : démarrer, obtenir un token, appeler l'API.
4. [Ajouter une feature](/Guides/Ajouter-une-feature.md) : écrire votre première commande ou query.

### Je débute et je veux comprendre

1. [Comprendre les templates](/Démarrer/Comprendre-les-templates.md) : ce que fait `dotnet new`.
2. [Architecture](/Architecture.md) : CQRS, Mediator, validation, erreurs.
3. [Fonctionnalités](/Fonctionnalités.md) : observabilité, health checks, rate limiting, OpenAPI.
4. [Aspire](/Aspire.md) : l'orchestration locale.
5. [Identité](/Identité.md) : OAuth2, Entra ID, Keycloak.

### Je forme une équipe

Voici un ordre de cours conseillé, avec une durée indicative par section (lecture et exercices « À essayer » compris).

| Étape | Section | Durée estimée | Objectif |
|-------|---------|---------------|----------|
| 1 | [Démarrer](/Démarrer.md) | 2 h | Générer le projet et l'appeler avec un token |
| 2 | [Architecture](/Architecture.md) | 3 h | Comprendre CQRS, Mediator, validation et erreurs |
| 3 | [Fonctionnalités](/Fonctionnalités.md) | 3 h | Savoir lire une trace, un health check, une limite de débit |
| 4 | [Aspire](/Aspire.md) | 2 h | Comprendre l'AppHost, les émulateurs et les secrets |
| 5 | [Identité](/Identité.md) | 4 h | Maîtriser tokens, rôles, groupes et personas |
| 6 | [Guides](/Guides.md) | 2 h | Ajouter une feature de bout en bout |

Faites réaliser les exercices « À essayer » en séance : ils sont conçus pour être exécutés sur un projet généré.

## La carte du template

Le dépôt est découpé en projets dont les dépendances vont toujours vers le centre (le Domain ne dépend de rien).

```mermaid
graph TD
    AppHost[AppHost orchestration Aspire]
    Api[Api endpoints, auth, middlewares]
    SD[ServiceDefaults OpenTelemetry, health, resilience]
    Contracts[Contracts requêtes et réponses HTTP]
    Infra[Infrastructure accès aux données, stockage]
    App[Application commandes, queries, handlers]
    Domain[Domain modèle métier]

    AppHost -. démarre .-> Api
    Api --> SD
    Api --> Contracts
    Api --> App
    Api --> Infra
    Infra --> App
    App --> Domain
    Contracts --> Domain
```

L'AppHost ne référence pas l'Api par dépendance de projet : il la démarre par son chemin de fichier `.csproj` (voir [AppHost](/Aspire/AppHost.md)). Le détail des dossiers se trouve dans [Arborescence](/Référence/Arborescence.md).

> ℹ️ **La documentation existe aussi dans le wiki Azure DevOps**
>
> Le même contenu est généré sous forme de code wiki Azure DevOps (dossier `docs/wiki` du dépôt). Seule la mise en forme change ; le texte est identique. Le [Glossaire](/Glossaire.md) vous aide si un terme vous échappe.
