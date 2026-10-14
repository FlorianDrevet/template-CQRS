using Mediator;
using Web.Template.CQRS.Application.Admin.Common;

namespace Web.Template.CQRS.Application.Admin.Queries.PingAdmin;

public sealed record PingAdminQuery : IQuery<PingAdminResult>;
