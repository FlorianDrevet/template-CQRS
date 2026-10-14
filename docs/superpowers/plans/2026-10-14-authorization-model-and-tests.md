# Modèle d'autorisation, Keycloak miroir d'Entra et tests — Plan d'implémentation

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ajouter au template `templatewebcqrs` un paramètre `AuthorizationModel` (Authenticated / AppRoles / Groups / AppRolesAndGroups), un realm Keycloak local qui émet des tokens de même forme qu'Entra, des personas, un script d'obtention de token, des projets de tests et une CI qui vérifie une matrice de générations.

**Architecture:** Le code d'autorisation est identique pour Entra et Keycloak : il lit les claims `oid`, `roles` et `groups`, et les object IDs de groupes viennent de la configuration. Le realm Keycloak reproduit ces claims via des protocol mappers déclarés sur les clients. Les variantes sont produites par le moteur `dotnet new` (symboles `computed`, `#if`, `modifiers`) ; le dépôt du template compile la variante la plus complète grâce à un `Directory.Build.props` exclu de la génération.

**Tech Stack:** .NET 10 (SDK 10.0.203+), ASP.NET Core minimal APIs, Mediator 3 (source generator), FluentValidation 12, Microsoft.Identity.Web, Aspire 13.5 (Keycloak, SQL Server, Azurite), xUnit v3 4.0.2 sur Microsoft.Testing.Platform, Microsoft.AspNetCore.Mvc.Testing, EF Core InMemory, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-14-authorization-model-and-tests-design.md`

## Global Constraints

- Branche de travail : `feat/authorization-model-and-tests`. Commits avec la ligne `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Toutes les commandes se lancent depuis la racine du dépôt (`template-CQRS/`), shell Git Bash.
- Valeur par défaut de `AuthorizationModel` : `AppRoles`. `EnableTests` par défaut : `true`.
- Dans le code, utiliser uniquement les symboles `UseAppRoles`, `UseGroups`, `UseAuthorizationPolicies`, `EnableAspire`, `EnableRateLimiting`, `EnableTests` ; jamais `AuthorizationModel` directement.
- Claims : `oid`, `sub`, `name`, `email`, `roles`, `groups` (constantes `ClaimNames`).
- Rôles d'exemple : `Admin`, `User`. Groupe d'exemple : `Administrators`, object ID local `6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11`.
- Personas : `admin@local.test` et `user@local.test`, mot de passe `Passw0rd!`, realm `template`, client CLI `template-cli`.
- Les object IDs de groupes viennent toujours de la configuration (`Authorization:Groups:<Nom>`), comparés sans tenir compte de la casse.
- Le realm ne déclare aucun tableau `clientScopes` ; les mappers sont dans `protocolMappers` des clients ; les clients n'énumèrent pas `defaultClientScopes`.
- Versions : `xunit.v3` 4.0.2, `Microsoft.AspNetCore.Mvc.Testing` 10.0.11, `Microsoft.EntityFrameworkCore.InMemory` 10.0.11.
- `global.json` déclare `"test": { "runner": "Microsoft.Testing.Platform" }` (obligatoire pour xUnit v3 4.x avec le SDK .NET 10).
- Exclus de toute génération : `.idea/**`, `eng/**`, `docs/**`, `.github/workflows/template-ci.yml`, `Directory.Build.props`.
- Style C# du dépôt : namespaces file-scoped, constructeurs primaires, `sealed`, pas de commentaires XML, `TypedResults`, endpoints regroupés dans des classes statiques `*Controller` avec méthode `Map*Endpoints`.

## Review Focus

- Object ID de groupe configuré en majuscules alors qu'Entra émet des minuscules : l'utilisateur doit être autorisé (test ajouté en Task 4).
- Utilisateur authentifié sans rôle ni groupe : `/me` renvoie des tableaux vides, pas `null` (test ajouté en Task 3).
- En-têtes de rôles présents sans utilisateur : la requête reste anonyme et `/me` renvoie 401 (test ajouté en Task 3).
- Persona ou mot de passe inconnu dans `get-token` : le script échoue avec un code non nul et un message lisible, sans afficher de token vide (vérifié en Task 6).
- Realm modifié alors que le volume Keycloak persistant existe déjà : le changement n'est pas appliqué ; le README explique comment repartir d'un volume vierge et la vérification live utilise un projet fraîchement généré (Task 6 et Task 7).

---

## File Structure

```
global.json                                   (modifié) runner de tests MTP
Directory.Build.props                         (créé)    constantes de compilation du dépôt template, exclu de la génération
Directory.Packages.props                      (modifié) packages de tests
Web.Template.CQRS.slnx / .Core.slnx           (modifiés) dossier /tests/ conditionnel
.template.config/template.json                (modifié) symboles + modifiers
eng/verify-template.sh                        (créé)    génère + build + test une combinaison
.github/workflows/template-ci.yml             (créé)    matrice de générations
scripts/get-token.ps1, scripts/get-token.sh   (créés)   token d'un persona (Aspire uniquement)
README.md, src/Web.Template.CQRS.Api/README.md (modifiés)

src/Web.Template.CQRS.Api/
  Authentication/ClaimNames.cs                (créé)    noms de claims Entra
  Authentication/HttpCurrentUser.cs           (modifié) Roles, Groups
  Authorization/AppRoles.cs                   (créé, UseAppRoles)
  Authorization/GroupOptions.cs               (créé, UseGroups)
  Authorization/Policies.cs                   (créé, UseAuthorizationPolicies)
  Authorization/AuthorizationSetup.cs         (créé)    fallback policy + policies nommées
  Controllers/AdminController.cs              (créé, UseAuthorizationPolicies) GET /admin/ping
  Controllers/CurrentUserController.cs        (modifié) roles, groups
  DependencyInjection.cs, Program.cs          (modifiés)
  appsettings.json, appsettings.Development.json, Api.http (modifiés)

src/Web.Template.CQRS.Application/
  Common/Interfaces/Authentication/ICurrentUser.cs (modifié)
  Users/Common/CurrentUserResult.cs, Users/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs (modifiés)
  Admin/Common/PingAdminResult.cs, Admin/Queries/PingAdmin/{PingAdminQuery,PingAdminQueryHandler}.cs (créés)

src/Web.Template.CQRS.Contracts/
  Authentication/CurrentUserResponse.cs (modifié), Admin/PingAdminResponse.cs (créé)

src/Web.Template.CQRS.AppHost/
  Realms/template-realm.json                  (réécrit) variante AppRolesAndGroups
  RealmVariants/template-realm.{authenticated,approles,groups}.json (créés)

tests/Web.Template.CQRS.Application.UnitTests/
  Fakes/FakeCurrentUser.cs, Users/GetCurrentUserQueryHandlerTests.cs,
  Common/ValidationBehaviorTests.cs, Admin/PingAdminQueryHandlerTests.cs
tests/Web.Template.CQRS.Api.IntegrationTests/
  Infrastructure/{ApiFactory,TestAuthHandler,TestUserExtensions,FakeBlobService}.cs
  Users/CurrentUserEndpointTests.cs, Errors/ErrorHandlingTests.cs,
  Infrastructure/TestInfrastructureTests.cs, RateLimiting/RateLimitingTests.cs,
  Admin/AdminEndpointTests.cs, Authorization/GroupOptionsValidationTests.cs
```

---

### Task 1: Socle de tests et compilation du dépôt template

Pose le runner de tests, les constantes de compilation du dépôt, les deux projets de tests et des tests qui figent le comportement existant (`/me`, gestion d'erreurs, rate limiting, remplacement de l'infrastructure).

**Files:**
- Modify: `global.json`, `Directory.Packages.props`, `Web.Template.CQRS.slnx`, `Web.Template.CQRS.Core.slnx`
- Create: `Directory.Build.props`
- Create: `tests/Web.Template.CQRS.Application.UnitTests/Web.Template.CQRS.Application.UnitTests.csproj`, `Fakes/FakeCurrentUser.cs`, `Users/GetCurrentUserQueryHandlerTests.cs`, `Common/ValidationBehaviorTests.cs`
- Create: `tests/Web.Template.CQRS.Api.IntegrationTests/Web.Template.CQRS.Api.IntegrationTests.csproj`, `Infrastructure/ApiFactory.cs`, `Infrastructure/TestAuthHandler.cs`, `Infrastructure/TestUserExtensions.cs`, `Infrastructure/FakeBlobService.cs`, `Infrastructure/TestInfrastructureTests.cs`, `Users/CurrentUserEndpointTests.cs`, `Errors/ErrorHandlingTests.cs`, `RateLimiting/RateLimitingTests.cs`

**Interfaces:**
- Consumes: code existant (`GetCurrentUserQueryHandler`, `ValidationBehavior<,>`, `ErrorHandling.UseErrorHandling()`, `Program`).
- Produces:
  - `ApiFactory : WebApplicationFactory<Program>` (namespace `Web.Template.CQRS.Api.IntegrationTests.Infrastructure`).
  - `TestAuthHandler.SchemeName = "Test"`, `UserHeader = "X-Test-User"`, `RolesHeader = "X-Test-Roles"`, `GroupsHeader = "X-Test-Groups"`.
  - `TestUserExtensions.AsUser(this HttpClient client, string userId, IEnumerable<string>? roles = null, IEnumerable<string>? groups = null) : HttpClient`.
  - `FakeCurrentUser : ICurrentUser` avec propriétés `init` (namespace `Web.Template.CQRS.Application.UnitTests.Fakes`).

- [ ] **Step 1: Activer Microsoft.Testing.Platform et les constantes du dépôt**

Remplacer `global.json` par :

```json
{
  "sdk": {
    "version": "10.0.203",
    "rollForward": "latestMajor",
    "allowPrerelease": true
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Créer `Directory.Build.props` :

```xml
<Project>
  <!-- Template repository only (excluded from generated projects): compiles the most complete variant. -->
  <PropertyGroup>
    <DefineConstants>$(DefineConstants);EnableAspire;EnableRateLimiting;EnableTests;UseAppRoles;UseGroups;UseAuthorizationPolicies</DefineConstants>
  </PropertyGroup>
</Project>
```

Dans `Directory.Packages.props`, ajouter dans l'`ItemGroup` (ordre alphabétique non requis, les placer à la fin) :

```xml
    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.11" />
    <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.11" />
    <PackageVersion Include="xunit.v3" Version="4.0.2" />
```

- [ ] **Step 2: Vérifier que la variante complète compile désormais dans le dépôt**

Run: `dotnet build Web.Template.CQRS.slnx`
Expected: Build succeeded. (Le code sous `#if (EnableAspire)` et `#if (EnableRateLimiting)` est maintenant compilé dans le dépôt.)

- [ ] **Step 3: Créer le projet de tests unitaires**

`tests/Web.Template.CQRS.Application.UnitTests/Web.Template.CQRS.Application.UnitTests.csproj` :

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <OutputType>Exe</OutputType>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
        <RootNamespace>Web.Template.CQRS.Application.UnitTests</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <Using Include="Xunit" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="xunit.v3" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\Web.Template.CQRS.Application\Web.Template.CQRS.Application.csproj" />
    </ItemGroup>
</Project>
```

`tests/Web.Template.CQRS.Application.UnitTests/Fakes/FakeCurrentUser.cs` :

```csharp
using Web.Template.CQRS.Application.Common.Interfaces.Authentication;

namespace Web.Template.CQRS.Application.UnitTests.Fakes;

public sealed class FakeCurrentUser : ICurrentUser
{
    public string? Id { get; init; }

    public string? DisplayName { get; init; }

    public string? Email { get; init; }
}
```

`tests/Web.Template.CQRS.Application.UnitTests/Users/GetCurrentUserQueryHandlerTests.cs` :

```csharp
using Web.Template.CQRS.Application.UnitTests.Fakes;
using Web.Template.CQRS.Application.Users.Queries.GetCurrentUser;

namespace Web.Template.CQRS.Application.UnitTests.Users;

public sealed class GetCurrentUserQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsCurrentUserIdentity()
    {
        var currentUser = new FakeCurrentUser
        {
            Id = "user-1",
            DisplayName = "Ada Lovelace",
            Email = "ada@contoso.test"
        };
        var handler = new GetCurrentUserQueryHandler(currentUser);

        var result = await handler.Handle(new GetCurrentUserQuery(), TestContext.Current.CancellationToken);

        Assert.Equal("user-1", result.Id);
        Assert.Equal("Ada Lovelace", result.DisplayName);
        Assert.Equal("ada@contoso.test", result.Email);
    }
}
```

`tests/Web.Template.CQRS.Application.UnitTests/Common/ValidationBehaviorTests.cs` :

```csharp
using FluentValidation;
using Mediator;
using Web.Template.CQRS.Application.Common.Behaviors;

namespace Web.Template.CQRS.Application.UnitTests.Common;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WithInvalidMessage_ThrowsValidationExceptionWithoutCallingNext()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(async () =>
            await behavior.Handle(
                new SampleCommand(string.Empty),
                (_, _) =>
                {
                    nextCalled = true;
                    return ValueTask.FromResult("handled");
                },
                TestContext.Current.CancellationToken));

        Assert.False(nextCalled);
        var failure = Assert.Single(exception.Errors);
        Assert.Equal(nameof(SampleCommand.Name), failure.PropertyName);
    }

    [Fact]
    public async Task Handle_WithValidMessage_CallsNext()
    {
        var behavior = new ValidationBehavior<SampleCommand, string>([new SampleCommandValidator()]);

        var result = await behavior.Handle(
            new SampleCommand("valid"),
            (_, _) => ValueTask.FromResult("handled"),
            TestContext.Current.CancellationToken);

        Assert.Equal("handled", result);
    }

    public sealed record SampleCommand(string Name) : ICommand<string>;

    public sealed class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator()
        {
            RuleFor(command => command.Name).NotEmpty();
        }
    }
}
```

- [ ] **Step 4: Créer le projet de tests d'intégration et son infrastructure**

`tests/Web.Template.CQRS.Api.IntegrationTests/Web.Template.CQRS.Api.IntegrationTests.csproj` :

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <OutputType>Exe</OutputType>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <IsPackable>false</IsPackable>
        <RootNamespace>Web.Template.CQRS.Api.IntegrationTests</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <Using Include="Xunit" />
    </ItemGroup>

    <ItemGroup>
        <FrameworkReference Include="Microsoft.AspNetCore.App" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
        <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
        <PackageReference Include="xunit.v3" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\..\src\Web.Template.CQRS.Api\Web.Template.CQRS.Api.csproj" />
    </ItemGroup>
</Project>
```

`Infrastructure/TestAuthHandler.cs` :

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";
    public const string RolesHeader = "X-Test-Roles";
    public const string GroupsHeader = "X-Test-Groups";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers[UserHeader].ToString();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new("oid", userId),
            new("sub", userId),
            new("name", $"Test {userId}"),
            new("email", $"{userId}@tests.local")
        };
        claims.AddRange(HeaderValues(RolesHeader).Select(role => new Claim("roles", role)));
        claims.AddRange(HeaderValues(GroupsHeader).Select(group => new Claim("groups", group)));

        var identity = new ClaimsIdentity(claims, SchemeName, "name", "roles");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string[] HeaderValues(string headerName) =>
        Request.Headers[headerName].ToString()
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
```

`Infrastructure/TestUserExtensions.cs` :

```csharp
namespace Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

public static class TestUserExtensions
{
    public static HttpClient AsUser(
        this HttpClient client,
        string userId,
        IEnumerable<string>? roles = null,
        IEnumerable<string>? groups = null)
    {
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
        if (roles is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        }

        if (groups is not null)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.GroupsHeader, string.Join(',', groups));
        }

        return client;
    }
}
```

`Infrastructure/FakeBlobService.cs` :

```csharp
using Web.Template.CQRS.Application.Common.Interfaces.Services;

namespace Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

public sealed class FakeBlobService : IBlobService
{
    public Task<Uri> UploadFileAsync(Stream content, string fileName, string? contentType = null) =>
        Task.FromResult(new Uri($"https://blobs.tests.local/uploads/{fileName}"));

    public Task<bool> DeleteFileAsync(string fileName) => Task.FromResult(true);
}
```

`Infrastructure/ApiFactory.cs` :

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Web.Template.CQRS.Application.Common.Interfaces.Services;
using Web.Template.CQRS.Infrastructure.Persistence;

namespace Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth:Provider", "Keycloak");
        builder.UseSetting("Auth:Authority", "https://keycloak.tests.local/realms/template");
        builder.UseSetting("Auth:Audience", "template-api");

        builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.RemoveAll<IDbContextOptionsConfiguration<ProjectDbContext>>();
            services.RemoveAll<DbContextOptions<ProjectDbContext>>();
            services.AddDbContext<ProjectDbContext>(options =>
                options.UseInMemoryDatabase("api-integration-tests"));

            services.RemoveAll<IBlobService>();
            services.AddSingleton<IBlobService, FakeBlobService>();
        });
    }
}
```

Note : `Program.cs` lit la section `Auth` avant `builder.Build()`. Si les tests échouent au démarrage avec `Auth:Instance, Auth:TenantId and Auth:ClientId are required for Entra.`, c'est que `UseSetting` n'est pas visible à ce moment : ajouter alors dans `ApiFactory` un constructeur qui pose les mêmes valeurs en variables d'environnement (`Environment.SetEnvironmentVariable("Auth__Provider", "Keycloak")`, idem `Auth__Authority`, `Auth__Audience`) et garder les `UseSetting`.

`Infrastructure/TestInfrastructureTests.cs` :

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Template.CQRS.Infrastructure.Persistence;

namespace Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

public sealed class TestInfrastructureTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void DbContext_IsReplacedByInMemoryProvider()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProjectDbContext>();

        Assert.True(dbContext.Database.IsInMemory());
    }
}
```

- [ ] **Step 5: Écrire les tests qui figent le comportement existant**

`Users/CurrentUserEndpointTests.cs` :

```csharp
using System.Net;
using System.Net.Http.Json;
using Web.Template.CQRS.Api.IntegrationTests.Infrastructure;
using Web.Template.CQRS.Contracts.Authentication;

namespace Web.Template.CQRS.Api.IntegrationTests.Users;

public sealed class CurrentUserEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Me_WithoutAuthentication_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithAuthenticatedUser_ReturnsIdentityClaims()
    {
        var client = factory.CreateClient().AsUser("user-42");

        var body = await client.GetFromJsonAsync<CurrentUserResponse>("/me", TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("user-42", body.Id);
        Assert.Equal("Test user-42", body.DisplayName);
        Assert.Equal("user-42@tests.local", body.Email);
    }
}
```

`Errors/ErrorHandlingTests.cs` :

```csharp
using System.Net;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Web.Template.CQRS.Api.Errors;

namespace Web.Template.CQRS.Api.IntegrationTests.Errors;

public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task ValidationException_Returns400ProblemDetailsWithErrors()
    {
        using var host = await StartHostAsync();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await host.GetTestClient().GetAsync("/validation", cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("VALIDATION", problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.Equal(
            "Name is required.",
            problem.RootElement.GetProperty("errors").GetProperty("Name")[0].GetString());
    }

    [Fact]
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
    }

    private static async Task<IHost> StartHostAsync()
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddRouting())
                .Configure(app =>
                {
                    app.UseErrorHandling();
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/validation", ThrowValidation);
                        endpoints.MapGet("/boom", ThrowUnexpected);
                    });
                }))
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        return host;
    }

    private static IResult ThrowValidation() =>
        throw new ValidationException([new ValidationFailure("Name", "Name is required.")]);

    private static IResult ThrowUnexpected() =>
        throw new InvalidOperationException("Sensitive internal detail");
}
```

`RateLimiting/RateLimitingTests.cs` :

```csharp
using System.Net;
using System.Text.Json;
using Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

namespace Web.Template.CQRS.Api.IntegrationTests.RateLimiting;

public sealed class RateLimitingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // Matches the permit limit of the "read" policy in Api/Common/RateLimiting/RateLimiting.cs.
    private const int ReadPermitLimit = 600;

    [Fact]
    public async Task ReadPolicy_WhenQuotaExceeded_Returns429ProblemDetails()
    {
        var client = factory.CreateClient().AsUser("rate-limited-user");
        var cancellationToken = TestContext.Current.CancellationToken;

        for (var request = 0; request < ReadPermitLimit; request++)
        {
            var accepted = await client.GetAsync("/me", cancellationToken);
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        }

        var rejected = await client.GetAsync("/me", cancellationToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal("RATE_LIMITED", problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }
}
```

- [ ] **Step 6: Référencer les projets de tests dans les deux solutions**

Dans `Web.Template.CQRS.slnx` et `Web.Template.CQRS.Core.slnx`, ajouter juste avant `</Solution>` :

```xml
  <!--#if (EnableTests) -->
  <Folder Name="/tests/">
    <Project Path="tests/Web.Template.CQRS.Application.UnitTests/Web.Template.CQRS.Application.UnitTests.csproj" />
    <Project Path="tests/Web.Template.CQRS.Api.IntegrationTests/Web.Template.CQRS.Api.IntegrationTests.csproj" />
  </Folder>
  <!--#endif -->
```

- [ ] **Step 7: Lancer les tests**

Run: `dotnet test --solution Web.Template.CQRS.slnx`
Expected: tous les tests PASS (ils décrivent du comportement existant). En cas d'échec, corriger la cause (infrastructure de test) avant de continuer ; ne pas modifier le code de production dans cette tâche.

- [ ] **Step 8: Commit**

```bash
git add global.json Directory.Build.props Directory.Packages.props Web.Template.CQRS.slnx Web.Template.CQRS.Core.slnx tests
git commit -m "test: add unit and integration test projects pinning current API behavior

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Paramètres du template et script de vérification

**Files:**
- Modify: `.template.config/template.json`
- Create: `eng/verify-template.sh`

**Interfaces:**
- Consumes: projets de tests de Task 1 (chemins `tests/...`).
- Produces: symboles `AuthorizationModel`, `EnableTests`, `UseAppRoles`, `UseGroups`, `UseAuthorizationPolicies` ; script `eng/verify-template.sh <AuthorizationModel> <EnableAspire> <EnableRateLimiting> <EnableTests>` (sortie non nulle en cas d'échec). Les exclusions listent déjà les fichiers créés en Tasks 4 et 5.

- [ ] **Step 1: Écrire le script de vérification (le « test » de cette tâche)**

`eng/verify-template.sh` :

```bash
#!/usr/bin/env bash
# Generates a project from this template with the given options, then checks, builds and tests it.
# Usage: eng/verify-template.sh [AuthorizationModel] [EnableAspire] [EnableRateLimiting] [EnableTests]
set -euo pipefail

auth="${1:-AppRoles}"
aspire="${2:-true}"
rate_limiting="${3:-true}"
tests="${4:-true}"

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

echo "::group::Generate AuthorizationModel=$auth EnableAspire=$aspire EnableRateLimiting=$rate_limiting EnableTests=$tests"
dotnet new install "$root" --force
dotnet new templatewebcqrs \
  --name Ci.Generated \
  --output "$work/Ci.Generated" \
  --AuthorizationModel "$auth" \
  --EnableAspire "$aspire" \
  --EnableRateLimiting "$rate_limiting" \
  --EnableTests "$tests"
echo "::endgroup::"

cd "$work/Ci.Generated"

fail() {
  echo "verify-template: $1" >&2
  exit 1
}

for excluded in .idea eng docs Directory.Build.props .github/workflows/template-ci.yml src/Ci.Generated.AppHost/RealmVariants; do
  [ ! -e "$excluded" ] || fail "'$excluded' leaked into the generated project."
done
if find . \( -name bin -o -name obj \) -type d | grep -q .; then
  fail "bin/obj folders leaked into the generated project."
fi
if grep -rq "Web.Template.CQRS" --include='*.cs' --include='*.csproj' --include='*.slnx' --include='*.json' .; then
  fail "the source name 'Web.Template.CQRS' was not replaced everywhere."
fi
if [ "$tests" = "false" ] && [ -e tests ]; then
  fail "tests/ must not be generated when EnableTests=false."
fi
if [ "$aspire" = "true" ] && [ ! -f src/Ci.Generated.AppHost/Realms/template-realm.json ]; then
  fail "the Keycloak realm is missing."
fi

dotnet build Ci.Generated.slnx
if [ "$tests" = "true" ]; then
  dotnet test --solution Ci.Generated.slnx
fi
echo "verify-template: OK ($auth, aspire=$aspire, rateLimiting=$rate_limiting, tests=$tests)"
```

- [ ] **Step 2: Lancer le script avant de modifier template.json**

Run: `bash eng/verify-template.sh AppRolesAndGroups true true true`
Expected: FAIL — `dotnet new` rejette l'option inconnue `--AuthorizationModel` (ou, à défaut, `'.idea' leaked into the generated project.`).

- [ ] **Step 3: Réécrire `.template.config/template.json`**

```json
{
  "$schema": "http://json.schemastore.org/template",
  "author": "Florian Drevet",
  "classifications": [
    "Web"
  ],
  "name": "Web template using CQRS",
  "description": "This template creates a new web application using CQRS",
  "tags": {
    "language": "C#",
    "type": "project"
  },
  "shortName": "templatewebcqrs",
  "sourceName": "Web.Template.CQRS",
  "identity": "Web.Template.CQRS",
  "preferNameDirectory": true,
  "symbols": {
    "EnableAspire": {
      "type": "parameter",
      "datatype": "bool",
      "defaultValue": "true",
      "description": "Ajoute Aspire, l'observabilité OpenTelemetry et les services locaux SQL Server, Azurite et Keycloak."
    },
    "EnableRateLimiting": {
      "type": "parameter",
      "datatype": "bool",
      "defaultValue": "true",
      "description": "Ajoute les politiques de limitation de débit de l'API."
    },
    "AuthorizationModel": {
      "type": "parameter",
      "datatype": "choice",
      "defaultValue": "AppRoles",
      "description": "Modèle d'autorisation Entra ID reproduit localement par Keycloak.",
      "choices": [
        {
          "choice": "Authenticated",
          "description": "Utilisateur connecté uniquement."
        },
        {
          "choice": "AppRoles",
          "description": "App roles Entra (claim roles)."
        },
        {
          "choice": "Groups",
          "description": "Groupes de sécurité Entra (claim groups)."
        },
        {
          "choice": "AppRolesAndGroups",
          "description": "App roles et groupes de sécurité."
        }
      ]
    },
    "EnableTests": {
      "type": "parameter",
      "datatype": "bool",
      "defaultValue": "true",
      "description": "Ajoute les projets de tests unitaires et d'intégration."
    },
    "UseAppRoles": {
      "type": "computed",
      "value": "(AuthorizationModel == \"AppRoles\" || AuthorizationModel == \"AppRolesAndGroups\")"
    },
    "UseGroups": {
      "type": "computed",
      "value": "(AuthorizationModel == \"Groups\" || AuthorizationModel == \"AppRolesAndGroups\")"
    },
    "UseAuthorizationPolicies": {
      "type": "computed",
      "value": "(AuthorizationModel != \"Authenticated\")"
    }
  },
  "sources": [
    {
      "source": "./",
      "target": "./",
      "modifiers": [
        {
          "exclude": [
            ".idea/**",
            "eng/**",
            "docs/**",
            ".github/workflows/template-ci.yml",
            "Directory.Build.props"
          ]
        },
        {
          "condition": "(!EnableAspire)",
          "exclude": [
            "src/Web.Template.CQRS.AppHost/**",
            "src/Web.Template.CQRS.ServiceDefaults/**",
            "scripts/**",
            "Web.Template.CQRS.slnx"
          ],
          "rename": {
            "Web.Template.CQRS.Core.slnx": "Web.Template.CQRS.slnx"
          }
        },
        {
          "condition": "(EnableAspire)",
          "exclude": [
            "Web.Template.CQRS.Core.slnx"
          ]
        },
        {
          "condition": "(!EnableRateLimiting)",
          "exclude": [
            "src/Web.Template.CQRS.Api/Common/RateLimiting/**",
            "tests/Web.Template.CQRS.Api.IntegrationTests/RateLimiting/**"
          ]
        },
        {
          "condition": "(!EnableTests)",
          "exclude": [
            "tests/**"
          ]
        },
        {
          "condition": "(!UseAuthorizationPolicies)",
          "exclude": [
            "src/Web.Template.CQRS.Api/Authorization/Policies.cs",
            "src/Web.Template.CQRS.Api/Controllers/AdminController.cs",
            "src/Web.Template.CQRS.Application/Admin/**",
            "src/Web.Template.CQRS.Contracts/Admin/**",
            "tests/Web.Template.CQRS.Application.UnitTests/Admin/**",
            "tests/Web.Template.CQRS.Api.IntegrationTests/Admin/**"
          ]
        },
        {
          "condition": "(!UseAppRoles)",
          "exclude": [
            "src/Web.Template.CQRS.Api/Authorization/AppRoles.cs"
          ]
        },
        {
          "condition": "(!UseGroups)",
          "exclude": [
            "src/Web.Template.CQRS.Api/Authorization/GroupOptions.cs",
            "tests/Web.Template.CQRS.Api.IntegrationTests/Authorization/**"
          ]
        },
        {
          "condition": "(AuthorizationModel == \"Authenticated\")",
          "exclude": [
            "src/Web.Template.CQRS.AppHost/Realms/template-realm.json",
            "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.approles.json",
            "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.groups.json"
          ],
          "rename": {
            "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.authenticated.json": "src/Web.Template.CQRS.AppHost/Realms/template-realm.json"
          }
        },
        {
          "condition": "(AuthorizationModel == \"AppRoles\")",
          "exclude": [
            "src/Web.Template.CQRS.AppHost/Realms/template-realm.json",
            "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.authenticated.json",
            "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.groups.json"
          ],
          "rename": {
            "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.approles.json": "src/Web.Template.CQRS.AppHost/Realms/template-realm.json"
          }
        },
        {
          "condition": "(AuthorizationModel == \"Groups\")",
          "exclude": [
            "src/Web.Template.CQRS.AppHost/Realms/template-realm.json",
            "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.authenticated.json",
            "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.approles.json"
          ],
          "rename": {
            "src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.groups.json": "src/Web.Template.CQRS.AppHost/Realms/template-realm.json"
          }
        },
        {
          "condition": "(AuthorizationModel == \"AppRolesAndGroups\")",
          "exclude": [
            "src/Web.Template.CQRS.AppHost/RealmVariants/**"
          ]
        }
      ]
    }
  ],
  "guids": [
    "bdab3963-4400-4b37-9c1d-7148b0a3a96c",
    "fc205b91-e44d-4eb0-a0dd-8ad940659771"
  ]
}
```

- [ ] **Step 4: Relancer la vérification sur les combinaisons disponibles à ce stade**

Run: `bash eng/verify-template.sh AppRolesAndGroups true true true`
Expected: `verify-template: OK (AppRolesAndGroups, aspire=true, rateLimiting=true, tests=true)`.

Run: `bash eng/verify-template.sh Authenticated false false false`
Expected: `verify-template: OK (Authenticated, aspire=false, rateLimiting=false, tests=false)`.

(Les combinaisons `AppRoles` et `Groups` avec Aspire échoueront sur « the Keycloak realm is missing » tant que Task 5 n'a pas créé les variantes : c'est attendu, elles sont vérifiées en Task 7.)

Si `dotnet test --solution` est refusé par la CLI, remplacer par `dotnet test Ci.Generated.slnx` dans le script.

- [ ] **Step 5: Commit**

```bash
git add .template.config/template.json eng/verify-template.sh
git commit -m "feat(template): add AuthorizationModel and EnableTests options with generation checks

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Rôles et groupes de l'utilisateur courant (`/me`)

**Files:**
- Create: `src/Web.Template.CQRS.Api/Authentication/ClaimNames.cs`
- Modify: `src/Web.Template.CQRS.Application/Common/Interfaces/Authentication/ICurrentUser.cs`, `src/Web.Template.CQRS.Api/Authentication/HttpCurrentUser.cs`, `src/Web.Template.CQRS.Application/Users/Common/CurrentUserResult.cs`, `src/Web.Template.CQRS.Application/Users/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs`, `src/Web.Template.CQRS.Contracts/Authentication/CurrentUserResponse.cs`, `src/Web.Template.CQRS.Api/Controllers/CurrentUserController.cs`
- Test: `tests/Web.Template.CQRS.Application.UnitTests/Fakes/FakeCurrentUser.cs`, `tests/Web.Template.CQRS.Application.UnitTests/Users/GetCurrentUserQueryHandlerTests.cs`, `tests/Web.Template.CQRS.Api.IntegrationTests/Users/CurrentUserEndpointTests.cs`

**Interfaces:**
- Consumes: `TestUserExtensions.AsUser(...)`, `ApiFactory` (Task 1).
- Produces:
  - `ClaimNames` (namespace `Web.Template.CQRS.Api.Authentication`) : `ObjectId = "oid"`, `Subject = "sub"`, `Name = "name"`, `Email = "email"`, `Roles = "roles"`, `Groups = "groups"`.
  - `ICurrentUser.Roles : IReadOnlyList<string>`, `ICurrentUser.Groups : IReadOnlyList<string>`.
  - `CurrentUserResult(string? Id, string? DisplayName, string? Email, IReadOnlyList<string> Roles, IReadOnlyList<string> Groups)`.
  - `CurrentUserResponse(string? Id, string? DisplayName, string? Email, IReadOnlyList<string> Roles, IReadOnlyList<string> Groups)`.

- [ ] **Step 1: Écrire les tests en échec**

Remplacer `tests/.../Application.UnitTests/Fakes/FakeCurrentUser.cs` par :

```csharp
using Web.Template.CQRS.Application.Common.Interfaces.Authentication;

namespace Web.Template.CQRS.Application.UnitTests.Fakes;

public sealed class FakeCurrentUser : ICurrentUser
{
    public string? Id { get; init; }

    public string? DisplayName { get; init; }

    public string? Email { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];

    public IReadOnlyList<string> Groups { get; init; } = [];
}
```

Dans `GetCurrentUserQueryHandlerTests`, remplacer le test par :

```csharp
    [Fact]
    public async Task Handle_ReturnsCurrentUserIdentityRolesAndGroups()
    {
        var currentUser = new FakeCurrentUser
        {
            Id = "user-1",
            DisplayName = "Ada Lovelace",
            Email = "ada@contoso.test",
            Roles = ["Admin"],
            Groups = ["6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"]
        };
        var handler = new GetCurrentUserQueryHandler(currentUser);

        var result = await handler.Handle(new GetCurrentUserQuery(), TestContext.Current.CancellationToken);

        Assert.Equal("user-1", result.Id);
        Assert.Equal("Ada Lovelace", result.DisplayName);
        Assert.Equal("ada@contoso.test", result.Email);
        Assert.Equal(["Admin"], result.Roles);
        Assert.Equal(["6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"], result.Groups);
    }
```

Dans `CurrentUserEndpointTests`, remplacer `Me_WithAuthenticatedUser_ReturnsIdentityClaims` et ajouter deux tests :

```csharp
    [Fact]
    public async Task Me_WithAuthenticatedUser_ReturnsIdentityRolesAndGroups()
    {
        var client = factory.CreateClient().AsUser(
            "user-42",
            roles: ["Admin", "User"],
            groups: ["6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"]);

        var body = await client.GetFromJsonAsync<CurrentUserResponse>("/me", TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("user-42", body.Id);
        Assert.Equal("Test user-42", body.DisplayName);
        Assert.Equal("user-42@tests.local", body.Email);
        Assert.Equal(["Admin", "User"], body.Roles);
        Assert.Equal(["6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"], body.Groups);
    }

    [Fact]
    public async Task Me_WithUserWithoutRolesOrGroups_ReturnsEmptyArrays()
    {
        var client = factory.CreateClient().AsUser("user-without-rights");

        var response = await client.GetAsync("/me", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"roles\":[]", json);
        Assert.Contains("\"groups\":[]", json);
    }

    [Fact]
    public async Task Me_WithRolesHeaderButNoUser_Returns401()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "Admin");

        var response = await client.GetAsync("/me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
```

- [ ] **Step 2: Vérifier que les tests échouent**

Run: `dotnet test --solution Web.Template.CQRS.slnx`
Expected: FAIL à la compilation — `FakeCurrentUser` déclare `Roles`/`Groups` absents de `ICurrentUser`, et `CurrentUserResult`/`CurrentUserResponse` n'ont pas de `Roles`/`Groups`.

- [ ] **Step 3: Implémenter**

`src/Web.Template.CQRS.Api/Authentication/ClaimNames.cs` :

```csharp
namespace Web.Template.CQRS.Api.Authentication;

public static class ClaimNames
{
    public const string ObjectId = "oid";
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string Roles = "roles";
    public const string Groups = "groups";
}
```

`ICurrentUser.cs` :

```csharp
namespace Web.Template.CQRS.Application.Common.Interfaces.Authentication;

public interface ICurrentUser
{
    string? Id { get; }

    string? DisplayName { get; }

    string? Email { get; }

    IReadOnlyList<string> Roles { get; }

    IReadOnlyList<string> Groups { get; }
}
```

`HttpCurrentUser.cs` :

```csharp
using System.Security.Claims;
using Web.Template.CQRS.Application.Common.Interfaces.Authentication;

namespace Web.Template.CQRS.Api.Authentication;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string? Id => Principal?.FindFirst(ClaimNames.ObjectId)?.Value
        ?? Principal?.FindFirst(ClaimNames.Subject)?.Value
        ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public string? DisplayName => Principal?.FindFirst(ClaimNames.Name)?.Value
        ?? Principal?.Identity?.Name;

    public string? Email => Principal?.FindFirst(ClaimNames.Email)?.Value
        ?? Principal?.FindFirst("preferred_username")?.Value
        ?? Principal?.FindFirst("upn")?.Value;

    public IReadOnlyList<string> Roles => ClaimValues(ClaimNames.Roles);

    public IReadOnlyList<string> Groups => ClaimValues(ClaimNames.Groups);

    private string[] ClaimValues(string claimType) =>
        Principal?.FindAll(claimType).Select(claim => claim.Value).ToArray() ?? [];
}
```

`CurrentUserResult.cs` :

```csharp
namespace Web.Template.CQRS.Application.Users.Common;

public sealed record CurrentUserResult(
    string? Id,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Groups);
```

Dans `GetCurrentUserQueryHandler.Handle`, remplacer la construction du résultat par :

```csharp
        var result = new CurrentUserResult(
            currentUser.Id,
            currentUser.DisplayName,
            currentUser.Email,
            currentUser.Roles,
            currentUser.Groups);
```

`CurrentUserResponse.cs` :

```csharp
namespace Web.Template.CQRS.Contracts.Authentication;

public sealed record CurrentUserResponse(
    string? Id,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Groups);
```

Dans `CurrentUserController.cs`, remplacer le `return` du handler par :

```csharp
                return TypedResults.Ok(new CurrentUserResponse(
                    currentUser.Id,
                    currentUser.DisplayName,
                    currentUser.Email,
                    currentUser.Roles,
                    currentUser.Groups));
```

- [ ] **Step 4: Vérifier que les tests passent**

Run: `dotnet test --solution Web.Template.CQRS.slnx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat(api): expose roles and groups claims on the current user

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Policies d'autorisation et endpoint `/admin/ping`

**Files:**
- Create: `src/Web.Template.CQRS.Api/Authorization/AppRoles.cs`, `GroupOptions.cs`, `Policies.cs`, `AuthorizationSetup.cs`
- Create: `src/Web.Template.CQRS.Api/Controllers/AdminController.cs`
- Create: `src/Web.Template.CQRS.Application/Admin/Common/PingAdminResult.cs`, `Admin/Queries/PingAdmin/PingAdminQuery.cs`, `Admin/Queries/PingAdmin/PingAdminQueryHandler.cs`
- Create: `src/Web.Template.CQRS.Contracts/Admin/PingAdminResponse.cs`
- Modify: `src/Web.Template.CQRS.Api/DependencyInjection.cs`, `src/Web.Template.CQRS.Api/Program.cs`, `src/Web.Template.CQRS.Api/appsettings.json`, `src/Web.Template.CQRS.Api/appsettings.Development.json`
- Modify: `tests/Web.Template.CQRS.Api.IntegrationTests/Infrastructure/ApiFactory.cs`
- Test: `tests/Web.Template.CQRS.Application.UnitTests/Admin/PingAdminQueryHandlerTests.cs`, `tests/Web.Template.CQRS.Api.IntegrationTests/Admin/AdminEndpointTests.cs`, `tests/Web.Template.CQRS.Api.IntegrationTests/Authorization/GroupOptionsValidationTests.cs`

**Interfaces:**
- Consumes: `ClaimNames`, `ICurrentUser` (Task 3), `ApiFactory`, `AsUser` (Task 1).
- Produces:
  - `AppRoles.Admin = "Admin"`, `AppRoles.User = "User"` ; `Policies.RequireAdmin = "RequireAdmin"` ; `GroupOptions { const SectionName = "Authorization:Groups"; string Administrators }`.
  - `AuthorizationSetup.AddTemplateAuthorization(this IServiceCollection, IConfiguration) : IServiceCollection`.
  - `AdminController.MapAdminEndpoints(this IEndpointRouteBuilder) : IEndpointRouteBuilder` → `GET /admin/ping`.
  - `PingAdminQuery : IQuery<PingAdminResult>`, `PingAdminResult(string Message, string? UserId)`, `PingAdminResponse(string Message, string? UserId)`.
  - `ApiFactory.AdministratorsGroupId = "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"` (sous `#if (UseGroups)`).

- [ ] **Step 1: Écrire les tests en échec**

`tests/Web.Template.CQRS.Application.UnitTests/Admin/PingAdminQueryHandlerTests.cs` :

```csharp
using Web.Template.CQRS.Application.Admin.Queries.PingAdmin;
using Web.Template.CQRS.Application.UnitTests.Fakes;

namespace Web.Template.CQRS.Application.UnitTests.Admin;

public sealed class PingAdminQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPongWithCurrentUserId()
    {
        var handler = new PingAdminQueryHandler(new FakeCurrentUser { Id = "admin-1" });

        var result = await handler.Handle(new PingAdminQuery(), TestContext.Current.CancellationToken);

        Assert.Equal("pong", result.Message);
        Assert.Equal("admin-1", result.UserId);
    }
}
```

Dans `ApiFactory`, ajouter la constante en tête de classe et le réglage juste après `builder.UseSetting("Auth:Audience", "template-api");` :

```csharp
#if (UseGroups)
    public const string AdministratorsGroupId = "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11";

#endif
```

```csharp
#if (UseGroups)
        builder.UseSetting("Authorization:Groups:Administrators", AdministratorsGroupId);
#endif
```

`tests/Web.Template.CQRS.Api.IntegrationTests/Admin/AdminEndpointTests.cs` :

```csharp
using System.Net;
using System.Net.Http.Json;
using Web.Template.CQRS.Api.IntegrationTests.Infrastructure;
using Web.Template.CQRS.Contracts.Admin;
#if (UseAppRoles)
using Web.Template.CQRS.Api.Authorization;
#endif

namespace Web.Template.CQRS.Api.IntegrationTests.Admin;

public sealed class AdminEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Ping_WithoutAuthentication_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/admin/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ping_AsStandardUser_Returns403()
    {
#if (UseAppRoles)
        var client = factory.CreateClient().AsUser("standard-user", roles: [AppRoles.User]);
#else
        var client = factory.CreateClient().AsUser("standard-user");
#endif

        var response = await client.GetAsync("/admin/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
#if (UseAppRoles)

    [Fact]
    public async Task Ping_WithAdminRole_Returns200()
    {
        var client = factory.CreateClient().AsUser("admin-by-role", roles: [AppRoles.Admin]);

        var body = await client.GetFromJsonAsync<PingAdminResponse>("/admin/ping", TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("pong", body.Message);
        Assert.Equal("admin-by-role", body.UserId);
    }
#endif
#if (UseGroups)

    [Fact]
    public async Task Ping_AsMemberOfAdministratorsGroup_Returns200()
    {
        var client = factory.CreateClient().AsUser("admin-by-group", groups: [ApiFactory.AdministratorsGroupId]);

        var body = await client.GetFromJsonAsync<PingAdminResponse>("/admin/ping", TestContext.Current.CancellationToken);

        Assert.NotNull(body);
        Assert.Equal("admin-by-group", body.UserId);
    }

    [Fact]
    public async Task Ping_WithGroupIdInDifferentCase_Returns200()
    {
        var client = factory.CreateClient().AsUser(
            "admin-by-group-uppercase",
            groups: [ApiFactory.AdministratorsGroupId.ToUpperInvariant()]);

        var response = await client.GetAsync("/admin/ping", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
#endif
}
```

`tests/Web.Template.CQRS.Api.IntegrationTests/Authorization/GroupOptionsValidationTests.cs` :

```csharp
using Microsoft.AspNetCore.Hosting;
using Web.Template.CQRS.Api.IntegrationTests.Infrastructure;

namespace Web.Template.CQRS.Api.IntegrationTests.Authorization;

public sealed class GroupOptionsValidationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void Startup_WithInvalidAdministratorsGroupId_Fails(string groupId)
    {
        using var invalidFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Authorization:Groups:Administrators", groupId));

        var exception = Record.Exception(() => invalidFactory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("Authorization:Groups:Administrators", exception.ToString());
    }
}
```

- [ ] **Step 2: Vérifier que les tests échouent**

Run: `dotnet test --solution Web.Template.CQRS.slnx`
Expected: FAIL à la compilation — `PingAdminQuery`, `PingAdminQueryHandler`, `PingAdminResponse` et `AppRoles` n'existent pas.

- [ ] **Step 3: Implémenter la couche Application et Contracts**

`src/Web.Template.CQRS.Application/Admin/Common/PingAdminResult.cs` :

```csharp
namespace Web.Template.CQRS.Application.Admin.Common;

public sealed record PingAdminResult(string Message, string? UserId);
```

`src/Web.Template.CQRS.Application/Admin/Queries/PingAdmin/PingAdminQuery.cs` :

```csharp
using Mediator;
using Web.Template.CQRS.Application.Admin.Common;

namespace Web.Template.CQRS.Application.Admin.Queries.PingAdmin;

public sealed record PingAdminQuery : IQuery<PingAdminResult>;
```

`src/Web.Template.CQRS.Application/Admin/Queries/PingAdmin/PingAdminQueryHandler.cs` :

```csharp
using Mediator;
using Web.Template.CQRS.Application.Admin.Common;
using Web.Template.CQRS.Application.Common.Interfaces.Authentication;

namespace Web.Template.CQRS.Application.Admin.Queries.PingAdmin;

public sealed class PingAdminQueryHandler(ICurrentUser currentUser)
    : IQueryHandler<PingAdminQuery, PingAdminResult>
{
    public ValueTask<PingAdminResult> Handle(
        PingAdminQuery query,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new PingAdminResult("pong", currentUser.Id));
    }
}
```

`src/Web.Template.CQRS.Contracts/Admin/PingAdminResponse.cs` :

```csharp
namespace Web.Template.CQRS.Contracts.Admin;

public sealed record PingAdminResponse(string Message, string? UserId);
```

- [ ] **Step 4: Implémenter l'autorisation dans l'API**

`src/Web.Template.CQRS.Api/Authorization/AppRoles.cs` :

```csharp
namespace Web.Template.CQRS.Api.Authorization;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
}
```

`src/Web.Template.CQRS.Api/Authorization/GroupOptions.cs` :

```csharp
namespace Web.Template.CQRS.Api.Authorization;

public sealed class GroupOptions
{
    public const string SectionName = "Authorization:Groups";

    public string Administrators { get; set; } = string.Empty;
}
```

`src/Web.Template.CQRS.Api/Authorization/Policies.cs` :

```csharp
namespace Web.Template.CQRS.Api.Authorization;

public static class Policies
{
    public const string RequireAdmin = "RequireAdmin";
}
```

`src/Web.Template.CQRS.Api/Authorization/AuthorizationSetup.cs` :

```csharp
using Microsoft.AspNetCore.Authorization;
#if (UseGroups)
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Web.Template.CQRS.Api.Authentication;
#endif

namespace Web.Template.CQRS.Api.Authorization;

public static class AuthorizationSetup
{
    public static IServiceCollection AddTemplateAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
#if (UseGroups)
        services.AddOptions<GroupOptions>()
            .Bind(configuration.GetSection(GroupOptions.SectionName))
            .Validate(options => Guid.TryParse(options.Administrators, out _),
                "Authorization:Groups:Administrators must be the object ID (GUID) of an Entra security group.")
            .ValidateOnStart();

#endif
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });
#if (UseAuthorizationPolicies)

        services.AddOptions<AuthorizationOptions>()
            .Configure<IServiceProvider>((options, serviceProvider) =>
                options.AddPolicy(Policies.RequireAdmin, policy => ConfigureRequireAdmin(policy, serviceProvider)));
#endif

        return services;
    }
#if (UseAuthorizationPolicies)

    private static void ConfigureRequireAdmin(AuthorizationPolicyBuilder policy, IServiceProvider serviceProvider)
    {
#if (UseGroups)
        var administratorsGroupId = serviceProvider
            .GetRequiredService<IOptions<GroupOptions>>()
            .Value
            .Administrators;
#endif
#if (UseAppRoles && UseGroups)
        policy.RequireAssertion(context =>
            context.User.IsInRole(AppRoles.Admin)
            || IsMemberOf(context.User, administratorsGroupId));
#elif (UseAppRoles)
        policy.RequireRole(AppRoles.Admin);
#else
        policy.RequireAssertion(context => IsMemberOf(context.User, administratorsGroupId));
#endif
    }
#endif
#if (UseGroups)

    private static bool IsMemberOf(ClaimsPrincipal user, string groupObjectId) =>
        user.FindAll(ClaimNames.Groups)
            .Any(claim => string.Equals(claim.Value, groupObjectId, StringComparison.OrdinalIgnoreCase));
#endif
}
```

Remplacer `src/Web.Template.CQRS.Api/DependencyInjection.cs` par :

```csharp
using Web.Template.CQRS.Api.Authentication;
using Web.Template.CQRS.Api.Authorization;
using Web.Template.CQRS.Application.Common.Interfaces.Authentication;

namespace Web.Template.CQRS.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddTemplateAuthentication(configuration, environment);
        services.AddTemplateAuthorization(configuration);
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        return services;
    }
}
```

`src/Web.Template.CQRS.Api/Controllers/AdminController.cs` :

```csharp
using Mediator;
using Web.Template.CQRS.Api.Authorization;
using Web.Template.CQRS.Application.Admin.Queries.PingAdmin;
using Web.Template.CQRS.Contracts.Admin;

#if (EnableRateLimiting)
using Microsoft.AspNetCore.RateLimiting;
using Web.Template.CQRS.Api.Common.RateLimiting;
#endif

namespace Web.Template.CQRS.Api.Controllers;

public static class AdminController
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var pingEndpoint = endpoints.MapGet("/admin/ping", async (
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(new PingAdminQuery(), cancellationToken);
                return TypedResults.Ok(new PingAdminResponse(result.Message, result.UserId));
            })
            .WithName("PingAdmin")
            .RequireAuthorization(Policies.RequireAdmin);

#if (EnableRateLimiting)
        pingEndpoint.RequireRateLimiting(RateLimitingPolicies.Read);
#endif

        return endpoints;
    }
}
```

Dans `Program.cs`, juste après `app.MapCurrentUserEndpoints();` :

```csharp
#if (UseAuthorizationPolicies)
app.MapAdminEndpoints();
#endif
```

Dans `appsettings.json`, insérer après le bloc `"Auth": { ... },` :

```json
  //#if (UseGroups)
  "Authorization": {
    "Groups": {
      "Administrators": ""
    }
  },
  //#endif
```

Dans `appsettings.Development.json`, à l'intérieur du bloc `//#if (EnableAspire)`, juste après le bloc `"Auth": { ... },` :

```json
  //#if (UseGroups)
  "Authorization": {
    "Groups": {
      "Administrators": "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"
    }
  },
  //#endif
```

- [ ] **Step 5: Vérifier que les tests passent**

Run: `dotnet test --solution Web.Template.CQRS.slnx`
Expected: PASS (le dépôt compile la variante `AppRolesAndGroups` : rôle, groupe, groupe en majuscules, 403, 401, validation de démarrage).

- [ ] **Step 6: Vérifier les autres variantes de code**

Run: `bash eng/verify-template.sh Authenticated false true true`
Expected: OK (pas de `/admin/ping`, pas de policies).

Run: `bash eng/verify-template.sh AppRoles false true true`
Expected: OK.

Run: `bash eng/verify-template.sh Groups false true true`
Expected: OK.

(Variantes sans Aspire : les realms de Task 5 ne sont pas encore requis.)

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "feat(api): add role and group based authorization policies with admin ping endpoint

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Realm Keycloak miroir d'Entra, personas et obtention de token

**Files:**
- Modify (réécriture): `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`
- Create: `src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.authenticated.json`, `template-realm.approles.json`, `template-realm.groups.json`
- Create: `scripts/get-token.ps1`, `scripts/get-token.sh`
- Modify: `src/Web.Template.CQRS.Api/Api.http`

**Interfaces:**
- Consumes: claims attendus par l'API (Task 3/4), GUID `6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11` de `appsettings.Development.json`.
- Produces: realm `template` avec clients `template-api`, `template-scalar`, `template-cli` ; personas `admin@local.test`, `user@local.test` ; scripts `get-token` (`-Persona`/1er argument, `-Password`, `-KeycloakUrl`) qui écrivent l'access token sur stdout.

- [ ] **Step 1: Réécrire `Realms/template-realm.json` (variante AppRolesAndGroups)**

```json
{
  "realm": "template",
  "enabled": true,
  "displayName": "Template local development",
  "roles": {
    "realm": [
      { "name": "Admin", "description": "Mirrors the Entra app role Admin." },
      { "name": "User", "description": "Mirrors the Entra app role User." }
    ]
  },
  "groups": [
    {
      "name": "Administrators",
      "path": "/Administrators",
      "attributes": {
        "objectId": [ "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11" ]
      }
    }
  ],
  "users": [
    {
      "username": "admin@local.test",
      "email": "admin@local.test",
      "emailVerified": true,
      "enabled": true,
      "firstName": "Alice",
      "lastName": "Admin",
      "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ],
      "realmRoles": [ "Admin" ],
      "groups": [ "/Administrators" ]
    },
    {
      "username": "user@local.test",
      "email": "user@local.test",
      "emailVerified": true,
      "enabled": true,
      "firstName": "Bob",
      "lastName": "User",
      "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ],
      "realmRoles": [ "User" ]
    }
  ],
  "clients": [
    {
      "clientId": "template-api",
      "name": "Template API",
      "enabled": true,
      "bearerOnly": true,
      "protocol": "openid-connect",
      "publicClient": false,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": false
    },
    {
      "clientId": "template-scalar",
      "name": "Template Scalar API reference",
      "enabled": true,
      "protocol": "openid-connect",
      "publicClient": true,
      "standardFlowEnabled": true,
      "directAccessGrantsEnabled": false,
      "attributes": { "pkce.code.challenge.method": "S256" },
      "redirectUris": [ "http://localhost:5257/*", "https://localhost:7246/*" ],
      "webOrigins": [ "http://localhost:5257", "https://localhost:7246" ],
      "protocolMappers": [
        {
          "name": "template-api-audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": { "included.client.audience": "template-api", "id.token.claim": "false", "access.token.claim": "true", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-oid",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-property-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "id", "claim.name": "oid", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-roles",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-realm-role-mapper",
          "consentRequired": false,
          "config": { "claim.name": "roles", "multivalued": "true", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-groups",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-attribute-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "objectId", "claim.name": "groups", "multivalued": "true", "aggregate.attrs": "true", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        }
      ]
    },
    {
      "clientId": "template-cli",
      "name": "Template local CLI (password grant, local development only)",
      "enabled": true,
      "protocol": "openid-connect",
      "publicClient": true,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": true,
      "protocolMappers": [
        {
          "name": "template-api-audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": { "included.client.audience": "template-api", "id.token.claim": "false", "access.token.claim": "true", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-oid",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-property-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "id", "claim.name": "oid", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-roles",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-realm-role-mapper",
          "consentRequired": false,
          "config": { "claim.name": "roles", "multivalued": "true", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-groups",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-attribute-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "objectId", "claim.name": "groups", "multivalued": "true", "aggregate.attrs": "true", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        }
      ]
    }
  ]
}
```

- [ ] **Step 2: Créer `RealmVariants/template-realm.approles.json`**

Identique au fichier de Step 1 **sans** : le tableau `groups` du realm, la propriété `"groups": [ "/Administrators" ]` de `admin@local.test`, et le mapper `entra-groups` des deux clients. Contenu complet :

```json
{
  "realm": "template",
  "enabled": true,
  "displayName": "Template local development",
  "roles": {
    "realm": [
      { "name": "Admin", "description": "Mirrors the Entra app role Admin." },
      { "name": "User", "description": "Mirrors the Entra app role User." }
    ]
  },
  "users": [
    {
      "username": "admin@local.test",
      "email": "admin@local.test",
      "emailVerified": true,
      "enabled": true,
      "firstName": "Alice",
      "lastName": "Admin",
      "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ],
      "realmRoles": [ "Admin" ]
    },
    {
      "username": "user@local.test",
      "email": "user@local.test",
      "emailVerified": true,
      "enabled": true,
      "firstName": "Bob",
      "lastName": "User",
      "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ],
      "realmRoles": [ "User" ]
    }
  ],
  "clients": [
    {
      "clientId": "template-api",
      "name": "Template API",
      "enabled": true,
      "bearerOnly": true,
      "protocol": "openid-connect",
      "publicClient": false,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": false
    },
    {
      "clientId": "template-scalar",
      "name": "Template Scalar API reference",
      "enabled": true,
      "protocol": "openid-connect",
      "publicClient": true,
      "standardFlowEnabled": true,
      "directAccessGrantsEnabled": false,
      "attributes": { "pkce.code.challenge.method": "S256" },
      "redirectUris": [ "http://localhost:5257/*", "https://localhost:7246/*" ],
      "webOrigins": [ "http://localhost:5257", "https://localhost:7246" ],
      "protocolMappers": [
        {
          "name": "template-api-audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": { "included.client.audience": "template-api", "id.token.claim": "false", "access.token.claim": "true", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-oid",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-property-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "id", "claim.name": "oid", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-roles",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-realm-role-mapper",
          "consentRequired": false,
          "config": { "claim.name": "roles", "multivalued": "true", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        }
      ]
    },
    {
      "clientId": "template-cli",
      "name": "Template local CLI (password grant, local development only)",
      "enabled": true,
      "protocol": "openid-connect",
      "publicClient": true,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": true,
      "protocolMappers": [
        {
          "name": "template-api-audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": { "included.client.audience": "template-api", "id.token.claim": "false", "access.token.claim": "true", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-oid",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-property-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "id", "claim.name": "oid", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-roles",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-realm-role-mapper",
          "consentRequired": false,
          "config": { "claim.name": "roles", "multivalued": "true", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        }
      ]
    }
  ]
}
```

- [ ] **Step 3: Créer `RealmVariants/template-realm.groups.json`**

```json
{
  "realm": "template",
  "enabled": true,
  "displayName": "Template local development",
  "groups": [
    {
      "name": "Administrators",
      "path": "/Administrators",
      "attributes": {
        "objectId": [ "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11" ]
      }
    }
  ],
  "users": [
    {
      "username": "admin@local.test",
      "email": "admin@local.test",
      "emailVerified": true,
      "enabled": true,
      "firstName": "Alice",
      "lastName": "Admin",
      "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ],
      "groups": [ "/Administrators" ]
    },
    {
      "username": "user@local.test",
      "email": "user@local.test",
      "emailVerified": true,
      "enabled": true,
      "firstName": "Bob",
      "lastName": "User",
      "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ]
    }
  ],
  "clients": [
    {
      "clientId": "template-api",
      "name": "Template API",
      "enabled": true,
      "bearerOnly": true,
      "protocol": "openid-connect",
      "publicClient": false,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": false
    },
    {
      "clientId": "template-scalar",
      "name": "Template Scalar API reference",
      "enabled": true,
      "protocol": "openid-connect",
      "publicClient": true,
      "standardFlowEnabled": true,
      "directAccessGrantsEnabled": false,
      "attributes": { "pkce.code.challenge.method": "S256" },
      "redirectUris": [ "http://localhost:5257/*", "https://localhost:7246/*" ],
      "webOrigins": [ "http://localhost:5257", "https://localhost:7246" ],
      "protocolMappers": [
        {
          "name": "template-api-audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": { "included.client.audience": "template-api", "id.token.claim": "false", "access.token.claim": "true", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-oid",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-property-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "id", "claim.name": "oid", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-groups",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-attribute-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "objectId", "claim.name": "groups", "multivalued": "true", "aggregate.attrs": "true", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        }
      ]
    },
    {
      "clientId": "template-cli",
      "name": "Template local CLI (password grant, local development only)",
      "enabled": true,
      "protocol": "openid-connect",
      "publicClient": true,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": true,
      "protocolMappers": [
        {
          "name": "template-api-audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": { "included.client.audience": "template-api", "id.token.claim": "false", "access.token.claim": "true", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-oid",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-property-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "id", "claim.name": "oid", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-groups",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-attribute-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "objectId", "claim.name": "groups", "multivalued": "true", "aggregate.attrs": "true", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        }
      ]
    }
  ]
}
```

- [ ] **Step 4: Créer `RealmVariants/template-realm.authenticated.json`**

```json
{
  "realm": "template",
  "enabled": true,
  "displayName": "Template local development",
  "users": [
    {
      "username": "admin@local.test",
      "email": "admin@local.test",
      "emailVerified": true,
      "enabled": true,
      "firstName": "Alice",
      "lastName": "Admin",
      "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ]
    },
    {
      "username": "user@local.test",
      "email": "user@local.test",
      "emailVerified": true,
      "enabled": true,
      "firstName": "Bob",
      "lastName": "User",
      "credentials": [ { "type": "password", "value": "Passw0rd!", "temporary": false } ]
    }
  ],
  "clients": [
    {
      "clientId": "template-api",
      "name": "Template API",
      "enabled": true,
      "bearerOnly": true,
      "protocol": "openid-connect",
      "publicClient": false,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": false
    },
    {
      "clientId": "template-scalar",
      "name": "Template Scalar API reference",
      "enabled": true,
      "protocol": "openid-connect",
      "publicClient": true,
      "standardFlowEnabled": true,
      "directAccessGrantsEnabled": false,
      "attributes": { "pkce.code.challenge.method": "S256" },
      "redirectUris": [ "http://localhost:5257/*", "https://localhost:7246/*" ],
      "webOrigins": [ "http://localhost:5257", "https://localhost:7246" ],
      "protocolMappers": [
        {
          "name": "template-api-audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": { "included.client.audience": "template-api", "id.token.claim": "false", "access.token.claim": "true", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-oid",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-property-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "id", "claim.name": "oid", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        }
      ]
    },
    {
      "clientId": "template-cli",
      "name": "Template local CLI (password grant, local development only)",
      "enabled": true,
      "protocol": "openid-connect",
      "publicClient": true,
      "standardFlowEnabled": false,
      "directAccessGrantsEnabled": true,
      "protocolMappers": [
        {
          "name": "template-api-audience",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-audience-mapper",
          "consentRequired": false,
          "config": { "included.client.audience": "template-api", "id.token.claim": "false", "access.token.claim": "true", "introspection.token.claim": "true" }
        },
        {
          "name": "entra-oid",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-usermodel-property-mapper",
          "consentRequired": false,
          "config": { "user.attribute": "id", "claim.name": "oid", "jsonType.label": "String", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        }
      ]
    }
  ]
}
```

- [ ] **Step 5: Valider le JSON des quatre fichiers**

Run: `for f in src/Web.Template.CQRS.AppHost/Realms/*.json src/Web.Template.CQRS.AppHost/RealmVariants/*.json; do python -m json.tool "$f" > /dev/null && echo "valid $f"; done`
Expected: quatre lignes `valid ...`, aucune erreur.

- [ ] **Step 6: Créer les scripts d'obtention de token**

`scripts/get-token.ps1` :

```powershell
<#
.SYNOPSIS
    Prints an access token for a local Keycloak persona (local development only).
.EXAMPLE
    ./scripts/get-token.ps1 -Persona admin
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Persona,
    [string]$Password = 'Passw0rd!',
    [string]$KeycloakUrl = 'http://localhost:8080',
    [string]$Realm = 'template',
    [string]$ClientId = 'template-cli'
)

$ErrorActionPreference = 'Stop'
$username = if ($Persona.Contains('@')) { $Persona } else { "$Persona@local.test" }

try {
    $response = Invoke-RestMethod `
        -Method Post `
        -Uri "$KeycloakUrl/realms/$Realm/protocol/openid-connect/token" `
        -ContentType 'application/x-www-form-urlencoded' `
        -Body @{
            grant_type = 'password'
            client_id  = $ClientId
            username   = $username
            password   = $Password
            scope      = 'openid'
        }
}
catch {
    Write-Error "Unable to get a token for '$username' from $KeycloakUrl (realm '$Realm'). Is the Aspire AppHost running and the persona defined in the realm? $($_.Exception.Message)"
    exit 1
}

$response.access_token
```

`scripts/get-token.sh` :

```bash
#!/usr/bin/env bash
# Prints an access token for a local Keycloak persona (local development only).
# Usage: scripts/get-token.sh <persona> [password]
#   KEYCLOAK_URL (default http://localhost:8080), KEYCLOAK_REALM (default template), KEYCLOAK_CLIENT_ID (default template-cli)
set -euo pipefail

persona="${1:?Usage: scripts/get-token.sh <persona> [password]}"
password="${2:-Passw0rd!}"
keycloak_url="${KEYCLOAK_URL:-http://localhost:8080}"
realm="${KEYCLOAK_REALM:-template}"
client_id="${KEYCLOAK_CLIENT_ID:-template-cli}"

case "$persona" in
  *@*) username="$persona" ;;
  *) username="$persona@local.test" ;;
esac

if ! response="$(curl --silent --show-error --fail-with-body \
  --data-urlencode "grant_type=password" \
  --data-urlencode "client_id=$client_id" \
  --data-urlencode "username=$username" \
  --data-urlencode "password=$password" \
  --data-urlencode "scope=openid" \
  "$keycloak_url/realms/$realm/protocol/openid-connect/token")"; then
  echo "get-token: unable to get a token for '$username' from $keycloak_url (realm '$realm'): $response" >&2
  exit 1
fi

token="$(printf '%s' "$response" | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p')"
if [ -z "$token" ]; then
  echo "get-token: no access_token in Keycloak response: $response" >&2
  exit 1
fi
printf '%s\n' "$token"
```

Run: `git update-index --add --chmod=+x scripts/get-token.sh eng/verify-template.sh` après le `git add` de Step 8 (permet l'exécution directe sous Linux/macOS).

- [ ] **Step 7: Mettre à jour `Api.http`**

```http
@ApiBaseUrl = http://localhost:5257
@KeycloakUrl = http://localhost:8080
# Local Keycloak only: paste the output of `./scripts/get-token.ps1 -Persona admin` or `./scripts/get-token.sh admin`,
# or copy access_token from the token request below.
@AccessToken = paste-an-access-token-here

### Token for the admin persona (local Keycloak, password grant on the template-cli client)
POST {{KeycloakUrl}}/realms/template/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&client_id=template-cli&username=admin%40local.test&password=Passw0rd%21&scope=openid

### Token for the standard user persona
POST {{KeycloakUrl}}/realms/template/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&client_id=template-cli&username=user%40local.test&password=Passw0rd%21&scope=openid

###
GET {{ApiBaseUrl}}/openapi/v1.json
Accept: application/json

### Current user: identity, roles and groups carried by the token
GET {{ApiBaseUrl}}/me
Authorization: Bearer {{AccessToken}}
Accept: application/json

### Admin only (available when AuthorizationModel is not Authenticated): 200 for admin, 403 for user
GET {{ApiBaseUrl}}/admin/ping
Authorization: Bearer {{AccessToken}}
Accept: application/json
```

- [ ] **Step 8: Vérifier les générations Aspire de chaque modèle et commit**

Run: `bash eng/verify-template.sh AppRoles true true true`
Expected: OK (le realm `approles` est renommé en `Realms/template-realm.json`).

Run: `bash eng/verify-template.sh Groups true false true`
Expected: OK.

Run: `bash eng/verify-template.sh Authenticated true true true`
Expected: OK.

```bash
git add src/Web.Template.CQRS.AppHost src/Web.Template.CQRS.Api/Api.http scripts
git update-index --add --chmod=+x scripts/get-token.sh eng/verify-template.sh
git commit -m "feat(keycloak): mirror Entra token claims with personas, realm variants and token scripts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Vérification live avec Aspire et Keycloak (Docker)

Cette tâche ne crée pas de code, sauf si le repli du mapper de groupes est nécessaire. Elle prouve que les tokens Keycloak ont la forme Entra et que l'API les accepte.

**Files:**
- Modify (uniquement si repli nécessaire) : `src/Web.Template.CQRS.AppHost/Realms/template-realm.json`, `src/Web.Template.CQRS.AppHost/RealmVariants/template-realm.groups.json`, `src/Web.Template.CQRS.Api/appsettings.Development.json` (inchangé dans le repli), README (Task 7).

**Interfaces:**
- Consumes: template complet (Tasks 1-5), Docker, Aspire CLI.
- Produces: preuve (sorties de commandes) que `aud`, `oid`, `roles`, `groups` sont présents et que `/me`, `/admin/ping` répondent 200/403/401 comme attendu.

- [ ] **Step 1: Préparer un projet frais (volumes Keycloak vierges)**

```bash
VERIFY_DIR="$(mktemp -d)/Verify.Keycloak"
dotnet new install ./ --force
dotnet new templatewebcqrs --name Verify.Keycloak --output "$VERIFY_DIR" --AuthorizationModel AppRolesAndGroups
cd "$VERIFY_DIR"
dotnet user-secrets set "Parameters:sql-password" "Local-Verify-Sql-1!" --project src/Verify.Keycloak.AppHost/Verify.Keycloak.AppHost.csproj
dotnet user-secrets set "Parameters:keycloak-admin-password" "Local-Verify-Kc-1!" --project src/Verify.Keycloak.AppHost/Verify.Keycloak.AppHost.csproj
docker ps --filter "publish=8080" --format '{{.Names}}'
```

Expected: la dernière commande n'affiche rien. Si un conteneur occupe le port 8080 (Keycloak d'un autre projet), l'arrêter avec `docker stop <nom>` avant de continuer.

- [ ] **Step 2: Démarrer l'AppHost**

Run: `aspire --help` et repérer les commandes de démarrage détaché et d'arrêt (`start` / `stop` sur Aspire CLI 13.6).
Run: `aspire start --apphost src/Verify.Keycloak.AppHost/Verify.Keycloak.AppHost.csproj`
Puis attendre Keycloak (jusqu'à 5 minutes au premier téléchargement d'image) :

```bash
for attempt in $(seq 1 60); do
  curl -sf http://localhost:8080/realms/template/.well-known/openid-configuration > /dev/null && echo "keycloak ready" && break
  sleep 5
done
```

Expected: `keycloak ready`.

- [ ] **Step 3: Inspecter les tokens des personas**

```bash
decode() { python -c "import sys,json,base64; p=sys.argv[1].split('.')[1]; p+='='*(-len(p)%4); c=json.loads(base64.urlsafe_b64decode(p)); print(json.dumps({k:c.get(k) for k in ['aud','oid','sub','name','email','roles','groups']}, indent=2))" "$1"; }
ADMIN_TOKEN="$(bash scripts/get-token.sh admin)"
USER_TOKEN="$(bash scripts/get-token.sh user)"
decode "$ADMIN_TOKEN"
decode "$USER_TOKEN"
```

Expected pour admin : `aud` contient `template-api`, `oid` est un UUID, `roles` contient `Admin`, `groups` vaut `["6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"]`, `name` = `Alice Admin`, `email` = `admin@local.test`.
Expected pour user : `roles` contient `User`, `groups` est `null` ou absent.

Si `groups` est absent pour admin alors que `roles` est correct, appliquer le **repli** (Step 6) puis recommencer depuis Step 1 avec un nouveau dossier.

- [ ] **Step 4: Vérifier le comportement d'erreur du script**

Run: `bash scripts/get-token.sh admin wrong-password; echo "exit=$?"`
Expected: message `get-token: unable to get a token for 'admin@local.test' ...` sur stderr et `exit=1`.

- [ ] **Step 5: Appeler l'API avec les tokens**

```bash
for attempt in $(seq 1 60); do curl -sf http://localhost:5257/alive > /dev/null && echo "api ready" && break; sleep 5; done
curl -s -H "Authorization: Bearer $ADMIN_TOKEN" http://localhost:5257/me; echo
curl -s -o /dev/null -w "admin ping: %{http_code}\n" -H "Authorization: Bearer $ADMIN_TOKEN" http://localhost:5257/admin/ping
curl -s -o /dev/null -w "user ping: %{http_code}\n" -H "Authorization: Bearer $USER_TOKEN" http://localhost:5257/admin/ping
curl -s -o /dev/null -w "anonymous ping: %{http_code}\n" http://localhost:5257/admin/ping
```

Expected : `/me` renvoie `roles: ["Admin"]` (éventuellement avec des rôles par défaut Keycloak) et `groups: ["6f1c2a4e-..."]` ; `admin ping: 200`, `user ping: 403`, `anonymous ping: 401`.
Si l'API ne répond pas sur 5257, lire le port réel dans le dashboard Aspire (URL affichée par `aspire start`).

- [ ] **Step 6: Repli du mapper de groupes (uniquement si Step 3 l'exige)**

Dans `Realms/template-realm.json` et `RealmVariants/template-realm.groups.json` :
1. Remplacer le groupe par :

```json
    {
      "name": "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11",
      "path": "/6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11",
      "attributes": { "displayName": [ "Administrators" ] }
    }
```

2. Remplacer `"groups": [ "/Administrators" ]` de `admin@local.test` par `"groups": [ "/6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11" ]`.
3. Remplacer chaque mapper `entra-groups` par :

```json
        {
          "name": "entra-groups",
          "protocol": "openid-connect",
          "protocolMapper": "oidc-group-membership-mapper",
          "consentRequired": false,
          "config": { "claim.name": "groups", "full.path": "false", "id.token.claim": "false", "access.token.claim": "true", "userinfo.token.claim": "false", "introspection.token.claim": "true" }
        }
```

4. Mettre à jour la spec §4.4 (« repli appliqué »), valider le JSON (Task 5 Step 5), puis committer :

```bash
git add src/Web.Template.CQRS.AppHost docs/superpowers/specs
git commit -m "fix(keycloak): emit group object IDs through the group membership mapper

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 7: Arrêter et nettoyer**

```bash
aspire stop --apphost src/Verify.Keycloak.AppHost/Verify.Keycloak.AppHost.csproj
docker ps -a --format '{{.Names}}' | grep -i verify && docker ps -a --format '{{.Names}}' | grep -i verify | xargs docker rm -f
docker volume ls --format '{{.Name}}' | grep -i verify | xargs -r docker volume rm
cd - && rm -rf "$(dirname "$VERIFY_DIR")"
```

Expected: plus aucun conteneur ni volume `verify` (`docker ps -a | grep -i verify` vide). Noter dans le rapport de tâche les noms des volumes observés : ils servent à la documentation du README (Task 7).

---

### Task 7: CI du dépôt template, matrice complète et documentation

**Files:**
- Create: `.github/workflows/template-ci.yml`
- Modify: `README.md`, `src/Web.Template.CQRS.Api/README.md`

**Interfaces:**
- Consumes: `eng/verify-template.sh` (Task 2), variantes de realm (Task 5), noms de volumes observés (Task 6).
- Produces: workflow `template-ci` ; README à jour (options, modèles d'autorisation, personas, tokens, import du realm, tests, développement du template).

- [ ] **Step 1: Créer le workflow**

`.github/workflows/template-ci.yml` :

```yaml
name: template-ci

on:
  push:
    branches: [main]
  pull_request:

jobs:
  realm-json:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5
      - name: Validate Keycloak realm files
        run: |
          for file in src/Web.Template.CQRS.AppHost/Realms/*.json src/Web.Template.CQRS.AppHost/RealmVariants/*.json; do
            python3 -m json.tool "$file" > /dev/null
            echo "valid $file"
          done

  generate:
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        include:
          - { auth: Authenticated, aspire: true, rateLimiting: true, tests: true }
          - { auth: AppRoles, aspire: true, rateLimiting: true, tests: true }
          - { auth: Groups, aspire: true, rateLimiting: false, tests: true }
          - { auth: AppRolesAndGroups, aspire: true, rateLimiting: true, tests: true }
          - { auth: AppRoles, aspire: false, rateLimiting: true, tests: true }
          - { auth: AppRoles, aspire: true, rateLimiting: true, tests: false }
    name: generate (${{ matrix.auth }}, aspire=${{ matrix.aspire }}, rl=${{ matrix.rateLimiting }}, tests=${{ matrix.tests }})
    steps:
      - uses: actions/checkout@v5
      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json
      - name: Generate, build and test
        run: bash eng/verify-template.sh ${{ matrix.auth }} ${{ matrix.aspire }} ${{ matrix.rateLimiting }} ${{ matrix.tests }}

  template-repository:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5
      - uses: actions/setup-dotnet@v5
        with:
          global-json-file: global.json
      - name: Build and test the template sources
        run: dotnet test --solution Web.Template.CQRS.slnx
```

- [ ] **Step 2: Exécuter la matrice complète en local**

```bash
bash eng/verify-template.sh Authenticated true true true
bash eng/verify-template.sh AppRoles true true true
bash eng/verify-template.sh Groups true false true
bash eng/verify-template.sh AppRolesAndGroups true true true
bash eng/verify-template.sh AppRoles false true true
bash eng/verify-template.sh AppRoles true true false
dotnet test --solution Web.Template.CQRS.slnx
```

Expected: six lignes `verify-template: OK (...)` et tests du dépôt PASS.

- [ ] **Step 3: Réécrire `README.md`**

~~~~markdown
# Template Web CQRS

Template de solution ASP.NET Core CQRS sous .NET 10. Il comprend l'authentification bearer Entra ID, un modèle d'autorisation au choix (app roles, groupes de sécurité ou les deux), des erreurs Problem Details, la limitation de débit, des projets de tests et une orchestration locale Aspire avec OpenTelemetry et un Keycloak qui imite Entra.

## Installer le template

Depuis la racine du dépôt :

```powershell
dotnet new install ./
dotnet new templatewebcqrs --help
```

## Générer une API

```powershell
dotnet new templatewebcqrs --name Contoso.Orders
```

| Option | Valeurs | Défaut | Effet |
|---|---|---|---|
| `--AuthorizationModel` | `Authenticated`, `AppRoles`, `Groups`, `AppRolesAndGroups` | `AppRoles` | Policies générées et forme du realm Keycloak local |
| `--EnableAspire` | `true` / `false` | `true` | AppHost, ServiceDefaults, OpenTelemetry, SQL Server, Azurite et Keycloak locaux |
| `--EnableRateLimiting` | `true` / `false` | `true` | Politiques `read`, `write`, `generate`, `publish` |
| `--EnableTests` | `true` / `false` | `true` | Projets `tests/*.UnitTests` et `tests/*.IntegrationTests` |

Exemple : `dotnet new templatewebcqrs --name Contoso.Orders --AuthorizationModel AppRolesAndGroups --EnableRateLimiting false`.

## Modèles d'autorisation

Entra ID est toujours la cible. Le code de l'API lit les claims `oid`, `roles` et `groups` d'un access token Entra ; en local, Keycloak émet des tokens de même forme.

| Modèle | Côté Entra | Côté API |
|---|---|---|
| `Authenticated` | Aucun droit fin | Tout endpoint exige un utilisateur authentifié |
| `AppRoles` | App roles `Admin` et `User` déclarés dans l'App Registration, assignés aux utilisateurs | Policy `RequireAdmin` = rôle `Admin` |
| `Groups` | Claim de groupes activé (Token configuration → Add groups claim → Security groups, Group ID) | Policy `RequireAdmin` = membre du groupe `Authorization:Groups:Administrators` |
| `AppRolesAndGroups` | Les deux | `RequireAdmin` = rôle `Admin` **ou** membre du groupe |

Les object IDs de groupes ne sont jamais dans le code : configurez `Authorization:Groups:Administrators` avec l'object ID du groupe Entra (variable d'environnement `Authorization__Groups__Administrators`). L'application refuse de démarrer si la valeur n'est pas un GUID. Au-delà de 200 groupes par utilisateur, Entra n'émet plus le claim `groups` : ce cas n'est pas géré par le template.

L'endpoint `GET /me` renvoie l'identifiant, le nom, l'e-mail, les rôles et les groupes du token : c'est le premier outil de diagnostic. `GET /admin/ping` (hors modèle `Authenticated`) illustre un endpoint protégé par `RequireAdmin`.

## Lancer l'API sans Aspire

Cette section concerne une génération avec `--EnableAspire false`. Configurez Entra, les groupes éventuels ainsi que les chaînes de connexion SQL Server et Blob Storage dans User Secrets ou vos variables d'environnement, puis lancez l'API :

```powershell
dotnet run --project src/Contoso.Orders.Api/Contoso.Orders.Api.csproj
```

Pour construire l'image API depuis la racine du projet généré :

```powershell
docker build --file src/Contoso.Orders.Api/Dockerfile --tag contoso-orders .
```

## Lancer la pile locale Aspire

Installez l'Aspire CLI avec `dotnet tool install -g Aspire.Cli` si nécessaire et démarrez Docker Desktop. Depuis le dossier créé, ajoutez les deux paramètres secrets de l'AppHost :

```powershell
dotnet user-secrets set "Parameters:sql-password" "Choose-A-Strong-Local-Password!" --project src/Contoso.Orders.AppHost/Contoso.Orders.AppHost.csproj
dotnet user-secrets set "Parameters:keycloak-admin-password" "Choose-Another-Strong-Password!" --project src/Contoso.Orders.AppHost/Contoso.Orders.AppHost.csproj
aspire start --apphost src/Contoso.Orders.AppHost/Contoso.Orders.AppHost.csproj
```

Aspire démarre SQL Server, Azurite et Keycloak (port 8080), puis l'API. Le tableau de bord affiche les traces, métriques, journaux et l'état de santé des ressources. L'API expose `/health` et `/alive` en développement.

## Keycloak local : personas et tokens

Le realm `template` est importé au démarrage. Il imite Entra : claims `oid`, `roles` (plat) et `groups` (object IDs), audience `template-api`.

| Persona | Mot de passe | Rôles (`AppRoles*`) | Groupes (`*Groups`) |
|---|---|---|---|
| `admin@local.test` | `Passw0rd!` | `Admin` | `Administrators` (`6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11`) |
| `user@local.test` | `Passw0rd!` | `User` | aucun |

Ces comptes et mots de passe n'existent que dans le realm local ; ils ne doivent jamais être réutilisés ailleurs.

Obtenir un token sans navigateur (client `template-cli`, password grant, local uniquement) :

```powershell
./scripts/get-token.ps1 -Persona admin
```

```bash
./scripts/get-token.sh admin
```

Collez le token dans `src/Contoso.Orders.Api/Api.http` (`@AccessToken`) ou utilisez Scalar sur `/scalar` (client `template-scalar`, connexion interactive).

**Important : Keycloak n'importe le realm qu'au premier démarrage.** Le conteneur et son volume sont persistants : une modification de `src/Contoso.Orders.AppHost/Realms/template-realm.json` n'est appliquée qu'après suppression du volume. Arrêtez l'AppHost, puis :

```bash
docker volume ls --filter name=keycloak
docker rm -f <conteneur-keycloak>
docker volume rm <volume-keycloak-du-projet>
```

## Authentification Entra

L'API valide les access tokens Entra avec `Microsoft.Identity.Web`. Configurez les valeurs `Auth` par variables d'environnement, User Secrets ou un fournisseur de configuration externe ; ne stockez pas les identifiants de tenant dans le dépôt.

Les clés principales sont `Auth:Provider=Entra`, `Auth:Instance`, `Auth:TenantId` et `Auth:ClientId`. Pour activer la connexion interactive depuis Scalar en développement, ajoutez `Auth:OpenApiClientId` et `Auth:OpenApiScope`, et enregistrez l'URL de rappel Scalar dans l'application cliente Entra. Déclarez dans l'App Registration de l'API les app roles dont les valeurs correspondent aux constantes de `Api/Authorization/AppRoles.cs`.

## Tests

```powershell
dotnet test --solution Contoso.Orders.slnx
```

Les tests d'intégration démarrent l'API en mémoire avec un schéma d'authentification de test : les en-têtes `X-Test-User`, `X-Test-Roles` et `X-Test-Groups` construisent l'utilisateur, sans Keycloak ni Entra. SQL Server est remplacé par EF Core InMemory et le stockage blob par un faux.

## Options intégrées

- **Aspire** : AppHost, ServiceDefaults, traces et métriques OpenTelemetry, logs structurés, health checks, découverte de services et résilience HTTP.
- **Services locaux Aspire** : SQL Server, Azurite et Keycloak avec import d'un realm qui imite Entra.
- **Rate limiting** : politiques nommées `read`, `write`, `generate` et `publish`, partitionnées par identifiant d'utilisateur ou adresse IP.
- **Gestion d'erreurs** : réponses Problem Details, validation en 400, erreurs inattendues en 500 avec `traceId`.
- **Identité** : Entra en configuration standard, Keycloak pour le lancement local avec Aspire.

## Développer le template

- `Directory.Build.props` (exclu de la génération) définit toutes les constantes de template : le dépôt compile et teste la variante la plus complète (`AppRolesAndGroups`, Aspire, rate limiting, tests).
- `eng/verify-template.sh <AuthorizationModel> <EnableAspire> <EnableRateLimiting> <EnableTests>` génère une solution, vérifie les exclusions, la compile et lance ses tests.
- `.github/workflows/template-ci.yml` exécute une matrice de six combinaisons et valide les fichiers de realm.
- `src/Web.Template.CQRS.AppHost/Realms/template-realm.json` est la variante complète ; `RealmVariants/` contient les autres variantes, renommées à la génération.
~~~~

Adapter la commande `docker volume ls` et l'exemple de nom de volume selon les noms relevés en Task 6 Step 7.

- [ ] **Step 4: Mettre à jour `src/Web.Template.CQRS.Api/README.md`**

```markdown
# API

L'API valide les access tokens bearer émis par Entra. Lorsque la variante Aspire est activée, le profil de développement utilise le realm Keycloak local démarré par l'AppHost, qui émet des tokens de même forme qu'Entra. Sans Aspire, le profil de développement utilise la configuration Entra de `appsettings.json`.

Configurez `Auth:Provider`, puis les paramètres correspondants :

- Entra : `Auth:Instance`, `Auth:TenantId`, `Auth:ClientId` et, si besoin, `Auth:Audience`.
- Keycloak : `Auth:Authority` et `Auth:Audience`.

Selon le modèle d'autorisation généré :

- `Authorization/AppRoles.cs` liste les valeurs des app roles Entra ;
- `Authorization:Groups:<Nom>` contient l'object ID de chaque groupe de sécurité Entra (validé au démarrage) ;
- `Authorization/Policies.cs` et `Authorization/AuthorizationSetup.cs` définissent les policies nommées, appliquées aux endpoints avec `RequireAuthorization(Policies.RequireAdmin)`.

En local, Aspire injecte la configuration Keycloak et exporte automatiquement OpenTelemetry vers son tableau de bord. Scalar permet de s'authentifier avec le client public `template-scalar` ; `scripts/get-token` fournit un token pour un persona via le client `template-cli`.

L'endpoint `GET /me` retourne l'identité, les rôles et les groupes du token. Les exceptions et erreurs de validation sont converties en réponses Problem Details sans exposer le détail des erreurs internes.
```

- [ ] **Step 5: Commit**

```bash
git add .github/workflows/template-ci.yml README.md src/Web.Template.CQRS.Api/README.md
git commit -m "ci: verify template generations in a matrix and document authorization models

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
