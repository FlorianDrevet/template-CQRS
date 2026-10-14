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
