using VPD.Application.Common.Interfaces.Persistence;
using VPD.Domain.UserAggregate;
using VPD.Domain.UserAggregate.ValueObjects;

namespace VPD.Infrastructure.Persistence.Repositories;

public class UserRepository(VPDDbContext vpdDbContext) : IUserRepository
{
    public User? GetUserByEmail(string email)
    {
        return vpdDbContext.Users
            .FirstOrDefault(user => user.Email == email);
    }

    public void AddUser(User user)
    {
        vpdDbContext.Add(user);
        vpdDbContext.SaveChanges();
    }

    public User? GetUserById(UserId requestUserId)
    {
        return vpdDbContext.Users
            .FirstOrDefault(user => user.Id == requestUserId);
    }

    public void UpdateUser(User user)
    {
        vpdDbContext.Update(user);
        vpdDbContext.SaveChanges();
    }

    public List<User> GetAllUsers()
    {
        return vpdDbContext.Users.ToList();
    }
}