using Web.Template.CQRS.Domain.UserAggregate;
using Web.Template.CQRS.Domain.UserAggregate.ValueObjects;

namespace Web.Template.CQRS.Application.Common.Interfaces.Persistence;

public interface IUserRepository: IRepository<User>
{
    Task<User?> GetUserByEmailAsync(string email);
}