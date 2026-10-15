# Check-list finale `add-feature`

Cochez chaque ligne avant de déclarer la feature terminée. Une ligne non applicable se justifie dans votre réponse.

## Couches

- [ ] Domain : identifiant `ValueObject` (refuse `Guid.Empty`), entité avec constantes de longueur, invariants dans `Create`, aucune dépendance technique.
- [ ] Application : record + validateur + handler par opération, dans `<Feature>/Commands|Queries/<Nom>/` ; `*Result` distinct de `*Response`.
- [ ] Application ne référence ni Infrastructure ni Api ; pas de `HttpContext` dans un handler (identité via `ICurrentUser`).
- [ ] Infrastructure : configuration EF (conversion de l'identifiant, longueurs, index unique si unicité), `DbSet`, repository, enregistrement `AddScoped` dans `DependencyInjection.cs`.
- [ ] Contracts : `*Request` et `*Response`, aucune entité exposée.
- [ ] Api : classe `*Controller` avec `Map*Endpoints`, appelée dans `Program.cs` avant `MapFallback`, aucune logique métier dans l'endpoint.

## Nommage

- [ ] Dossiers par feature au pluriel, `<Nom>Command`, `<Nom>CommandHandler`, `<Nom>CommandValidator`, `<Nom>Query`, `<Nom>QueryHandler`, `<Nom>Result`, `<Entity>Response`.
- [ ] Les namespaces suivent les dossiers ; `.WithName("<Nom>")` sur chaque route.

## Autorisation et débit

- [ ] Chaque route a `RequireAuthorization()` ou `RequireAuthorization(Policies.*)` explicite, conforme au modèle d'autorisation de `AGENTS.md` et à la demande.
- [ ] Chaque route a `RequireRateLimiting(RateLimitingPolicies.*)` si le projet a des politiques de débit (`Read` lecture, `Write` écriture).

## Validation et erreurs

- [ ] Chaque commande a un validateur (obligatoire, formats, longueurs avec les constantes du domaine).
- [ ] Chaque query à identifiant a un validateur `NotEmpty()` sur cet identifiant.
- [ ] Une liste paginée borne `page` et `pageSize` ; le tri est déterministe.
- [ ] Une règle d'unicité est vérifiée dans le handler (409 `CONFLICT` en Problem Details) et protégée par un index unique.
- [ ] Aucune exception lancée pour un cas attendu ; aucun message d'exception renvoyé au client.
- [ ] Les codes de retour correspondent au tableau du skill : 201/200, 400, 401, 403, 404, 409.

## Tests

- [ ] Validateur : cas valide, chaque règle violée, bornes exactes (longueur maximale acceptée, maximale + 1 refusée).
- [ ] Handler : cas nominal, cas de conflit, introuvable.
- [ ] Intégration : autorisé (2xx), refusé (403), anonyme (401), invalide (400 avec `code` `VALIDATION`), introuvable (404), doublon (409 avec `code` `CONFLICT`), liste.
- [ ] Les tests n'ont pas de dépendance à l'ordre d'exécution ni à une base vide (noms uniques).
- [ ] `dotnet test --solution Web.Template.CQRS.slnx` : tout est vert, aucun test supprimé ou ignoré.

## Migration

- [ ] Migration créée avec `dotnet ef migrations add`, relue (table, colonnes, index unique).
- [ ] `Microsoft.EntityFrameworkCore.Design` référencé dans l'Api si l'outil l'exige.
- [ ] Rien n'a été appliqué sur une base partagée sans accord.

## Documentation et revue

- [ ] Si une route est ajoutée, `src/Web.Template.CQRS.Api/Api.http` ou la documentation du projet est mise à jour lorsque le projet les utilise.
- [ ] Agent `reviewer` lancé, aucun constat bloquant restant.
