using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var sqlPassword = builder.AddParameter("sql-password", secret: true);
var sqlServer = builder
    .AddSqlServer("sqlserver", password: sqlPassword)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);
var projectDatabase = sqlServer.AddDatabase("projectdb");

var blobs = builder
    .AddAzureStorage("storage")
    .RunAsEmulator()
    .AddBlobs("blobs");

var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var keycloak = builder
    .AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
    .WithRealmImport("./Realms")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder
    .AddProject("api", "../Web.Template.CQRS.Api/Web.Template.CQRS.Api.csproj")
    .WithReference(projectDatabase)
    .WithReference(blobs)
    .WithReference(keycloak)
    .WaitFor(sqlServer)
    .WaitFor(blobs)
    .WaitFor(keycloak)
    .WithEnvironment("Auth__Provider", "Keycloak")
    .WithEnvironment("Auth__Authority", "http://localhost:8080/realms/template")
    .WithEnvironment("Auth__Audience", "template-api")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/alive");

builder.Build().Run();
