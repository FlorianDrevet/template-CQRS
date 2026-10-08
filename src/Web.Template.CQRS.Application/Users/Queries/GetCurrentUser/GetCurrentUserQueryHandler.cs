using Mediator;
using Web.Template.CQRS.Application.Common.Interfaces.Authentication;
using Web.Template.CQRS.Application.Users.Common;

namespace Web.Template.CQRS.Application.Users.Queries.GetCurrentUser;

public sealed class GetCurrentUserQueryHandler(ICurrentUser currentUser)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserResult>
{
    public ValueTask<CurrentUserResult> Handle(
        GetCurrentUserQuery query,
        CancellationToken cancellationToken)
    {
        var result = new CurrentUserResult(
            currentUser.Id,
            currentUser.DisplayName,
            currentUser.Email);

        return ValueTask.FromResult(result);
    }
}
