namespace Web.Template.CQRS.Api.Authentication;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public const string LocalKeycloakScalarClientId = "template-scalar";

    public AuthProvider Provider { get; set; } = AuthProvider.Entra;

    public string? Authority { get; set; }

    public string? Audience { get; set; }

    public string? Instance { get; set; }

    public string? TenantId { get; set; }

    public string? ClientId { get; set; }

    public string? OpenApiClientId { get; set; }

    public string? OpenApiScope { get; set; }
}
