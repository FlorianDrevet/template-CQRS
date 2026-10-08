using Mediator;
using Web.Template.CQRS.Application.Users.Common;

namespace Web.Template.CQRS.Application.Users.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery : IQuery<CurrentUserResult>;
