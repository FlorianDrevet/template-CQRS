# Modèles de code pour `add-feature`

Exemple fil rouge : la ressource `Product` (nom unique, prix). Remplacez `Product`/`Products`/`Name`/`Price` par votre ressource. Ces modèles viennent du tutoriel https://floriandrevet.github.io/template-CQRS/guides/ajouter-une-feature/, appliqué à un projet généré (build OK, tests verts, migration créée), plus les cas d'unicité, de conflit et de liste. `Web.Template.CQRS` est le nom de votre projet.

Pièges vérifiés :

- Un validateur sur l'identifiant (`NotEmpty()`) est obligatoire pour les queries à identifiant : sans lui, `new ProductId(Guid.Empty)` lève une exception du domaine et le client reçoit une 500 au lieu d'une 400.
- Le domaine lève des exceptions quand un invariant est violé : ce n'est pas un moyen de répondre 400 ou 409. Le validateur (400) et la vérification d'unicité du handler (409) passent avant.
- Les handlers retournent `ValueTask`. Dans les tests, le jeton vient de `TestContext.Current.CancellationToken`.
- `Database.Migrate()` n'est appelé qu'en `Development` : les tests (`Testing`) ne migrent pas et utilisent EF InMemory, qui ignore les index uniques.
- Dans un endpoint qui peut renvoyer plusieurs types (201 ou 409), utilisez `Results.*` (type `IResult`) pour toutes les branches : mélanger `TypedResults.Created` et `Results.Problem` ne compile pas.

## 1. Domain

```csharp
// src/Web.Template.CQRS.Domain/Products/ProductId.cs
using Web.Template.CQRS.Domain.Common.Models;

namespace Web.Template.CQRS.Domain.Products;

public sealed class ProductId : ValueObject
{
    public ProductId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A product identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static ProductId CreateNew() => new(Guid.NewGuid());

    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

```csharp
// src/Web.Template.CQRS.Domain/Products/Product.cs
using Web.Template.CQRS.Domain.Common.Models;

namespace Web.Template.CQRS.Domain.Products;

public sealed class Product : AggregateRoot<ProductId>
{
    public const int NameMaxLength = 200;

    private Product(ProductId id, string name, decimal price)
        : base(id)
    {
        Name = name;
        Price = price;
    }

    // Constructeur sans paramètre réservé à EF Core.
    private Product()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public static Product Create(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A product name is required.", nameof(name));
        }

        if (name.Length > NameMaxLength)
        {
            throw new ArgumentException($"A product name cannot exceed {NameMaxLength} characters.", nameof(name));
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "A product price cannot be negative.");
        }

        return new Product(ProductId.CreateNew(), name.Trim(), price);
    }
}
```

## 2. Persistance

Interface (dans Application). `IRepository<T>` fournit déjà `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync` ; déclarez ici seulement les requêtes spécifiques.

```csharp
// src/Web.Template.CQRS.Application/Common/Interfaces/Persistence/IProductRepository.cs
using Web.Template.CQRS.Domain.Products;

namespace Web.Template.CQRS.Application.Common.Interfaces.Persistence;

public interface IProductRepository : IRepository<Product>
{
    Task<bool> ExistsByNameAsync(string name);

    Task<IReadOnlyList<Product>> ListAsync(int skip, int take);
}
```

Configuration EF (découverte automatique par `ApplyConfigurationsFromAssembly`), avec index unique :

```csharp
// src/Web.Template.CQRS.Infrastructure/Persistence/Configurations/ProductConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Web.Template.CQRS.Domain.Products;

namespace Web.Template.CQRS.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(product => product.Id);
        builder.Property(product => product.Id)
            .ValueGeneratedNever()
            .HasConversion(id => id.Value, value => new ProductId(value));

        builder.Property(product => product.Name)
            .IsRequired()
            .HasMaxLength(Product.NameMaxLength);

        builder.HasIndex(product => product.Name).IsUnique();

        builder.Property(product => product.Price)
            .HasPrecision(18, 2);
    }
}
```

`DbSet` dans `ProjectDbContext` (ajoutez la propriété et le `using Web.Template.CQRS.Domain.Products;`) :

```csharp
public DbSet<Product> Products => Set<Product>();
```

Repository (hérite `BaseRepository`, qui expose `Context`) :

```csharp
// src/Web.Template.CQRS.Infrastructure/Persistence/Repositories/ProductRepository.cs
using Microsoft.EntityFrameworkCore;
using Web.Template.CQRS.Application.Common.Interfaces.Persistence;
using Web.Template.CQRS.Domain.Products;

namespace Web.Template.CQRS.Infrastructure.Persistence.Repositories;

public sealed class ProductRepository(ProjectDbContext context)
    : BaseRepository<Product, ProjectDbContext>(context), IProductRepository
{
    public Task<bool> ExistsByNameAsync(string name) =>
        Context.Set<Product>().AnyAsync(product => product.Name == name);

    public async Task<IReadOnlyList<Product>> ListAsync(int skip, int take) =>
        await Context.Set<Product>()
            .OrderBy(product => product.Name) // colonne comparable ; pas l'Id (ValueObject)
            .Skip(skip)
            .Take(take)
            .ToListAsync();
}
```

Enregistrement, dans `AddInfrastructure` de `src/Web.Template.CQRS.Infrastructure/DependencyInjection.cs`, avant `services.AddSingleton<IDateTimeProvider, DateTimeProvider>();` :

```csharp
services.AddScoped<IProductRepository, ProductRepository>();
```

avec les `using Web.Template.CQRS.Application.Common.Interfaces.Persistence;` et `using Web.Template.CQRS.Infrastructure.Persistence.Repositories;`.

## 3. Migration

```bash
dotnet ef migrations add AddProducts --project src/Web.Template.CQRS.Infrastructure --startup-project src/Web.Template.CQRS.Api --output-dir Persistence/Migrations
```

Application locale au démarrage (Aspire, `Development` seulement). Dans `Program.cs`, ajoutez `using Web.Template.CQRS.Infrastructure.Persistence;` et `using Microsoft.EntityFrameworkCore;` en tête, puis, après `app.MapFallback(...)` et avant `app.Run()` :

```csharp
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ProjectDbContext>().Database.Migrate();
}
```

## 4. Application

```csharp
// src/Web.Template.CQRS.Application/Products/Common/ProductResult.cs
namespace Web.Template.CQRS.Application.Products.Common;

public sealed record ProductResult(Guid Id, string Name, decimal Price);
```

Commande, validateur, handler (version simple, sans règle d'unicité ; pour l'unicité voir la section 7) :

```csharp
// src/Web.Template.CQRS.Application/Products/Commands/CreateProduct/CreateProductCommand.cs
using Mediator;
using Web.Template.CQRS.Application.Products.Common;

namespace Web.Template.CQRS.Application.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price) : ICommand<ProductResult>;
```

```csharp
// .../CreateProduct/CreateProductCommandValidator.cs
using FluentValidation;
using Web.Template.CQRS.Domain.Products;

namespace Web.Template.CQRS.Application.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(Product.NameMaxLength);

        RuleFor(command => command.Price)
            .GreaterThanOrEqualTo(0);
    }
}
```

```csharp
// .../CreateProduct/CreateProductCommandHandler.cs
using Mediator;
using Web.Template.CQRS.Application.Common.Interfaces.Persistence;
using Web.Template.CQRS.Application.Products.Common;
using Web.Template.CQRS.Domain.Products;

namespace Web.Template.CQRS.Application.Products.Commands.CreateProduct;

public sealed class CreateProductCommandHandler(IProductRepository products)
    : ICommandHandler<CreateProductCommand, ProductResult>
{
    public async ValueTask<ProductResult> Handle(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = Product.Create(command.Name, command.Price);

        await products.AddAsync(product);

        return new ProductResult(product.Id.Value, product.Name, product.Price);
    }
}
```

Query de lecture (`null` = introuvable) avec validateur d'identifiant :

```csharp
// src/Web.Template.CQRS.Application/Products/Queries/GetProduct/GetProductQuery.cs
using Mediator;
using Web.Template.CQRS.Application.Products.Common;

namespace Web.Template.CQRS.Application.Products.Queries.GetProduct;

public sealed record GetProductQuery(Guid Id) : IQuery<ProductResult?>;
```

```csharp
// .../GetProduct/GetProductQueryValidator.cs
using FluentValidation;

namespace Web.Template.CQRS.Application.Products.Queries.GetProduct;

public sealed class GetProductQueryValidator : AbstractValidator<GetProductQuery>
{
    public GetProductQueryValidator()
    {
        RuleFor(query => query.Id).NotEmpty();
    }
}
```

```csharp
// .../GetProduct/GetProductQueryHandler.cs
using Mediator;
using Web.Template.CQRS.Application.Common.Interfaces.Persistence;
using Web.Template.CQRS.Application.Products.Common;
using Web.Template.CQRS.Domain.Products;

namespace Web.Template.CQRS.Application.Products.Queries.GetProduct;

public sealed class GetProductQueryHandler(IProductRepository products)
    : IQueryHandler<GetProductQuery, ProductResult?>
{
    public async ValueTask<ProductResult?> Handle(
        GetProductQuery query,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(new ProductId(query.Id));

        return product is null
            ? null
            : new ProductResult(product.Id.Value, product.Name, product.Price);
    }
}
```

Rien à enregistrer : Mediator (générateur de code) trouve les handlers, `AddValidatorsFromAssembly` les validateurs. Le `ValidationBehavior` lève une `ValidationException` que `ErrorHandling` transforme en 400 Problem Details (`code: VALIDATION`, `errors` par nom de propriété).

## 5. Contrats

```csharp
// src/Web.Template.CQRS.Contracts/Products/CreateProductRequest.cs
namespace Web.Template.CQRS.Contracts.Products;

public sealed record CreateProductRequest(string Name, decimal Price);
```

```csharp
// src/Web.Template.CQRS.Contracts/Products/ProductResponse.cs
namespace Web.Template.CQRS.Contracts.Products;

public sealed record ProductResponse(Guid Id, string Name, decimal Price);
```

## 6. Endpoint

Version simple (création 201, lecture 200/404). `Policies.RequireAdmin` n'existe qu'avec rôles ou groupes ; en modèle « authentifié uniquement », remplacez par `.RequireAuthorization()`. Supprimez le `using ...RateLimiting` et les `RequireRateLimiting` si le projet n'a pas de `RateLimitingPolicies`.

```csharp
// src/Web.Template.CQRS.Api/Controllers/ProductsController.cs
using Mediator;
using Microsoft.AspNetCore.RateLimiting;
using Web.Template.CQRS.Api.Authorization;
using Web.Template.CQRS.Api.Common.RateLimiting;
using Web.Template.CQRS.Application.Products.Commands.CreateProduct;
using Web.Template.CQRS.Application.Products.Queries.GetProduct;
using Web.Template.CQRS.Contracts.Products;

namespace Web.Template.CQRS.Api.Controllers;

public static class ProductsController
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var createEndpoint = endpoints.MapPost("/products", async (
                CreateProductRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new CreateProductCommand(request.Name, request.Price),
                    cancellationToken);

                return TypedResults.Created(
                    $"/products/{result.Id}",
                    new ProductResponse(result.Id, result.Name, result.Price));
            })
            .WithName("CreateProduct")
            .RequireAuthorization(Policies.RequireAdmin);
        createEndpoint.RequireRateLimiting(RateLimitingPolicies.Write);

        var getEndpoint = endpoints.MapGet("/products/{id:guid}", async (
                Guid id,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new GetProductQuery(id), cancellationToken);

                return result is null
                    ? Results.NotFound()
                    : Results.Ok(new ProductResponse(result.Id, result.Name, result.Price));
            })
            .WithName("GetProduct")
            .RequireAuthorization();
        getEndpoint.RequireRateLimiting(RateLimitingPolicies.Read);

        return endpoints;
    }
}
```

Branchement dans `src/Web.Template.CQRS.Api/Program.cs`, après `app.MapCurrentUserEndpoints();` (et `app.MapAdminEndpoints();` s'il existe), **avant** `app.MapFallback(...)` :

```csharp
app.MapProductEndpoints();
```

`{id:guid}` n'accepte que des GUID (sinon la route de secours répond 404). Le 404 de `Results.NotFound()` devient du Problem Details grâce à `UseStatusCodePages`.

## 7. Unicité et conflit 409

Le handler vérifie l'unicité et renvoie un résultat explicite ; l'endpoint le traduit en 409 Problem Details. Remplace la commande, le handler et l'endpoint de création des sections 4 et 6.

```csharp
// src/Web.Template.CQRS.Application/Products/Commands/CreateProduct/CreateProductOutcome.cs
using Web.Template.CQRS.Application.Products.Common;

namespace Web.Template.CQRS.Application.Products.Commands.CreateProduct;

public sealed record CreateProductOutcome(ProductResult? Created)
{
    public bool IsConflict => Created is null;

    public static CreateProductOutcome Success(ProductResult created) => new(created);

    public static CreateProductOutcome Conflict() => new((ProductResult?)null);
}
```

```csharp
// .../CreateProduct/CreateProductCommand.cs (remplace le fichier entier)
using Mediator;

namespace Web.Template.CQRS.Application.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price) : ICommand<CreateProductOutcome>;
```

```csharp
// .../CreateProduct/CreateProductCommandHandler.cs (remplace le fichier entier)
using Mediator;
using Web.Template.CQRS.Application.Common.Interfaces.Persistence;
using Web.Template.CQRS.Application.Products.Common;
using Web.Template.CQRS.Domain.Products;

namespace Web.Template.CQRS.Application.Products.Commands.CreateProduct;

public sealed class CreateProductCommandHandler(IProductRepository products)
    : ICommandHandler<CreateProductCommand, CreateProductOutcome>
{
    public async ValueTask<CreateProductOutcome> Handle(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var name = command.Name.Trim(); // même normalisation que Product.Create

        if (await products.ExistsByNameAsync(name))
        {
            return CreateProductOutcome.Conflict();
        }

        var product = Product.Create(name, command.Price);
        await products.AddAsync(product);

        return CreateProductOutcome.Success(
            new ProductResult(product.Id.Value, product.Name, product.Price));
    }
}
```

Le validateur ne change pas (il reste la source des 400). Pour un format précis (par exemple un code à 13 chiffres), ajoutez des règles au validateur : `.Matches(@"^\d{13}$")`, ou `.Must(IsValidChecksum)` pour une somme de contrôle, avec `.WithMessage(...)`. Le validateur s'exécute avant le handler, donc un format invalide donne 400 et jamais 409. Une valeur vide viole alors `NotEmpty()` **et** la règle de format : dans les tests du validateur, cherchez l'erreur de la propriété (`Assert.Contains(result.Errors, f => f.PropertyName == ...)`) plutôt que `Assert.Single`, ou ajoutez `.Cascade(CascadeMode.Stop)` à la propriété.

Endpoint de création avec 409 (toutes les branches en `Results.*`, `HttpContext` injecté pour le `traceId`). Il remplace le `createEndpoint` de la section 6 dans `MapProductEndpoints` ; ajoutez `using System.Diagnostics;` en tête de `ProductsController.cs` (les autres `using` de la section 6 restent nécessaires) :

```csharp
// dans MapProductEndpoints (à la place du createEndpoint de la section 6)
var createEndpoint = endpoints.MapPost("/products", async (
        CreateProductRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken) =>
    {
        var outcome = await mediator.Send(
            new CreateProductCommand(request.Name, request.Price),
            cancellationToken);

        if (outcome.IsConflict)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "A product with this name already exists.",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "CONFLICT",
                    ["traceId"] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier
                });
        }

        var created = outcome.Created!;
        return Results.Created(
            $"/products/{created.Id}",
            new ProductResponse(created.Id, created.Name, created.Price));
    })
    .WithName("CreateProduct")
    .RequireAuthorization(Policies.RequireAdmin);
createEndpoint.RequireRateLimiting(RateLimitingPolicies.Write);
```

Le `title` ne doit pas contenir la valeur fournie ni le message d'une exception. L'index unique de la section 2 reste le filet en cas de requêtes concurrentes (une `DbUpdateException` en base serait alors une 500 ; l'accepter, ou la rattraper dans le repository, est une décision à signaler à l'utilisateur).

Faux repository pour les tests unitaires (ajoutez ces deux méthodes à `FakeProductRepository`) :

```csharp
// dans FakeProductRepository (aucun using de plus : System.Linq est implicite)
public Task<bool> ExistsByNameAsync(string name) =>
    Task.FromResult(Products.Any(product => product.Name == name));

public Task<IReadOnlyList<Product>> ListAsync(int skip, int take) =>
    Task.FromResult<IReadOnlyList<Product>>(
        Products.OrderBy(product => product.Name).Skip(skip).Take(take).ToList());
```

## 8. Liste

Query paginée, résultat en liste ; la réponse HTTP est un tableau JSON.

```csharp
// src/Web.Template.CQRS.Application/Products/Queries/ListProducts/ListProductsQuery.cs
using Mediator;
using Web.Template.CQRS.Application.Products.Common;

namespace Web.Template.CQRS.Application.Products.Queries.ListProducts;

public sealed record ListProductsQuery(int Page, int PageSize) : IQuery<IReadOnlyList<ProductResult>>;
```

```csharp
// .../ListProducts/ListProductsQueryValidator.cs
using FluentValidation;

namespace Web.Template.CQRS.Application.Products.Queries.ListProducts;

public sealed class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
```

```csharp
// .../ListProducts/ListProductsQueryHandler.cs
using Mediator;
using Web.Template.CQRS.Application.Common.Interfaces.Persistence;
using Web.Template.CQRS.Application.Products.Common;

namespace Web.Template.CQRS.Application.Products.Queries.ListProducts;

public sealed class ListProductsQueryHandler(IProductRepository products)
    : IQueryHandler<ListProductsQuery, IReadOnlyList<ProductResult>>
{
    public async ValueTask<IReadOnlyList<ProductResult>> Handle(
        ListProductsQuery query,
        CancellationToken cancellationToken)
    {
        var page = await products.ListAsync((query.Page - 1) * query.PageSize, query.PageSize);

        return page
            .Select(product => new ProductResult(product.Id.Value, product.Name, product.Price))
            .ToList();
    }
}
```

Endpoint (lecture pour tout utilisateur connecté), dans `MapProductEndpoints` :

```csharp
var listEndpoint = endpoints.MapGet("/products", async (
        int? page,
        int? pageSize,
        IMediator mediator,
        CancellationToken cancellationToken) =>
    {
        var results = await mediator.Send(
            new ListProductsQuery(page ?? 1, pageSize ?? 50),
            cancellationToken);

        return Results.Ok(results
            .Select(result => new ProductResponse(result.Id, result.Name, result.Price))
            .ToList());
    })
    .WithName("ListProducts")
    .RequireAuthorization();
listEndpoint.RequireRateLimiting(RateLimitingPolicies.Read);
```

Pour une petite collection bornée, sans pagination : `IQuery<IReadOnlyList<ProductResult>>` sans paramètres, handler avec `(await products.GetAllAsync()).OrderBy(product => product.Name)`.

## 9. Tests

Unitaires du validateur (aucune dépendance) :

```csharp
// tests/Web.Template.CQRS.Application.UnitTests/Products/CreateProductCommandValidatorTests.cs
using Web.Template.CQRS.Application.Products.Commands.CreateProduct;
using Web.Template.CQRS.Domain.Products;

namespace Web.Template.CQRS.Application.UnitTests.Products;

public sealed class CreateProductCommandValidatorTests
{
    private readonly CreateProductCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_IsValid()
    {
        var result = _validator.Validate(new CreateProductCommand("Keyboard", 49.90m));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyName_ReportsNameError(string name)
    {
        var result = _validator.Validate(new CreateProductCommand(name, 10m));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(CreateProductCommand.Name), failure.PropertyName);
    }

    [Fact]
    public void Validate_WithNameAtMaxLength_IsValid()
    {
        var result = _validator.Validate(new CreateProductCommand(new string('a', Product.NameMaxLength), 10m));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithNameOverMaxLength_ReportsNameError()
    {
        var result = _validator.Validate(new CreateProductCommand(new string('a', Product.NameMaxLength + 1), 10m));

        var failure = Assert.Single(result.Errors);
        Assert.Equal(nameof(CreateProductCommand.Name), failure.PropertyName);
    }
}
```

Faux repository (liste en mémoire ; implémentez toute l'interface, ajoutez les méthodes de la section 7) :

```csharp
// tests/Web.Template.CQRS.Application.UnitTests/Fakes/FakeProductRepository.cs
using System.Linq.Expressions;
using Web.Template.CQRS.Application.Common.Interfaces.Persistence;
using Web.Template.CQRS.Domain.Common.Models;
using Web.Template.CQRS.Domain.Products;

namespace Web.Template.CQRS.Application.UnitTests.Fakes;

public sealed class FakeProductRepository : IProductRepository
{
    public List<Product> Products { get; } = [];

    public Task<Product> AddAsync(Product entity)
    {
        Products.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<Product?> GetByIdAsync(ValueObject id) =>
        Task.FromResult(Products.FirstOrDefault(product => product.Id.Equals(id)));

    public Task<IEnumerable<Product>> GetAllAsync(params Expression<Func<Product, object>>[] includes) =>
        Task.FromResult<IEnumerable<Product>>(Products);

    public Task<Product> UpdateAsync(Product entity) => Task.FromResult(entity);

    public Task<bool> DeleteAsync(ValueObject id) =>
        Task.FromResult(Products.RemoveAll(product => product.Id.Equals(id)) > 0);

    public Task<bool> ExistsByNameAsync(string name) =>
        Task.FromResult(Products.Any(product => product.Name == name));

    public Task<IReadOnlyList<Product>> ListAsync(int skip, int take) =>
        Task.FromResult<IReadOnlyList<Product>>(
            Products.OrderBy(product => product.Name).Skip(skip).Take(take).ToList());
}
```

Unitaires du handler, y compris le conflit (version section 7) :

```csharp
// tests/Web.Template.CQRS.Application.UnitTests/Products/CreateProductCommandHandlerTests.cs
using Web.Template.CQRS.Application.Products.Commands.CreateProduct;
using Web.Template.CQRS.Application.UnitTests.Fakes;

namespace Web.Template.CQRS.Application.UnitTests.Products;

public sealed class CreateProductCommandHandlerTests
{
    [Fact]
    public async Task Handle_StoresTheProductAndReturnsIt()
    {
        var repository = new FakeProductRepository();
        var handler = new CreateProductCommandHandler(repository);

        var outcome = await handler.Handle(
            new CreateProductCommand("Keyboard", 49.90m),
            TestContext.Current.CancellationToken);

        Assert.False(outcome.IsConflict);
        var stored = Assert.Single(repository.Products);
        Assert.Equal(stored.Id.Value, outcome.Created!.Id);
    }

    [Fact]
    public async Task Handle_WithExistingName_ReturnsConflictAndStoresNothingNew()
    {
        var repository = new FakeProductRepository();
        var handler = new CreateProductCommandHandler(repository);
        await handler.Handle(new CreateProductCommand("Keyboard", 49.90m), TestContext.Current.CancellationToken);

        var outcome = await handler.Handle(
            new CreateProductCommand("  Keyboard ", 10m),
            TestContext.Current.CancellationToken);

        Assert.True(outcome.IsConflict);
        Assert.Single(repository.Products);
    }
}
```

Intégration. `ApiFactory` fournit une base EF InMemory **partagée par tous les tests de l'assembly d'intégration** (nom de base constant dans `ApiFactory`, donc commune à toutes les classes de tests) : utilisez des noms uniques (`Guid`) pour que les tests ne se gênent pas, et n'affirmez jamais qu'une liste est vide ou de taille exacte, seulement qu'elle contient vos éléments. `AsUser` crée l'identité (en-têtes `X-Test-User`, `X-Test-Roles`) ; avec le modèle groupes, utilisez `groups: [ApiFactory.AdministratorsGroupId]` à la place de `roles:`.

```csharp
// tests/Web.Template.CQRS.Api.IntegrationTests/Products/ProductEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Web.Template.CQRS.Api.Authorization;
using Web.Template.CQRS.Api.IntegrationTests.Infrastructure;
using Web.Template.CQRS.Contracts.Products;

namespace Web.Template.CQRS.Api.IntegrationTests.Products;

public sealed class ProductEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient Admin() => factory.CreateClient().AsUser("admin-1", roles: [AppRoles.Admin]);

    private HttpClient StandardUser() => factory.CreateClient().AsUser("user-1", roles: [AppRoles.User]);

    private static string UniqueName() => $"Product-{Guid.NewGuid():N}";

    [Fact]
    public async Task Create_AsAdmin_Returns201AndTheProductCanBeRead()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var created = await Admin().PostAsJsonAsync(
            "/products", new CreateProductRequest(UniqueName(), 49.90m), cancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var product = await created.Content.ReadFromJsonAsync<ProductResponse>(cancellationToken);
        Assert.NotNull(product);
        Assert.Equal($"/products/{product.Id}", created.Headers.Location?.ToString());

        var fetched = await StandardUser().GetFromJsonAsync<ProductResponse>($"/products/{product.Id}", cancellationToken);
        Assert.Equal(product, fetched);
    }

    [Fact]
    public async Task Create_AsStandardUser_Returns403()
    {
        var response = await StandardUser().PostAsJsonAsync(
            "/products", new CreateProductRequest(UniqueName(), 1m), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutAuthentication_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/products", new CreateProductRequest(UniqueName(), 1m), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyName_Returns400ProblemDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await Admin().PostAsJsonAsync("/products", new CreateProductRequest("", 10m), cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("VALIDATION", problem.RootElement.GetProperty("code").GetString());
        Assert.True(problem.RootElement.GetProperty("errors").TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task Create_WithExistingName_Returns409ProblemDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var request = new CreateProductRequest(UniqueName(), 5m);
        var admin = Admin();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/products", request, cancellationToken)).StatusCode);

        var response = await admin.PostAsJsonAsync("/products", request, cancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("CONFLICT", problem.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_WithUnknownId_Returns404ProblemDetails()
    {
        var response = await StandardUser().GetAsync($"/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_WithEmptyGuid_Returns400()
    {
        var response = await StandardUser().GetAsync($"/products/{Guid.Empty}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_AsStandardUser_ReturnsCreatedProducts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        // Préfixe "0-" : trié avant les noms "Product-..." des autres tests, donc toujours dans la première page.
        var name = $"0-List-{Guid.NewGuid():N}";
        var created = await Admin().PostAsJsonAsync("/products", new CreateProductRequest(name, 3m), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var list = await StandardUser().GetFromJsonAsync<List<ProductResponse>>("/products?pageSize=100", cancellationToken);

        Assert.NotNull(list);
        Assert.Contains(list, product => product.Name == name);
    }

    [Fact]
    public async Task List_WithoutAuthentication_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/products", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_WithInvalidPageSize_Returns400()
    {
        var response = await StandardUser().GetAsync("/products?pageSize=0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

Valeur unique à format contraint (ISBN, code à somme de contrôle) : un `Guid` ne passe pas le validateur. Ajoutez au test d'intégration un petit générateur de valeurs valides et aléatoires (par exemple `UniqueIsbn()` : préfixe fixe, chiffres aléatoires, chiffre de contrôle calculé) pour que chaque test crée sa propre valeur.

Le test de liste nomme son élément avec un préfixe qui se trie avant tous les autres (`0-List-...`) : le tri par `Name` le place dans la première page, même si d'autres tests de l'assembly ont créé de nombreux éléments. Si plusieurs tests de liste coexistent, gardez ce préfixe réservé à un seul d'entre eux par ordre de tri, ou filtrez la réponse sur un préfixe commun. Pour un test de pagination strict, créez les éléments avec un préfixe commun connu et filtrez la réponse sur ce préfixe.
