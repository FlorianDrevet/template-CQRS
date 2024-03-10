using VPD.Domain.UserAggregate;
using VPD.Domain.UserAggregate.ValueObjects;

namespace VPD.Application.Common.Interfaces.Persistence;

public interface IUserRepository
{
    User? GetUserByEmail(string email);
    void AddUser(User user);
    User? GetUserById(UserId requestUserId);
    void UpdateUser(User user);
    List<User> GetAllUsers();
}