namespace Web.Template.CQRS.Api.Authorization;

public sealed class GroupOptions
{
    public const string SectionName = "Authorization:Groups";

    public string Administrators { get; set; } = string.Empty;
}
