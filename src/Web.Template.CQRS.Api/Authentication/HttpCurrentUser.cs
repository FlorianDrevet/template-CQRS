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
