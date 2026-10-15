# Erreurs et Problem Details

Cette page explique ce que reçoit un client quand quelque chose se passe mal, et pourquoi le template répond toujours dans le même format.

## Le concept simplement

Quand une API échoue, le client a besoin de savoir **quoi** (quel type d'erreur) et **quoi faire** (corriger sa requête, réessayer plus tard, contacter le support). Sans convention, chaque API invente son format : un champ `message` ici, `error` là, du texte brut ailleurs.

La **RFC 9457** (*Problem Details for HTTP APIs*) standardise ce format. Une erreur est un objet JSON, servi avec le type de contenu `application/problem+json`, dont les champs de base sont :

| Champ | Signification |
|-------|---------------|
| `status` | le code HTTP, répété dans le corps |
| `title` | un résumé court de l'erreur |
| `type` | une URI qui identifie la catégorie d'erreur (peut être ajoutée automatiquement par ASP.NET Core) |
| `detail` | une explication propre à cette occurrence (le template ne l'utilise pas, voir plus bas) |

Le format est **extensible** : on peut ajouter des champs. Le template en ajoute deux : `code` (un identifiant stable, lisible par un programme) et `traceId` (l'identifiant de la requête).

## Pourquoi c'est important

- **Un seul traitement côté client** : un client qui sait lire un Problem Details sait lire toutes les erreurs de l'API, de la validation au plantage.
- **Un `code` stable** : le `title` est un texte pour les humains qui peut être reformulé. Le `code` (`VALIDATION`, `INTERNAL`, `RATE_LIMITED`) est fait pour les `if` du client.
- **Le support peut retrouver la cause** : le `traceId` relie la réponse reçue par l'utilisateur aux logs et aux traces côté serveur.
- **Aucune fuite d'information** : une erreur inattendue ne montre jamais le message de l'exception, qui peut contenir des chemins, des requêtes SQL ou des secrets.

## Comment le template le fait

### Le gestionnaire d'erreurs global

Un seul endroit transforme les exceptions en réponses : `UseErrorHandling`, installé en **tout premier** dans le pipeline HTTP de `Program.cs` (`app.UseErrorHandling();`). Il enveloppe ainsi tout ce qui s'exécute après lui.

**Fichier :** `src/Web.Template.CQRS.Api/Errors/ErrorHandling.cs`

```csharp
public static class ErrorHandling
{
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder builder)
    {
        return builder.UseExceptionHandler(exceptionHandlerApp =>
            exceptionHandlerApp.Run(async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GlobalExceptionHandler");

                if (exception is ValidationException validationException)
                {
                    var errors = validationException.Errors
                        .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Select(error => error.ErrorMessage).ToArray(),
                            StringComparer.Ordinal);

                    await Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "One or more validation errors occurred.",
                        extensions: new Dictionary<string, object?>
                        {
                            ["code"] = "VALIDATION",
                            ["traceId"] = traceId,
                            ["errors"] = errors
                        }).ExecuteAsync(context);

                    return;
                }

                logger.LogError(exception, "An unhandled exception occurred for trace {TraceId}.", traceId);

                await Results.Problem(
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "An unexpected error occurred.",
                    extensions: new Dictionary<string, object?>
                    {
                        ["code"] = "INTERNAL",
                        ["traceId"] = traceId
                    }).ExecuteAsync(context);
            }));
    }
}
```

Le code distingue deux cas :

1. **Une `ValidationException`** (levée par le [comportement de validation](/Architecture/Validation.md)) devient une réponse 400 avec le code `VALIDATION` et un objet `errors` qui regroupe les messages par nom de propriété.
2. **Toute autre exception** est journalisée avec `LogError` (exception complète, `traceId` en paramètre), puis devient une réponse 500 avec le code `INTERNAL` et un titre générique.

### Les trois codes

| Code | Statut | Quand | Où c'est produit |
|------|--------|-------|------------------|
| `VALIDATION` | 400 | une commande ou une query ne respecte pas ses règles | `ErrorHandling.cs` |
| `INTERNAL` | 500 | exception non prévue | `ErrorHandling.cs` |
| `RATE_LIMITED` | 429 | quota de requêtes dépassé | `RateLimiting.cs` (si l'option de limitation de débit est activée) |

Le code `RATE_LIMITED` est produit au moment où une requête est rejetée par le limiteur, avec en plus l'en-tête `Retry-After` quand la durée d'attente est connue :

**Fichier :** `src/Web.Template.CQRS.Api/Common/RateLimiting/RateLimiting.cs`

```csharp
options.OnRejected = async (rejection, cancellationToken) =>
{
    var context = rejection.HttpContext;
    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

    if (rejection.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
    {
        context.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
            .ToString(CultureInfo.InvariantCulture);
    }

    await Results.Problem(
        statusCode: StatusCodes.Status429TooManyRequests,
        title: "Too many requests.",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = "RATE_LIMITED",
            ["traceId"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier
        }).ExecuteAsync(context);
};
```

Le fonctionnement du limiteur est détaillé dans la page [Rate limiting](/Fonctionnalités/Rate-limiting.md).

### Exemples de réponses

Les exemples ci-dessous montrent les champs que le code du template ajoute. ASP.NET Core peut y ajouter d'autres champs standard, comme `type`.

**400 VALIDATION**


```json
{
  "status": 400,
  "title": "One or more validation errors occurred.",
  "code": "VALIDATION",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "errors": {
    "Name": ["'Name' must not be empty."]
  }
}
```

**500 INTERNAL**


```json
{
  "status": 500,
  "title": "An unexpected error occurred.",
  "code": "INTERNAL",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

**429 RATE_LIMITED**


```json
{
  "status": 429,
  "title": "Too many requests.",
  "code": "RATE_LIMITED",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

### Le `traceId`

Chaque réponse d'erreur contient un `traceId`. Il vaut l'identifiant de la trace OpenTelemetry en cours (`Activity.Current`), ou à défaut l'identifiant de requête d'ASP.NET Core. Pour que **toutes** les réponses Problem Details en portent un, y compris celles que le gestionnaire ci-dessus ne produit pas (comme un 404 ou un 401 sans corps), `Program.cs` personnalise le service :

**Fichier :** `src/Web.Template.CQRS.Api/Program.cs`

```csharp
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
    };
});
```

Quand un utilisateur vous signale une erreur, demandez-lui ce `traceId` : vous le recherchez ensuite dans les logs et les traces. Voir [Observabilité](/Fonctionnalités/Observabilité.md).

### Ne jamais exposer le message d'exception

Le message d'une exception est écrit pour le développeur, pas pour le client : il peut révéler une table, un chemin de fichier, une chaîne de connexion. Le template garde donc ce message **dans les logs** et renvoie au client un titre fixe.

Un test de non-régression protège cette règle. Il fait lever `InvalidOperationException("Sensitive internal detail")` par un endpoint, puis vérifie que la réponse porte le code `INTERNAL` et que son corps ne contient pas cette phrase :

**Fichier :** `tests/Web.Template.CQRS.Api.IntegrationTests/Errors/ErrorHandlingTests.cs`

```csharp
public async Task UnexpectedException_Returns500WithoutInternalDetails()
{
    using var host = await StartHostAsync();
    var cancellationToken = TestContext.Current.CancellationToken;

    var response = await host.GetTestClient().GetAsync("/boom", cancellationToken);

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    using var problem = JsonDocument.Parse(body);
    Assert.Equal("INTERNAL", problem.RootElement.GetProperty("code").GetString());
    Assert.DoesNotContain("Sensitive internal detail", body);
```

> ⚠️ **À ne pas faire**
>
> N'ajoutez jamais `exception.Message` ni `exception.ToString()` dans les `extensions` ou le `detail` d'une réponse, même « juste pour déboguer ». Utilisez le `traceId` et consultez les logs.

> ℹ️ **Les lignes `#if` du dépôt**
>
> Dans le dépôt template, `Program.cs` contient des directives `#if` absentes de ces extraits, et le fichier `RateLimiting.cs` n'est conservé dans un projet généré que si l'option de limitation de débit est activée. Seules les branches correspondant à vos options subsistent.

## À essayer

1. Appelez une route qui n'existe pas (par exemple `GET /nimporte-quoi`) avec `curl -i`. Quel statut et quel type de contenu obtenez-vous ?
2. Lisez `ErrorHandlingTests.cs` : combien de cas de test y a-t-il, et que vérifie chacun ?
3. Lancez `dotnet test --project tests/Web.Template.CQRS.Api.IntegrationTests --filter-class "*ErrorHandlingTests"`.
4. Pour une future erreur « commande introuvable » (404), quel `code` proposeriez-vous, et où dans le code l'ajouteriez-vous ?

## Pour aller plus loin

- [RFC 9457, Problem Details for HTTP APIs](https://www.rfc-editor.org/rfc/rfc9457)
- [Gérer les erreurs dans les API ASP.NET Core (Microsoft Learn)](https://learn.microsoft.com/aspnet/core/fundamentals/error-handling-api)
