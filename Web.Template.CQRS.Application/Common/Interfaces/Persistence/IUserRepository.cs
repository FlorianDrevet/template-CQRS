using Web.Template.CQRS.Domain.UserAggregate;
using Web.Template.CQRS.Domain.UserAggregate.ValueObjects;

namespace Web.Template.CQRS.Application.Common.Interfaces.Persistence;

public interface IUserRepository
{
    User? GetUserByEmail(string email);
    void AddUser(User user);
    User? GetUserById(UserId requestUserId);
    void UpdateUser(User user);
    List<User> GetAllUsers();
}