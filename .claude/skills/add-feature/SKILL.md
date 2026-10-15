---
name: add-feature
description: "Ajouter une fonctionnalité métier complète à l'API (entité, persistance EF, commande/query CQRS, validation, endpoint, autorisation, rate limiting, tests) en suivant les conventions du projet. À utiliser pour toute nouvelle route, ressource, endpoint de liste ou de lecture, règle d'unicité ou cas d'usage métier (« ajouter une ressource », « créer un endpoint », « nouvelle feature »)."
---

# Ajouter une feature

Tranche verticale : Domain, Infrastructure, Application, Contracts, Api, tests. Les conventions sont dans `AGENTS.md`. Les exemples de code complets sont dans [references/code-patterns.md](references/code-patterns.md) : lisez-le avant d'écrire du code et recopiez-le plutôt que de réinventer. La check-list finale est dans [references/checklist.md](references/checklist.md).

Dans ce skill, `<Feature>` est le nom pluriel du dossier (`Products`), `<Entity>` le nom singulier (`Product`). Les chemins s'écrivent avec `Web.Template.CQRS` : c'est le nom de votre projet.

## Étapes

1. **Comprendre le besoin.** Lisez `project-profile.json` (racine) s'il existe, et la section « Modèle d'autorisation » de `AGENTS.md`. Établissez : la ressource et ses champs (types, longueurs, formats), les opérations (créer, lire un, lister, modifier, supprimer), les règles métier (unicité, bornes), qui a le droit de quoi, le quota. Posez les questions manquantes à l'utilisateur ; ne devinez pas une règle d'accès.
2. **Choisir les codes de retour** avant de coder (tableau plus bas), puis **annoncer un plan court** : liste des fichiers à créer et à modifier.
3. **Domain.** Dans `src/Web.Template.CQRS.Domain/<Feature>/` : `<Entity>Id` (`ValueObject`, refuse `Guid.Empty`) et l'entité (`AggregateRoot<<Entity>Id>`) avec constantes de longueur (`NameMaxLength`), constructeur privé, constructeur vide privé pour EF, fabrique `Create` qui vérifie les invariants.
4. **Infrastructure.**
   - `src/Web.Template.CQRS.Application/Common/Interfaces/Persistence/I<Entity>Repository.cs` : hérite `IRepository<<Entity>>` ; y déclarer les requêtes spécifiques (existence, page).
   - `src/Web.Template.CQRS.Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` : table, clé avec `HasConversion` de l'identifiant, longueurs, **index unique** si une valeur doit être unique.
   - `DbSet<<Entity>>` dans `src/Web.Template.CQRS.Infrastructure/Persistence/ProjectDbContext.cs`.
   - `src/Web.Template.CQRS.Infrastructure/Persistence/Repositories/<Entity>Repository.cs` : hérite `BaseRepository<<Entity>, ProjectDbContext>`.
   - `services.AddScoped<I<Entity>Repository, <Entity>Repository>();` dans `src/Web.Template.CQRS.Infrastructure/DependencyInjection.cs` (avec les `using`).
5. **Migration.** Une fois la solution compilée :
   ```bash
   dotnet tool update --global dotnet-ef
   dotnet ef migrations add <Nom> --project src/Web.Template.CQRS.Infrastructure --startup-project src/Web.Template.CQRS.Api --output-dir Persistence/Migrations
   ```
   (si l'outil n'est pas installé : `dotnet tool install --global dotnet-ef`.) Si l'outil répond que le projet de démarrage ne référence pas `Microsoft.EntityFrameworkCore.Design`, ajoutez dans `src/Web.Template.CQRS.Api/Web.Template.CQRS.Api.csproj`, dans l'`ItemGroup` des `PackageReference` (la version est déjà dans `Directory.Packages.props` : n'écrivez aucun `Version`) :
   ```xml
   <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
       <PrivateAssets>all</PrivateAssets>
       <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
   </PackageReference>
   ```
   Ne définissez jamais `ASPNETCORE_ENVIRONMENT=Development` dans le shell pour `dotnet ef` : l'outil exécute `Program` à la conception, donc le bloc `Database.Migrate()` du mode `Development` tenterait de migrer une base réelle.
   Relisez la méthode `Up` de la migration générée : table, colonnes, index unique. Ne l'appliquez pas à une base partagée sans accord (voir « Application de la migration »).
6. **Application.** Dans `src/Web.Template.CQRS.Application/<Feature>/` : `Common/<Entity>Result.cs`, puis par opération `Commands|Queries/<Nom>/` avec le record, son **validateur** et son handler. Toute query qui prend un identifiant a un validateur `NotEmpty()` sur cet identifiant (sinon `new <Entity>Id(Guid.Empty)` lève une exception, donc une 500). Un échec métier attendu (doublon, état interdit) n'est **pas** une exception : le handler le renvoie dans son résultat (voir « Conflits et unicité »).
7. **Contrats.** Dans `src/Web.Template.CQRS.Contracts/<Feature>/` : `Create<Entity>Request` et `<Entity>Response` (records). Ne renvoyez jamais l'entité ni le `*Result` tel quel.
8. **Endpoint.** `src/Web.Template.CQRS.Api/Controllers/<Feature>Controller.cs` (classe statique, méthode `Map<Entity>Endpoints`), puis `app.Map<Entity>Endpoints();` dans `src/Web.Template.CQRS.Api/Program.cs`, **avant** `app.MapFallback(...)`. Sur chaque route : `.WithName(...)`, `.RequireAuthorization(...)` explicite, et `RequireRateLimiting(RateLimitingPolicies.*)` si ce type existe dans le projet (`Read` pour les lectures, `Write` pour les écritures). Le corps ne contient que : envoyer à `IMediator`, traduire le résultat en `TypedResults`/`Results`.
9. **Tests** (si le dossier `tests/` existe).
   - Unitaires (`tests/Web.Template.CQRS.Application.UnitTests/<Feature>/`) : validateur (valide, chaque règle violée, bornes exactes), handler avec un faux repository, y compris le cas de conflit. Un faux repository va dans `tests/Web.Template.CQRS.Application.UnitTests/Fakes/`.
   - Intégration (`tests/Web.Template.CQRS.Api.IntegrationTests/<Feature>/`, `ApiFactory`, `AsUser`) : cas passant, 403, 401, 400, 404, 409 selon les routes.
10. **Build et tests.**
    ```bash
    dotnet build Web.Template.CQRS.slnx
    dotnet test --solution Web.Template.CQRS.slnx
    ```
    Corrigez la cause d'un échec ; ne supprimez ni n'assouplissez un test pour le faire passer. Pour cibler : `dotnet test --solution Web.Template.CQRS.slnx --filter-method "*Product*"`.
11. **Vérification live (facultative)** avec le skill `run-local`, si le projet a Aspire : créer avec le persona admin, relire avec le persona user, constater 403 puis 401.
12. **Relecture.** Lancez l'agent `reviewer` sur le diff, corrigez les constats bloquants, relancez l'étape 10.

## Choisir le code de retour

| Situation | Code | Comment |
|-----------|------|---------|
| Création réussie | 201 | `TypedResults.Created("/<route>/{id}", response)` |
| Lecture ou liste réussie | 200 | `Results.Ok(...)` ; une liste vide est un 200 avec `[]` |
| Format invalide, champ manquant, trop long, page hors bornes | 400 | validateur FluentValidation, automatique : Problem Details `code: VALIDATION` |
| Non authentifié | 401 | automatique (`RequireAuthorization`) |
| Authentifié sans le droit | 403 | automatique (policy) |
| Ressource inconnue | 404 | le handler renvoie `null`, l'endpoint `Results.NotFound()` (devient Problem Details) |
| Doublon, état incompatible avec la demande | 409 | résultat du handler, puis `Results.Problem(statusCode: 409, ...)` avec `code: CONFLICT` |

Règle : **400** si la requête est mauvaise en soi (vérifiable sans lire la base) ; **409** si elle est correcte mais en conflit avec l'état des données (unicité). Ne lancez jamais d'exception pour un 409 : le gestionnaire global en ferait une 500 `INTERNAL`.

## Conflits et unicité

Une unicité se garantit à deux niveaux :

1. Le handler vérifie l'existence via une méthode du repository (`ExistsBy...Async`) et renvoie un résultat « conflit ».
2. Un **index unique** dans la configuration EF protège contre la concurrence en base.

Le niveau 1 est indispensable : les tests d'intégration utilisent EF InMemory, qui **n'applique pas** les index uniques. Normalisez la valeur (espaces de bord, tirets d'un ISBN, casse si pertinent) avant de la comparer et de la stocker. Code complet : section « Unicité et conflit 409 » de [references/code-patterns.md](references/code-patterns.md).

## Listes

Un endpoint de liste est une query (`IQuery<IReadOnlyList<...>>`) sur `GET /<route>`. Triez toujours de façon déterministe. Si la collection peut croître, paginez (`page`, `pageSize` bornés par le validateur : 400 hors bornes) ; sinon `GetAllAsync()` suffit. Modèle dans `code-patterns.md`, section « Liste ».

## Autorisation

Reprenez le modèle de `AGENTS.md`.

- Écriture réservée aux administrateurs : `.RequireAuthorization(Policies.RequireAdmin)`. La policy n'existe qu'avec des rôles ou des groupes.
- Lecture pour tout utilisateur connecté : `.RequireAuthorization()` sans policy.
- Modèle « utilisateur authentifié uniquement » : pas de policy, donc pas de 403. Utilisez `.RequireAuthorization()` seul sur toutes les routes, **supprimez** des modèles de `code-patterns.md` les tests de rôle/403 et tout `Policies.RequireAdmin`, `AppRoles` ou `roles:`/`groups:`. Dites à l'utilisateur que la distinction admin/utilisateur n'existe pas dans ce modèle et proposez le skill `manage-access`.
- Dans les tests : avec rôles `AsUser("id", roles: [AppRoles.Admin])`, avec groupes `AsUser("id", groups: [ApiFactory.AdministratorsGroupId])`.

## Application de la migration

Avec Aspire (base SQL Server locale fournie), ajoutez dans `Program.cs`, après `app.MapFallback(...)` et avant `app.Run()`, le bloc `Database.Migrate()` gardé par `app.Environment.IsDevelopment()` (code dans `code-patterns.md`), avec ses deux `using`. Ajoutez-le une seule fois (vérifiez qu'il n'existe pas déjà). Les tests (environnement `Testing`) ne migrent pas. En production, ne migrez pas au démarrage : générez un script idempotent (`dotnet ef migrations script --idempotent`). Sans Aspire, aucune base locale n'est fournie : n'ajoutez pas le bloc et signalez-le à l'utilisateur.

## Terminé quand

- `dotnet build` sans erreur et `dotnet test` entièrement vert.
- Les nouveaux tests couvrent les cas 2xx/400/401/403/404/409 pertinents pour les routes ajoutées.
- Chaque route a une autorisation explicite et, si disponible, une politique de rate limiting.
- Une migration existe et a été relue.
- La revue de l'agent `reviewer` n'a plus de constat bloquant.

## Ne jamais faire sans demander

- Changer le modèle d'autorisation, ou modifier une policy existante (voir `manage-access`).
- Supprimer ou renommer des routes existantes, ou changer leur contrat.
- Modifier le realm Keycloak (voir `manage-access`).
- Appliquer une migration sur une base partagée.
- Désactiver une validation, une autorisation ou un test.
