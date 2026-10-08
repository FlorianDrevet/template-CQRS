namespace Web.Template.CQRS.Api.Common.RateLimiting;

public static class RateLimitingPolicies
{
    public const string Read = "read";
    public const string Write = "write";
    public const string Generate = "generate";
    public const string Publish = "publish";
}
