---
name: perf-guardrails
description: "Règles de performance de l'API : à appliquer avant d'écrire ou de relire un endpoint de liste ou de recherche, une requête EF Core, un import ou un export, un appel HTTP sortant, un envoi de blob, une nouvelle ressource Azure ou un cache. Règles absolues et gratuites (pagination, filtre, tri et recherche toujours côté serveur, lectures EF, index, timeouts) ; les options payantes ou d'infrastructure (Redis, palier SQL, réplicas, CDN) sont seulement proposées en une ligne et jamais mises en place sans accord."
---

# Garde-fous de performance

Synthèse critique, reformulée pour cette API, d'un guide tiers de 31 correctifs (« App-Performance Toolkit », BreakingBreach, usage personnel : ne le recopiez pas). Ce skill garde ce qui est vrai pour la stack du template (.NET 10, EF Core et SQL Server, Mediator, FluentValidation, minimal API) et écarte le reste. Il couvre **l'API** : le front d'un projet client a ses propres règles.

L'environnement de production (palier de la base, nombre de réplicas, région) est propre à chaque projet. Lisez `project-profile.json` s'il existe ; sinon, **ne supposez pas** de capacité confortable : appliquez §A et demandez avant d'en conclure quoi que ce soit.

## Comment l'utiliser

Avant d'écrire ou de relire du code qui touche un déclencheur de la description :

1. Appliquez **§A** sans demander : ce sont des règles, pas des options. Une dérogation exige l'accord explicite de l'utilisateur et se note dans la réponse.
2. Si un cas relève de **§B**, proposez-le en **une ligne** (format ci-dessous) et attendez la réponse. N'implémentez jamais une reco §B sans « oui ».
3. Terminez par la **vérification §D** et résumez-la en une ligne dans votre réponse.

N'optimisez pas au-delà de §A/§B sans mesure (plan d'exécution, traces, métriques).

---

## A. Règles absolues (gratuites : toujours)

### A1. Pagination, filtre, tri et recherche : TOUJOURS côté serveur

Tout endpoint de liste dont la collection peut croître :

- La query porte `Page` et `PageSize` (plus les filtres et le tri), et le repository applique `Where`, `OrderBy`, `Skip` et `Take` **dans la requête SQL**. Jamais « tout charger puis filtrer, trier ou paginer en mémoire » : pas de `GetAllAsync()` suivi de LINQ pour ces listes.
- Bornes imposées par le **validateur** (400 `VALIDATION` hors bornes), comme dans le patron « Liste » d'`add-feature` : `Page` entre 1 et 10 000, `PageSize` entre 1 et 100, défaut 20 à 50. Le client ne fixe jamais la taille sans plafond.
- Tri : uniquement sur une **liste blanche** de colonnes (un `enum` ou un `switch` ; jamais un nom de colonne ou une expression venue du client), toujours **déterministe** : ajoutez en dernier critère une colonne unique comparable pour que les pages ne sautent ni ne répètent de lignes (l'identifiant est un `ValueObject` : voir le commentaire du patron « Liste »).
- Recherche et filtres : validés (longueur maximale ; longueur minimale de 2 pour une recherche libre), traduits en `Where` SQL. Les paramètres de filtre sont des paramètres de la query, donc de l'URL.
- Total : ne le calculez que si le client l'affiche (un `Count` coûte une lecture). Sinon, lisez `PageSize + 1` ligne et renvoyez un indicateur « page suivante ». Le patron « Liste » renvoie un tableau JSON ; passer à une enveloppe (`Items`, `Page`, `PageSize`, `Total` ou `HasMore`, en `*Result` et `*Response` distincts) **change le contrat** : demandez avant de le faire sur un endpoint existant.
- Pagination par offset (`Skip`/`Take`) par défaut. Un curseur (keyset) ne se justifie que pour un flux « charger plus » ou une très grosse table ; il rend le tri multi-colonnes impraticable.
- Exception : une collection **bornée par nature** (quelques dizaines de lignes : rôles, pays…) peut rester complète. Dites-le dans un commentaire et dans la réponse. Attention : `GetAllAsync()` charge toutes les lignes **suivies** par EF ; pour une liste, préférez une méthode dédiée en lecture seule (A3).

### A2. Recherche sargable

Pas de `Contains` ni de `LIKE '%x%'` sur une colonne d'une table qui grossit sans index adapté : EF le traduit en `LIKE` non indexable, donc en lecture complète. Préférez un préfixe (`StartsWith`, traduit en `LIKE 'x%'`), une colonne normalisée indexée ou le full-text SQL Server. Signalez le choix dans la réponse.

### A3. Lectures EF Core

- Toute méthode de repository qui **lit sans modifier** : `AsNoTracking()`. `GetByIdAsync` et `GetAllAsync` de `BaseRepository` suivent les entités (elles servent aux écritures) : ne les utilisez pas pour servir une liste ou une lecture.
- Projetez en SQL (`Select` vers un type de l'Application, souvent le `*Result`) dès que l'entité porte des colonnes lourdes (texte long, JSON, binaire) inutiles à l'écran. Jamais d'entité renvoyée au client (convention du projet).
- Zéro accès base dans une boucle ou un `Select` en mémoire (N+1) ; `Include` seulement de ce qui sert ; plusieurs collections incluses : `AsSplitQuery()`.
- Tout filtre est traduit en SQL **avant** `ToListAsync()` (aucun `AsEnumerable()` ni `ToList()` avant un `Where`).
- Les nouvelles méthodes de repository prennent un `CancellationToken` et le transmettent à EF ; le handler passe celui qu'il reçoit. (Les méthodes de `IRepository<T>` n'en ont pas : n'élargissez pas cette interface sans accord, elle est utilisée partout.)

### A4. Index livrés avec la requête

Toute nouvelle requête filtrée, triée ou paginée livre ses index **dans la même migration** : clés étrangères, colonnes de `Where` et d'`OrderBy`, composites dans le bon ordre (égalités d'abord, puis tri). Déclarez-les avec `HasIndex` dans `<Entity>Configuration`. Pas d'index « au cas où » : chacun coûte en écriture et en stockage. Sur une grosse table déjà en production, créez-le en ligne (`ONLINE = ON` dans le script de migration) et prévenez avant de l'appliquer.

### A5. Imports et exports volumineux

- Import : lecture en flux, traitement **par lots** (de l'ordre de 500 à 1 000), un `SaveChangesAsync` par lot ; jamais un par ligne. Vider le `ChangeTracker` entre deux lots.
- Export : écrit en flux dans la réponse (`IAsyncEnumerable<T>`, `Results.Stream`) ; jamais construit en entier en mémoire.
- Fichiers (blob) : transmettez des `Stream`, jamais un `byte[]` complet. `IBlobService.UploadFileAsync` prend déjà un `Stream` : gardez-le ainsi, et plafonnez la taille acceptée côté API.
- Au-delà de ~2 s de traitement ou d'un volume qui peut croître : A6.

### A6. Travail lent hors de la requête

Tout ce qui dépasse ~1 à 2 s (traitement de fichier, e-mails en masse, appels externes en lot) ne s'exécute pas dans la requête : l'endpoint répond vite (`202 Accepted` et un moyen de lire l'avancement) et le travail est **idempotent** (un retry ne duplique rien). Le template ne fournit pas de worker : si le projet n'en a pas, c'est une reco §B (un `BackgroundService` en mémoire perd les tâches au redémarrage).

### A7. Appels sortants

- `IHttpClientFactory` (client typé) ; jamais `new HttpClient()` par appel. Transmettez le `CancellationToken`.
- Avec Aspire, `ServiceDefaults` applique `AddStandardResilienceHandler()` à tous les clients (timeouts, nouvelle tentative, disjoncteur). **Sans Aspire, rien n'est posé** : donnez un timeout explicite (le défaut de `HttpClient` est de 100 s).
- Le handler standard **retente aussi les `POST`** : pour une écriture non idempotente (paiement, envoi), désactivez les nouvelles tentatives pour ce client (`Retry.DisableForUnsafeHttpMethods()`) ou utilisez une clé d'idempotence du fournisseur. Ne retentez jamais une écriture à l'aveugle.
- À l'échéance : une erreur propre (Problem Details), jamais le message de l'exception.

### A8. Parallélisme

**Jamais** `Task.WhenAll` sur des requêtes d'un même `DbContext` (il n'est pas thread-safe) ni de rafale de requêtes parallèles vers une petite base. Autorisé : des appels externes indépendants, avec un plafond de concurrence.

### A9. Pas de doublons

Création et import protégés contre la double soumission : unicité vérifiée dans le handler **et** index unique (voir « Conflits et unicité » dans `add-feature`), ou clé d'idempotence acceptée par l'endpoint. Un endpoint de création renvoie 409 `CONFLICT`, pas deux lignes.

### A10. Cache HTTP et compression

- Une réponse qui contient des données propres à l'utilisateur n'est **jamais** publique : `Cache-Control: private` (ou `no-store` pour les données sensibles). N'ajoutez ni cache de sortie ni cache partagé sur une route authentifiée sans que la clé inclue l'identité.
- La compression Brotli/gzip du JSON se fait de préférence au niveau de l'hébergeur ou de la passerelle. Si vous activez le middleware de compression de l'API, ne l'activez pas pour HTTPS sans y avoir réfléchi (risque BREACH quand une réponse mêle un secret et du contenu contrôlé par l'appelant), et vérifiez qu'un flux (SSE, export en flux) n'est pas mis en tampon.

### A11. Ressources et état

- Toute nouvelle ressource Azure est créée dans la **même région que la base**.
- Une seule instance de `DbContext` par requête (injection, `AddDbContext`) ; jamais `new SqlConnection` ni `new ProjectDbContext` dans un handler.
- Aucun état en mémoire qui casserait avec deux réplicas : sessions, compteurs, caches non invalidables, tâches planifiées qui se doubleraient. Un cache en mémoire n'est acceptable que pour des données de référence à TTL court, où une copie par instance est sans conséquence.
- Le rate limiting (`RateLimitingPolicies.*`, si le projet l'a) protège aussi la base : une route coûteuse (recherche, export) prend la politique adaptée, pas `Read` par défaut.

---

## B. Recommandations payantes ou d'infrastructure (l'agent propose, l'utilisateur décide)

Ne rien implémenter, ne rien créer sans « oui ». Proposez seulement quand un **déclencheur mesuré ou évident** existe, jamais « au cas où », au plus une fois par sujet et par session.

Format (une ligne, pas de pavé) :

> 💡 **Reco perf (payante)** : *quoi* — gain : *mesuré ou estimé* — coût : *estimé, à vérifier* — déclencheur : *ce qui l'a provoquée*. Vous validez ?

Si le projet tient un journal de ses décisions (`docs/perf-decisions.md`), consignez-y la réponse (acceptée, refusée, reportée) et consultez-le avant de reproposer. Une reco refusée ne se représente que si son déclencheur change. Sans journal, signalez la décision dans votre réponse.

| Reco | Déclencheur typique | Avant de payer |
|------|---------------------|----------------|
| **Cache distribué (Redis)** | La même lecture lourde se répète, ou plusieurs réplicas doivent partager un cache | Essayez d'abord `HybridCache`/`IMemoryCache` (gratuit, TTL court) et A1 à A4. Clé préfixée par l'utilisateur ou le compte pour toute donnée personnelle ; repli sur la base si le cache est indisponible ; jamais de secret en cache. |
| **Monter le palier de la base** | Utilisation soutenue de la capacité de la base **après** application de A1 à A5 | Montrer la métrique et les requêtes coupables : un palier ne répare pas une lecture complète. |
| **Réplicas, autoscale** | Latence ou CPU saturés en charge réelle | A11 respectée ; toujours un maximum de réplicas pour plafonner la facture. |
| **Worker ou file de jobs** | Une tâche dépasse ~2 s et ne peut pas rester dans la requête (A6) | Le besoin d'abord : file en base ou service géré, coût, supervision, reprise après arrêt. |
| **CDN devant le stockage de blobs** | Fichiers ou images volumineux servis à beaucoup de clients | Jamais de cache CDN sur une réponse personnalisée. |
| **Résilience SQL (`EnableRetryOnFailure`)** | Erreurs transitoires observées sur la base | Impose `CreateExecutionStrategy` pour les transactions manuelles : changement de comportement à valider. |
| **Rendu serveur, mesure d'usage réel, contrôle de perf en CI** | Côté projet client uniquement | Hors périmètre de cette API. |

---

## C. Écarté ou non applicable (et pourquoi)

| Élément du guide | Décision |
|------------------|----------|
| Images, polices, lazy-loading, skeletons, debounce, re-renders, découpage du JS, rendu serveur | Front : hors périmètre de ce template. L'API fournit ce dont le front a besoin (pagination, filtres, tri, recherche bornés, A1). |
| Pooling de connexions | ADO.NET et EF poolent déjà. Seule règle utile : A11. |
| `Promise.all` générique | Remplacé par A8 : EF n'est pas thread-safe. |
| `CREATE INDEX CONCURRENTLY` | Propre à PostgreSQL ; ici `ONLINE = ON` (A4). |
| Tracing OpenTelemetry | Déjà fourni par `ServiceDefaults` quand Aspire est activé ; ne pas ajouter un second système. |
| Load balancer à configurer | L'hébergeur répartit déjà ; le seul sujet est le plafond de réplicas (§B). |
| Streaming de réponses IA | Aucune fonctionnalité IA dans ce template. Si elle arrive : flux, timeout, annulation. |
| Optimistic UI | Décision du client, pas de l'API ; l'API reste source de vérité. |

---

## D. Vérification (avant de dire « terminé »)

Pour toute feature qui touche une liste, une requête, un import ou un appel sortant :

1. **Aucune requête sans borne** : un test du validateur prouve que `PageSize` au-dessus du plafond est refusé (400) et que la borne exacte passe.
2. **Pas de N+1** : le nombre de requêtes SQL reste constant pour 10 ou 1 000 éléments (journal EF activé en local).
3. **Plan d'exécution** : recherche d'index (`Seek`), pas de lecture complète (`Scan`) sur une table qui grossit, vérifié sur la **vraie base SQL Server locale** (Aspire) avec un jeu réaliste (au moins 10 000 lignes). Les tests d'intégration utilisent EF InMemory : il n'applique **ni index ni traduction SQL**, il ne prouve donc rien sur la performance.
4. **Index** : présents dans `<Entity>Configuration` et dans la migration relue.
5. **Appels sortants** : timeout et annulation vérifiés (un appel volontairement lent s'arrête à l'échéance).
6. Une ligne « Perf : … » dans la réponse : ce qui a été vérifié, ce qui ne l'a pas été.
