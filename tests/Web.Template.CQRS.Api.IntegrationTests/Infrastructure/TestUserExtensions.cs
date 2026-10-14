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
