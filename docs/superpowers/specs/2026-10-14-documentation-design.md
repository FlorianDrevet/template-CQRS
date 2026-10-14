# Sous-projet 3 — Documentation (GitHub Pages + wiki de code Azure DevOps)

- Date : 2026-10-14
- Statut : design validé en conversation, en attente de relecture de la spec
- Dépend de : sous-projet 1 (fusionné dans `main`, commit 52b92d9)
- Sous-projet 2 (harnais agentique) : ajoutera plus tard ses propres pages ; hors périmètre ici

## 1. Objectif

Fournir une documentation complète, en français, qui sert à la fois :

- de **guide d'utilisation** pour les équipes qui créent des API clientes avec le template ;
- de **cours** pour des développeurs novices : chaque concept (template, CQRS, Aspire, Keycloak, OAuth2/OIDC, observabilité, correlation id…) est expliqué depuis zéro, puis relié au code exact du template.

Une **source unique** en Markdown est publiée à deux endroits :

1. un site statique sur **GitHub Pages** ;
2. un **wiki de code Azure DevOps** publié depuis le même dépôt (le dépôt existe déjà dans Azure Repos).

### Critères de succès

1. `python eng/docs/docs.py build` produit le site et le wiki ; `python eng/docs/docs.py check` échoue si une inclusion de code est cassée (fichier ou ancre introuvable), si une syntaxe hors guide de style est utilisée, ou si `docs/wiki/` n'est pas à jour.
2. Les extraits de code affichés sont lus dans les vrais fichiers du dépôt au moment de la génération : aucun code n'est recopié à la main dans la source de la doc.
3. Le site se déploie sur GitHub Pages depuis `main` ; la CI GitHub et la CI Azure DevOps vérifient la doc sur chaque pull request.
4. Le wiki Azure DevOps affiche correctement chaque page (encadrés, onglets, diagrammes, code, liens, ordre de navigation) sans extension non supportée.
5. Un débutant peut, en suivant uniquement la doc : installer le template, générer un projet, le lancer avec Aspire, obtenir un token pour un persona, appeler l'API, puis ajouter une feature CQRS complète avec ses tests.

### Hors périmètre

- Pages du harnais agentique (sous-projet 2).
- Versionnement multi-versions du site.
- Traduction anglaise.
- Infrastructure as code de déploiement (le template n'en contient pas) : la doc décrit la configuration à fournir, pas la création des ressources Azure.

## 2. Chaîne de fabrication

```
docs/site/**.md  ── eng/docs/docs.py ──┬── resolve ──> build/docs-src/ ── Zensical ──> site/ ──> GitHub Pages
                                       └── wiki ─────> docs/wiki/ (commité) ──> wiki de code Azure DevOps (main:/docs/wiki)
```

### 2.1 Emplacements

| Chemin | Rôle | Versionné |
|---|---|---|
| `docs/site/` | Source unique de la doc (Markdown + `assets/`) | oui |
| `docs/wiki/` | Version Azure DevOps générée | oui (généré, vérifié par la CI) |
| `mkdocs.yml` | Configuration du site (lue par Zensical), navigation = ordre des pages | oui |
| `eng/docs/docs.py` | Script de génération / vérification | oui |
| `eng/docs/tests/` | Tests pytest du script | oui |
| `eng/docs/requirements.txt` | Versions épinglées (Zensical, pytest) | oui |
| `build/`, `site/` | Sorties temporaires | non (`.gitignore`) |
| `docs/superpowers/` | Specs et plans internes | oui, jamais publiés |

`mkdocs.yml` est ajouté aux exclusions de génération du template (`docs/**` et `eng/**` le sont déjà).

### 2.2 Inclusions de code ancrées sur du texte

Syntaxe, seule sur sa ligne, à l'intérieur d'un bloc de code :

```
--8<-- "chemin/depuis/la/racine.cs"
--8<-- "chemin/depuis/la/racine.cs" start="texte de début" end="texte de fin"
```

- Sans `start`/`end` : le fichier entier.
- Avec `start`/`end` : de la première ligne contenant `start` jusqu'à la première ligne suivante contenant `end`, bornes incluses. `start` seul : jusqu'à la fin du fichier.
- Échec explicite (message avec page, ligne, chemin, ancre) si le fichier n'existe pas, si une ancre est introuvable, ou si `start` apparaît plusieurs fois (ancre ambiguë).
- L'indentation commune minimale de l'extrait est retirée.
- Aucun marqueur n'est ajouté dans le code du template (ce code est livré aux clients).

### 2.3 Résolution pour le site (`resolve`)

Copie `docs/site/` vers `build/docs-src/` en remplaçant chaque inclusion par le contenu résolu ; le reste du Markdown est inchangé. `mkdocs.yml` déclare `docs_dir: build/docs-src`.

### 2.4 Conversion pour Azure DevOps (`wiki`)

Appliquée au contenu déjà résolu :

| Source | Wiki Azure DevOps |
|---|---|
| `!!! note "Titre"` + corps indenté (types : note, info, tip, warning, danger, example, question) ; `???` (repliable) idem | Citation : `> <emoji> **Titre**` puis le corps préfixé par `> `. Sans titre : nom du type en français (Note, Info, Astuce, Attention, Danger, Exemple, Question) |
| Onglets `=== "Libellé"` + corps indenté | `**Libellé**` sur sa ligne, puis le corps désindenté |
| ` ```lang title="chemin" ` | `**Fichier :** `chemin`` sur une ligne, puis ` ```lang ` |
| ` ```mermaid ` | inchangé (supporté par Azure DevOps) |
| `index.md` d'un dossier `X/` | page `X.md` à côté du dossier `X/` (convention des wikis Azure DevOps) |
| Page `nom-de-page.md` | fichier wiki nommé d'après le **titre de navigation** (`mkdocs.yml`), espaces remplacés par `-`, caractères interdits retirés |
| Liens internes `[t](../a/b.md#ancre)` | réécrits vers le chemin wiki absolu `/Dossier/Page.md` (ancre conservée) |
| Ordre de navigation | un fichier `.order` par dossier, généré depuis `nav` |
| `assets/…` (images) | copiés dans `docs/wiki/.attachments/`, liens réécrits |

Le dossier `docs/wiki/` est entièrement régénéré à chaque exécution (suppression puis écriture), avec un fichier `README` de garde indiquant qu'il est généré.

### 2.5 Guide de style (vérifié par `check`)

Syntaxe autorisée : titres, paragraphes, listes, tableaux, liens, images (`assets/`), blocs de code (avec `title` optionnel), inclusions §2.2, encadrés §2.4, onglets, mermaid. Toute autre extension Material/Zensical (annotations de code, icônes `:material-…:`, attributs `{ … }`, grilles, boutons) est refusée par `check` avec le nom du fichier et la ligne.

### 2.6 Commandes

| Commande | Effet |
|---|---|
| `python eng/docs/docs.py resolve` | produit `build/docs-src/` |
| `python eng/docs/docs.py wiki` | régénère `docs/wiki/` |
| `python eng/docs/docs.py build` | `resolve` + `wiki` + `zensical build` → `site/` |
| `python eng/docs/docs.py serve` | `resolve` puis `zensical serve` (relancer après modification de la source) |
| `python eng/docs/docs.py check` | guide de style + inclusions + `docs/wiki/` identique à une régénération (aucune écriture) |

### 2.7 Moteur et site

- Zensical (successeur de Material for MkDocs, lit `mkdocs.yml`), version épinglée. Repli documenté : Material for MkDocs 9.7 avec le même `mkdocs.yml`.
- Langue `fr`, recherche, thème clair/sombre, bouton copier sur le code, navigation par sections, lien « modifier cette page » vers GitHub.
- Extensions Markdown déclarées : `admonition`, `pymdownx.details`, `pymdownx.superfences` (fence `mermaid`), `pymdownx.tabbed` (`alternate_style: true`), `pymdownx.highlight`, `pymdownx.inlinehilite`, `tables`, `toc` (`permalink: true`). Pas de `pymdownx.snippets` : les inclusions sont résolues par `docs.py`.

### 2.8 CI

- **GitHub** `.github/workflows/docs.yml` :
  - sur pull request et push : job `check` (`pip install -r eng/docs/requirements.txt`, `pytest eng/docs/tests`, `python eng/docs/docs.py check`) ;
  - sur push vers `main` : job `deploy` (`docs.py build`, `actions/upload-pages-artifact`, `actions/deploy-pages`), permissions `pages: write`, `id-token: write`, `contents: read`.
- **Azure DevOps** `eng/azure-pipelines/docs-ci.yml` : mêmes étapes que le job `check`.
- **Wiki de code** : publication manuelle unique, documentée dans la page « Contribuer à la doc » : *Publish code as wiki* (dépôt, branche `main`, dossier `/docs/wiki`) ou `az devops wiki create --name "<Nom>" --type codewiki --repository <Repo> --mapped-path /docs/wiki --version main`. Le wiki suit ensuite `main` automatiquement.

## 3. Plan de contenu

Ton : on s'adresse à un développeur qui découvre le sujet. Chaque page de concept suit le fil **Le concept simplement → Pourquoi c'est important → Comment le template le fait (code réel inclus) → À essayer**. Les noms de code restent en anglais. Exemples de génération avec `Contoso.Orders`.

```
index.md                                  Accueil : public, parcours de lecture (dev pressé / débutant / formateur)
glossaire.md                              Glossaire
demarrer/
  index.md                                Vue d'ensemble « Démarrer »
  qu-est-ce-qu-un-template.md             dotnet new, sourceName, symboles, #if, modifiers (template.json réel)
  prerequis.md                            SDK, Docker, Aspire CLI, certificat de dev
  creer-un-projet.md                      options, fichiers générés par option, scénarios clients types
  premier-lancement.md                    Aspire, dashboard, token persona, /me, /admin/ping, Scalar
architecture/
  index.md                                Clean Architecture : couches, dépendances (diagramme)
  cqrs-et-mediator.md                     commandes/queries, Mediator source generator, parcours de /me (séquence)
  validation.md                           FluentValidation + ValidationBehavior
  erreurs-et-problem-details.md           RFC 9457, codes VALIDATION / INTERNAL / RATE_LIMITED, traceId
fonctionnalites/
  index.md                                Vue d'ensemble des fonctionnalités transverses
  observabilite.md                        cours : logs/traces/métriques, OpenTelemetry, correlation id, W3C traceparent, scopes de logs, dashboard, Application Insights
  health-checks.md                        /health, /alive, usage par l'orchestrateur
  rate-limiting.md                        fenêtre fixe, politiques, partition utilisateur/IP, 429 + Retry-After
  resilience-et-service-discovery.md      ServiceDefaults : résilience HTTP, découverte de services
  openapi-et-scalar.md                    document OpenAPI, Scalar, OAuth2 PKCE
  cors.md                                 origines autorisées
aspire/
  index.md                                Qu'est-ce qu'Aspire, AppHost vs ServiceDefaults
  apphost.md                              l'AppHost du template ligne par ligne
  emulateurs.md                           émulateur vs service réel ; SQL Server, Azurite, Keycloak ; volumes persistants, réinitialisation
  parametres-et-secrets.md                AddParameter, user-secrets
  dashboard-et-cli.md                     dashboard, aspire start/stop, MCP Aspire (présentation)
  local-vs-deploye.md                     équivalences locales / Azure
identite/
  index.md                                OAuth2, OIDC, JWT (anatomie), audience, issuer, scopes / rôles / groupes
  entra-id.md                             App Registration API, app roles, claim de groupes, clés Auth:*
  keycloak-local.md                       pourquoi, realm miroir d'Entra, mappers, clients, personas, tokens, Scalar, import au 1er démarrage, reset
  modeles-d-autorisation.md               Authenticated / AppRoles / Groups / AppRolesAndGroups, policies, code
  local-vs-deploye.md                     tableau de toutes les clés : local (Keycloak) vs déployé (Entra)
tests/
  index.md                                stratégie, tests unitaires, intégration, TestAuthHandler, écrire un test
guides/
  index.md                                Liste des guides
  ajouter-une-feature.md                  tutoriel fil rouge « Product » : entité, configuration EF, migration, repository, commande, query, validation, endpoint, policy, rate limit, tests
  ajouter-role-groupe-persona.md          realm, constantes, policies, Entra, tests
  configurer-un-deploiement.md            variables d'environnement, secrets, Application Insights, CORS, HTTPS
reference/
  index.md                                Vue d'ensemble de la référence
  options-du-template.md                  toutes les options et leurs effets
  configuration.md                        toutes les clés appsettings / variables d'environnement
  endpoints.md                            endpoints exposés
  scripts-et-ci.md                        get-token, verify-template, pipelines GitHub / Azure DevOps
  arborescence.md                         structure des fichiers d'un projet généré
  contribuer-a-la-doc.md                  écrire, inclure du code, générer, publier le wiki
```

Le tutoriel « Product » vit uniquement dans la doc : aucun code n'est ajouté au template. Ses extraits sont donc écrits dans la page (exception explicite au §2.2, puisqu'ils n'existent pas dans le dépôt) ; ils sont vérifiés une fois en les appliquant à un projet généré pendant l'implémentation.

## 4. README

- `README.md` (dépôt du template) : page d'accueil courte (ce qu'est le template, installation, une commande de génération, lien vers le site et vers le wiki). **Exclu** de la génération.
- `README.generated.md` : README du projet généré, renommé en `README.md` par `template.json`. Court, adapté aux options par `#if` (Aspire, rate limiting, modèle d'autorisation, tests), il renvoie au site pour le détail.
- `src/Web.Template.CQRS.Api/README.md` : inchangé dans son rôle, aligné sur le contenu de la doc.

## 5. Tests et vérification

- `eng/docs/tests/` (pytest) couvre chaque règle de §2.2 et §2.4 et le guide de style §2.5 : inclusions (fichier entier, plage, `start` seul, ancre manquante, ancre ambiguë, désindentation), encadrés (avec/sans titre, repliables, types), onglets, titres de blocs de code, renommage des pages et `index.md`, réécriture des liens, `.order`, images, rejet des syntaxes interdites, `check` qui détecte un wiki périmé.
- `python eng/docs/docs.py build` réussit sur la doc complète (aucune ancre cassée).
- Le site est relu localement (`docs.py serve`) ; le wiki est relu dans Azure DevOps après publication (étape manuelle signalée à l'utilisateur).
- Le tutoriel « Product » est appliqué à un projet généré : `dotnet build` et `dotnet test` passent.
- `eng/verify-template.sh` reste vert : les nouveaux fichiers (`mkdocs.yml`, `README.generated.md`) sont correctement exclus ou renommés.

## 6. Risques

- Zensical est jeune : si une fonction déclarée en §2.7 manque, repli sur Material for MkDocs 9.7 avec le même `mkdocs.yml` (aucun changement de contenu).
- Rendu Azure DevOps des citations imbriquées et des emojis : vérifié visuellement après la première publication.
- Dérive des ancres d'inclusion quand le code change : bloquée par `check` en CI, avec un message qui indique la page et l'ancre à corriger.
- Volume de contenu (~40 pages) : découpé en tâches par section, chaque page relue contre le code.
