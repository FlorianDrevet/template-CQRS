using System.Linq.Expressions;
using Web.Template.CQRS.Domain.Common.Models;

namespace Web.Template.CQRS.Application.Common.Interfaces.Persistence;

public interface IRepository<T>
{
    Task<T?> GetByIdAsync(ValueObject id);
    Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes);
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<bool> DeleteAsync(ValueObject id);
}