namespace Web.Template.CQRS.Api.Authentication;

public static class ScalarOAuth2
{
    public const string SecuritySchemeName = "OAuth2";

    private static readonly string[] KeycloakScopes = ["openid", "profile", "email"];

    public static string ClientId(AuthOptions options) =>
        options.Provider == AuthProvider.Keycloak
            ? AuthOptions.LocalKeycloakScalarClientId
            : options.OpenApiClientId ?? string.Empty;

    public static string AuthorizationUrl(AuthOptions options) =>
        options.Provider == AuthProvider.Keycloak
            ? $"{options.Authority!.TrimEnd('/')}/protocol/openid-connect/auth"
            : $"{EntraAuthority(options)}/oauth2/v2.0/authorize";

    public static string TokenUrl(AuthOptions options) =>
        options.Provider == AuthProvider.Keycloak
            ? $"{options.Authority!.TrimEnd('/')}/protocol/openid-connect/token"
            : $"{EntraAuthority(options)}/oauth2/v2.0/token";

    public static string[] Scopes(AuthOptions options) =>
        options.Provider == AuthProvider.Keycloak
            ? KeycloakScopes
            : string.IsNullOrWhiteSpace(options.OpenApiScope) ? [] : [options.OpenApiScope];

    public static Dictionary<string, string> ScopeDescriptions(AuthOptions options) =>
        Scopes(options).ToDictionary(scope => scope, scope => scope, StringComparer.Ordinal);

    private static string EntraAuthority(AuthOptions options) =>
        $"{options.Instance!.TrimEnd('/')}/{options.TenantId}";
}
