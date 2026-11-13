---
name: add-feature
description: "Ajouter une fonctionnalité métier complète à l'API (entité, persistance EF, commande/query CQRS, validation, endpoint, autorisation, rate limiting, tests) en suivant les conventions du projet. À utiliser pour toute nouvelle route, ressource, endpoint de liste ou de lecture, règle d'unicité ou cas d'usage métier (« ajouter une ressource », « créer un endpoint », « nouvelle feature »)."
---

# Ajouter une feature

Tranche verticale : Domain, Infrastructure, Application, Contracts, Api, tests. Les conventions sont dans `AGENTS.md`. Les exemples de code complets sont dans [references/code-patterns.md](references/code-patterns.md) : lisez-le avant d'écrire du code et recopiez-le plutôt que de réinventer. La check-list finale est dans [references/checklist.md](references/checklist.md).

Dans ce skill, `<Feature>` est le nom pluriel du dossier (`Products`), `<Entity>` le nom singulier (`Product`), `<Nom>` le nom de l'opération (`CreateProduct`). Les chemins s'écrivent avec `Web.Template.CQRS` : c'est le nom de votre projet.

Si le projet a des tests (`tests/`), le travail suit un cycle TDD : les tests d'abord, vus rouges ; puis le code, couche par couche, jusqu'au vert ; puis la mutation, pour vérifier que les tests attrapent les régressions.

## Étapes

1. **Comprendre le besoin.** Lisez `project-profile.json` (racine) s'il existe, et la section « Modèle d'autorisation » de `AGENTS.md`. Établissez : la ressource et ses champs (types, longueurs, formats), les opérations (créer, lire un, lister, modifier, supprimer), les règles métier (unicité, bornes), qui a le droit de quoi, le quota. Posez les questions manquantes à l'utilisateur ; ne devinez pas une règle d'accès.
2. **Choisir les codes de retour** avant de coder (tableau plus bas), puis **annoncer un plan court** : liste des fichiers à créer et à modifier et, si `tests/` existe, liste des tests à écrire (cas 2xx, 400, 401, 403, 404, 409 selon les routes ; règles du validateur ; bornes ; invariants du domaine).
3. **Tests d'abord, rouges** (si le dossier `tests/` existe). Écrivez les tests du plan avant tout code de production :
   - Unitaires (`tests/Web.Template.CQRS.Application.UnitTests/<Feature>/`) : validateur (valide, chaque règle violée, bornes exactes : longueur maximale acceptée, maximale + 1 refusée), handler avec un faux repository (cas nominal, introuvable, conflit), invariants du domaine. Un faux repository va dans `tests/Web.Template.CQRS.Application.UnitTests/Fakes/`.
   - Intégration (`tests/Web.Template.CQRS.Api.IntegrationTests/<Feature>/`, `ApiFactory`, `AsUser`) : cas passant, 403, 401, 400, 404, 409 selon les routes.

   Les types n'existent pas encore : créez-les en squelettes (signatures, corps `throw new NotImplementedException();`) pour que la solution compile. Lancez ensuite les tests de la feature, par exemple `dotnet test --solution Web.Template.CQRS.slnx --filter-class "*<Entity>*"`, et constatez qu'ils échouent pour la bonne raison : un échec d'assertion ou `NotImplementedException`, pas une erreur de compilation. Un nouveau test qui passe déjà ne prouve rien : revoyez-le avant de coder.
4. **Implémentation par couche, jusqu'au vert.** Ordre : domaine, infrastructure, application, contrats, endpoint. Si `tests/` existe, après chaque couche, lancez les tests de cette couche et corrigez le code : invariants après le domaine, validateurs et handlers après l'application, tests d'intégration une fois l'endpoint en place (ils ne peuvent pas passer avant). Un test ne se modifie que s'il est faux, par exemple un squelette mal deviné : dans ce cas, dites-le dans la réponse. Une fois l'endpoint en place, toute la feature doit être verte.
   - **Domain.** Dans `src/Web.Template.CQRS.Domain/<Feature>/` : `<Entity>Id` (`ValueObject`, refuse `Guid.Empty`) et l'entité (`AggregateRoot<<Entity>Id>`) avec constantes de longueur (`NameMaxLength`), constructeur privé, constructeur vide privé pour EF, fabrique `Create` qui vérifie les invariants.
   - **Infrastructure.**
     - `src/Web.Template.CQRS.Application/Common/Interfaces/Persistence/I<Entity>Repository.cs` : hérite `IRepository<<Entity>>` ; y déclarer les requêtes spécifiques (existence, page).
     - `src/Web.Template.CQRS.Infrastructure/Persistence/Configurations/<Entity>Configuration.cs` : table, clé avec `HasConversion` de l'identifiant, longueurs, **index unique** si une valeur doit être unique.
     - `DbSet<<Entity>>` dans `src/Web.Template.CQRS.Infrastructure/Persistence/ProjectDbContext.cs`.
     - `src/Web.Template.CQRS.Infrastructure/Persistence/Repositories/<Entity>Repository.cs` : hérite `BaseRepository<<Entity>, ProjectDbContext>`.
     - `services.AddScoped<I<Entity>Repository, <Entity>Repository>();` dans `src/Web.Template.CQRS.Infrastructure/DependencyInjection.cs` (avec les `using`).
   - **Application.** Dans `src/Web.Template.CQRS.Application/<Feature>/` : `Common/<Entity>Result.cs`, puis par opération `Commands|Queries/<Nom>/` avec le record, son **validateur** et son handler. Toute query qui prend un identifiant a un validateur `NotEmpty()` sur cet identifiant (sinon `new <Entity>Id(Guid.Empty)` lève une exception, donc une 500). Un échec métier attendu (doublon, état interdit) n'est **pas** une exception : le handler le renvoie dans son résultat (voir « Conflits et unicité »).
   - **Contrats.** Dans `src/Web.Template.CQRS.Contracts/<Feature>/` : `Create<Entity>Request` et `<Entity>Response` (records). Ne renvoyez jamais l'entité ni le `*Result` tel quel.
   - **Endpoint.** `src/Web.Template.CQRS.Api/Controllers/<Feature>Controller.cs` (classe statique, méthode `Map<Entity>Endpoints`), puis `app.Map<Entity>Endpoints();` dans `src/Web.Template.CQRS.Api/Program.cs`, **avant** `app.MapFallback(...)`. Sur chaque route : `.WithName(...)`, `.RequireAuthorization(...)` explicite, et `RequireRateLimiting(RateLimitingPolicies.*)` si ce type existe dans le projet (`Read` pour les lectures, `Write` pour les écritures). Le corps ne contient que : envoyer à `IMediator`, traduire le résultat en `TypedResults`/`Results`.
5. **Migration.** Une fois la solution compilée :
   ```bash
   dotnet tool update --global dotnet-ef --version 10.0.15
   dotnet ef migrations add <Nom> --project src/Web.Template.CQRS.Infrastructure --startup-project src/Web.Template.CQRS.Api --output-dir Persistence/Migrations
   ```
   (si l'outil n'est pas installé : `dotnet tool install --global dotnet-ef --version 10.0.15`.) Gardez l'outil EF Core au même numéro majeur et à la même version que les paquets EF du projet. Si l'outil répond que le projet de démarrage ne référence pas `Microsoft.EntityFrameworkCore.Design`, ajoutez dans `src/Web.Template.CQRS.Api/Web.Template.CQRS.Api.csproj`, dans l'`ItemGroup` des `PackageReference` (la version est déjà dans `Directory.Packages.props` : n'écrivez aucun `Version`) :
   ```xml
   <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
       <PrivateAssets>all</PrivateAssets>
       <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
   </PackageReference>
   ```
   Ne définissez jamais `ASPNETCORE_ENVIRONMENT=Development` dans le shell pour `dotnet ef` : l'outil exécute `Program` à la conception, donc le bloc `Database.Migrate()` du mode `Development` tenterait de migrer une base réelle.
   Relisez la méthode `Up` de la migration générée : table, colonnes, index unique. Ne l'appliquez pas à une base partagée sans accord (voir « Application de la migration »).
6. **Build et tests.**
   ```bash
   dotnet build Web.Template.CQRS.slnx
   dotnet test --solution Web.Template.CQRS.slnx
   ```
   Corrigez la cause d'un échec ; ne supprimez ni n'assouplissez un test pour le faire passer. Pour cibler : `dotnet test --solution Web.Template.CQRS.slnx --filter-method "*Product*"`.
7. **Mutation** (si le dossier `tests/` existe). Stryker.NET modifie le code de la feature (inverse une condition, retire un appel, change une chaîne) et vérifie qu'un test échoue à chaque fois. Un mutant qui survit est un comportement que les tests ne vérifient pas.

   Installez l'outil une fois, comme `dotnet-ef` à l'étape 5 : `dotnet tool install --global dotnet-stryker --version 5.1.0` (ou `dotnet tool update --global dotnet-stryker --version 5.1.0` s'il est déjà installé). Puis lancez-le sur les fichiers métier du plan, un `-m` par fichier :
   ```bash
   dotnet stryker -s Web.Template.CQRS.slnx -t mtp -r json -r html -m '**/<Nom>Command.cs' -m '**/<Nom>CommandHandler.cs' -m '**/<Nom>CommandValidator.cs' -m '**/<Entity>.cs' -m '**/<Entity>Id.cs' -m '**/<Entity>Repository.cs' -m '**/<Feature>Controller.cs'
   ```
   Remplacez les placeholders (`<Nom>` vaut par exemple `CreateProduct`) et ne gardez que les fichiers réellement créés par le plan. Pour une query, remplacez `Command` par `Query` dans les noms (`<Nom>QueryHandler.cs`, `<Nom>QueryValidator.cs`). Règles :
   - Mutez uniquement le code métier de la feature : entité et identifiant du domaine, validateurs, handlers, repository (couvert par les tests d'intégration InMemory), contrôleur. Ne mutez pas `DependencyInjection.cs`, `Program.cs`, les configurations EF ni les migrations : leurs mutants sont surtout du câblage, peu informatif et souvent sans test qui les touche.
   - Ne lancez pas Stryker sans `-m` pendant le travail sur une feature : sur toute la solution, c'est nettement plus lent.
   - Ouvrez le rapport `StrykerOutput/*/reports/mutation-report.json` (ou le rapport HTML) et listez les mutants `Survived` et `NoCoverage` : fichier, ligne, mutateur.
   - Pour chaque survivant : soit un test qui le tue (un test du comportement, qui échoue sur le mutant et passe sur le code réel), soit une justification écrite dans votre réponse (mutant équivalent, message de log, etc.). N'ajoutez pas de test seulement pour faire monter le score.
   - Relancez sur le même périmètre, au plus deux fois.
   - Limite (vérifiée avec Stryker.NET 5.1.0) : Stryker ne mute pas les littéraux passés à FluentValidation (`MaximumLength(50)`, `GreaterThan(0m)`). Les bornes sont donc couvertes par les tests explicites de l'étape 3, pas par la mutation.
8. **Vérification live (facultative)** avec le skill `run-local`, si le projet a Aspire : créer avec le persona admin, relire avec le persona user, constater 403 puis 401.
9. **Relecture.** Lancez l'agent `reviewer` sur le diff, corrigez les constats bloquants, relancez les étapes 6 et 7.

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
- Si `tests/` existe : les tests ont été écrits avant le code et vus rouges à l'étape 3, pour la bonne raison, et aucun `NotImplementedException` ne subsiste dans `src/`.
- Si `tests/` existe : les nouveaux tests couvrent les cas 2xx/400/401/403/404/409 pertinents pour les routes ajoutées, et les bornes sont testées explicitement.
- Si `tests/` existe : la mutation (étape 7) a été lancée sur les fichiers de la feature ; chaque survivant est traité par un test ou justifié dans la réponse.
- Chaque route a une autorisation explicite et, si disponible, une politique de rate limiting.
- Une migration existe et a été relue.
- La revue de l'agent `reviewer` n'a plus de constat bloquant.

## Ne jamais faire sans demander

- Changer le modèle d'autorisation, ou modifier une policy existante (voir `manage-access`).
- Supprimer ou renommer des routes existantes, ou changer leur contrat.
- Modifier le realm Keycloak (voir `manage-access`).
- Appliquer une migration sur une base partagée.
- Désactiver une validation, une autorisation ou un test.
