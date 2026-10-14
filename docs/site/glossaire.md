# Glossaire

Les termes sont classés par thème. La dernière colonne renvoie vers la page qui explique la notion.

## Templates

| Terme | Définition simple | Page |
|-------|-------------------|------|
| template | Dossier de fichiers et fichier `template.json` à partir desquels `dotnet new` génère un projet | [Comprendre les templates](demarrer/qu-est-ce-qu-un-template.md) |
| sourceName | Chaîne du template (`Web.Template.CQRS`) remplacée partout par le nom du projet généré | [Comprendre les templates](demarrer/qu-est-ce-qu-un-template.md) |
| symbole de template | Variable du template : option saisie (`parameter`) ou valeur calculée (`computed`) | [Comprendre les templates](demarrer/qu-est-ce-qu-un-template.md) |

## Architecture

| Terme | Définition simple | Page |
|-------|-------------------|------|
| CQRS | Séparer les actions qui modifient (commandes) de celles qui lisent (queries) | [CQRS et Mediator](architecture/cqrs-et-mediator.md) |
| commande | Demande qui modifie l'état de l'application | [CQRS et Mediator](architecture/cqrs-et-mediator.md) |
| query | Demande qui lit des données sans les modifier | [CQRS et Mediator](architecture/cqrs-et-mediator.md) |
| handler | Classe qui traite une commande ou une query précise | [CQRS et Mediator](architecture/cqrs-et-mediator.md) |
| Mediator | Bibliothèque qui achemine chaque commande ou query vers son handler | [CQRS et Mediator](architecture/cqrs-et-mediator.md) |
| pipeline behavior | Étape commune (validation, journalisation) exécutée autour de chaque handler | [Validation](architecture/validation.md) |
| Problem Details | Format standard JSON d'erreur HTTP (RFC 9457) | [Erreurs et Problem Details](architecture/erreurs-et-problem-details.md) |

## Observabilité et robustesse

| Terme | Définition simple | Page |
|-------|-------------------|------|
| OpenTelemetry | Standard ouvert pour produire traces, métriques et logs | [Observabilité](fonctionnalites/observabilite.md) |
| trace | Parcours complet d'une requête à travers l'application | [Observabilité](fonctionnalites/observabilite.md) |
| span | Une étape chronométrée à l'intérieur d'une trace | [Observabilité](fonctionnalites/observabilite.md) |
| correlation id | Identifiant qui permet de relier les logs d'une même requête | [Observabilité](fonctionnalites/observabilite.md) |
| traceId | Identifiant d'une trace, repris dans les erreurs Problem Details | [Observabilité](fonctionnalites/observabilite.md) |
| métrique | Mesure numérique agrégée dans le temps (nombre de requêtes, durée) | [Observabilité](fonctionnalites/observabilite.md) |
| health check | Endpoint qui indique si l'application est en vie et prête | [Health checks](fonctionnalites/health-checks.md) |
| rate limiting | Limitation du nombre de requêtes acceptées sur une période | [Rate limiting](fonctionnalites/rate-limiting.md) |
| Scalar | Interface web qui affiche la documentation OpenAPI et permet d'appeler l'API | [OpenAPI et Scalar](fonctionnalites/openapi-et-scalar.md) |
| OpenAPI | Description standard d'une API HTTP au format JSON | [OpenAPI et Scalar](fonctionnalites/openapi-et-scalar.md) |

## Aspire et conteneurs

| Terme | Définition simple | Page |
|-------|-------------------|------|
| Aspire | Outillage .NET qui décrit et démarre l'ensemble des services d'une application en local | [Aspire](aspire/index.md) |
| AppHost | Projet qui décrit les ressources à démarrer (base, stockage, identité, API) | [AppHost](aspire/apphost.md) |
| ServiceDefaults | Projet partagé qui configure télémétrie, health checks et résilience | [Aspire](aspire/index.md) |
| émulateur | Programme local qui imite un service cloud (Azurite pour le stockage Azure) | [Émulateurs](aspire/emulateurs.md) |
| conteneur | Processus isolé et reproductible, lancé par Docker à partir d'une image | [Émulateurs](aspire/emulateurs.md) |
| volume | Espace disque Docker qui conserve les données d'un conteneur | [Paramètres et secrets](aspire/parametres-et-secrets.md) |

## Identité et autorisation

| Terme | Définition simple | Page |
|-------|-------------------|------|
| OAuth2 | Protocole d'autorisation qui délivre des tokens d'accès | [Identité](identite/index.md) |
| OpenID Connect | Couche d'identité par-dessus OAuth2 (savoir qui est l'utilisateur) | [Identité](identite/index.md) |
| JWT | Token signé, au format JSON, qui contient des claims | [Identité](identite/index.md) |
| claim | Information portée par un token (identifiant, rôles, groupes…) | [Identité](identite/index.md) |
| audience | Destinataire visé par le token (`aud`) : ici l'API | [Identité](identite/index.md) |
| issuer | Émetteur du token (`iss`) : Entra ID ou Keycloak | [Identité](identite/index.md) |
| scope | Permission demandée au moment de l'authentification | [Identité](identite/index.md) |
| app role | Rôle déclaré sur l'application dans Entra ID, porté par le claim `roles` | [Modèles d'autorisation](identite/modeles-d-autorisation.md) |
| groupe de sécurité | Ensemble d'utilisateurs Entra ID, porté par le claim `groups` | [Modèles d'autorisation](identite/modeles-d-autorisation.md) |
| Entra ID | Service d'identité cloud de Microsoft (ex Azure AD) | [Entra ID](identite/entra-id.md) |
| Keycloak | Serveur d'identité open source qui reproduit Entra ID en local | [Keycloak en local](identite/keycloak-local.md) |
| realm | Espace isolé de Keycloak (utilisateurs, clients, rôles) ; ici `template` | [Keycloak en local](identite/keycloak-local.md) |
| client | Application déclarée auprès du serveur d'identité (`template-api`, `template-scalar`, `template-cli`) | [Keycloak en local](identite/keycloak-local.md) |
| persona | Utilisateur de test local, par exemple `admin@local.test` | [Rôles, groupes et personas](guides/ajouter-role-groupe-persona.md) |
| policy | Règle d'autorisation nommée appliquée à un endpoint (`RequireAdmin`) | [Modèles d'autorisation](identite/modeles-d-autorisation.md) |
| PKCE | Mécanisme qui sécurise le flux Authorization Code pour les applications sans secret | [OpenAPI et Scalar](fonctionnalites/openapi-et-scalar.md) |
