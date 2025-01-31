using Microsoft.EntityFrameworkCore;
using Web.Template.CQRS.Application.Common.Interfaces.Persistence;
using Web.Template.CQRS.Domain.UserAggregate;
using Web.Template.CQRS.Domain.UserAggregate.ValueObjects;

namespace Web.Template.CQRS.Infrastructure.Persistence.Repositories;

public class UserRepository: BaseRepository<User, ProjectDbContext>,IUserRepository
{
    public UserRepository(ProjectDbContext context) : base(context)
    {
    }
    
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await Context.Users
            .FirstOrDefaultAsync(user => user.Email == email);
    }
}