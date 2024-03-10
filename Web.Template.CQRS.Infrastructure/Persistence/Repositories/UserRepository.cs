using Web.Template.CQRS.Application.Common.Interfaces.Persistence;
using Web.Template.CQRS.Domain.UserAggregate;
using Web.Template.CQRS.Domain.UserAggregate.ValueObjects;

namespace Web.Template.CQRS.Infrastructure.Persistence.Repositories;

public class UserRepository(ProjectDbContext vpdDbContext) : IUserRepository
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