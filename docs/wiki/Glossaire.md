# Glossaire

Les termes sont classés par thème. La dernière colonne renvoie vers la page qui explique la notion.

## Templates

| Terme | Définition simple | Page |
|-------|-------------------|------|
| template | Dossier de fichiers et fichier `template.json` à partir desquels `dotnet new` génère un projet | [Comprendre les templates](/Démarrer/Comprendre-les-templates.md) |
| sourceName | Chaîne du template (`Web.Template.CQRS`) remplacée partout par le nom du projet généré | [Comprendre les templates](/Démarrer/Comprendre-les-templates.md) |
| symbole de template | Variable du template : option saisie (`parameter`) ou valeur calculée (`computed`) | [Comprendre les templates](/Démarrer/Comprendre-les-templates.md) |

## Architecture

| Terme | Définition simple | Page |
|-------|-------------------|------|
| CQRS | Séparer les actions qui modifient (commandes) de celles qui lisent (queries) | [CQRS et Mediator](/Architecture/CQRS-et-Mediator.md) |
| commande | Demande qui modifie l'état de l'application | [CQRS et Mediator](/Architecture/CQRS-et-Mediator.md) |
| query | Demande qui lit des données sans les modifier | [CQRS et Mediator](/Architecture/CQRS-et-Mediator.md) |
| handler | Classe qui traite une commande ou une query précise | [CQRS et Mediator](/Architecture/CQRS-et-Mediator.md) |
| Mediator | Bibliothèque qui achemine chaque commande ou query vers son handler | [CQRS et Mediator](/Architecture/CQRS-et-Mediator.md) |
| pipeline behavior | Étape commune (validation, journalisation) exécutée autour de chaque handler | [Validation](/Architecture/Validation.md) |
| Problem Details | Format standard JSON d'erreur HTTP (RFC 9457) | [Erreurs et Problem Details](/Architecture/Erreurs-et-Problem-Details.md) |

## Observabilité et robustesse

| Terme | Définition simple | Page |
|-------|-------------------|------|
| OpenTelemetry | Standard ouvert pour produire traces, métriques et logs | [Observabilité](/Fonctionnalités/Observabilité.md) |
| trace | Parcours complet d'une requête à travers l'application | [Observabilité](/Fonctionnalités/Observabilité.md) |
| span | Une étape chronométrée à l'intérieur d'une trace | [Observabilité](/Fonctionnalités/Observabilité.md) |
| correlation id | Identifiant qui permet de relier les logs d'une même requête | [Observabilité](/Fonctionnalités/Observabilité.md) |
| traceId | Identifiant d'une trace, repris dans les erreurs Problem Details | [Observabilité](/Fonctionnalités/Observabilité.md) |
| métrique | Mesure numérique agrégée dans le temps (nombre de requêtes, durée) | [Observabilité](/Fonctionnalités/Observabilité.md) |
| health check | Endpoint qui indique si l'application est en vie et prête | [Health checks](/Fonctionnalités/Health-checks.md) |
| rate limiting | Limitation du nombre de requêtes acceptées sur une période | [Rate limiting](/Fonctionnalités/Rate-limiting.md) |
| Scalar | Interface web qui affiche la documentation OpenAPI et permet d'appeler l'API | [OpenAPI et Scalar](/Fonctionnalités/OpenAPI-et-Scalar.md) |
| OpenAPI | Description standard d'une API HTTP au format JSON | [OpenAPI et Scalar](/Fonctionnalités/OpenAPI-et-Scalar.md) |

## Aspire et conteneurs

| Terme | Définition simple | Page |
|-------|-------------------|------|
| Aspire | Outillage .NET qui décrit et démarre l'ensemble des services d'une application en local | [Aspire](/Aspire.md) |
| AppHost | Projet qui décrit les ressources à démarrer (base, stockage, identité, API) | [AppHost](/Aspire/AppHost.md) |
| ServiceDefaults | Projet partagé qui configure télémétrie, health checks et résilience | [Aspire](/Aspire.md) |
| émulateur | Programme local qui imite un service cloud (Azurite pour le stockage Azure) | [Émulateurs](/Aspire/Émulateurs.md) |
| conteneur | Processus isolé et reproductible, lancé par Docker à partir d'une image | [Émulateurs](/Aspire/Émulateurs.md) |
| volume | Espace disque Docker qui conserve les données d'un conteneur | [Paramètres et secrets](/Aspire/Paramètres-et-secrets.md) |

## Identité et autorisation

| Terme | Définition simple | Page |
|-------|-------------------|------|
| OAuth2 | Protocole d'autorisation qui délivre des tokens d'accès | [Identité](/Identité.md) |
| OpenID Connect | Couche d'identité par-dessus OAuth2 (savoir qui est l'utilisateur) | [Identité](/Identité.md) |
| JWT | Token signé, au format JSON, qui contient des claims | [Identité](/Identité.md) |
| claim | Information portée par un token (identifiant, rôles, groupes…) | [Identité](/Identité.md) |
| audience | Destinataire visé par le token (`aud`) : ici l'API | [Identité](/Identité.md) |
| issuer | Émetteur du token (`iss`) : Entra ID ou Keycloak | [Identité](/Identité.md) |
| scope | Permission demandée au moment de l'authentification | [Identité](/Identité.md) |
| app role | Rôle déclaré sur l'application dans Entra ID, porté par le claim `roles` | [Modèles d'autorisation](/Identité/Modèles-d'autorisation.md) |
| groupe de sécurité | Ensemble d'utilisateurs Entra ID, porté par le claim `groups` | [Modèles d'autorisation](/Identité/Modèles-d'autorisation.md) |
| Entra ID | Service d'identité cloud de Microsoft (ex Azure AD) | [Entra ID](/Identité/Entra-ID.md) |
| Keycloak | Serveur d'identité open source qui reproduit Entra ID en local | [Keycloak en local](/Identité/Keycloak-en-local.md) |
| realm | Espace isolé de Keycloak (utilisateurs, clients, rôles) ; ici `template` | [Keycloak en local](/Identité/Keycloak-en-local.md) |
| client | Application déclarée auprès du serveur d'identité (`template-api`, `template-scalar`, `template-cli`) | [Keycloak en local](/Identité/Keycloak-en-local.md) |
| persona | Utilisateur de test local, par exemple `admin@local.test` | [Rôles, groupes et personas](/Guides/Rôles,-groupes-et-personas.md) |
| policy | Règle d'autorisation nommée appliquée à un endpoint (`RequireAdmin`) | [Modèles d'autorisation](/Identité/Modèles-d'autorisation.md) |
| PKCE | Mécanisme qui sécurise le flux Authorization Code pour les applications sans secret | [OpenAPI et Scalar](/Fonctionnalités/OpenAPI-et-Scalar.md) |
