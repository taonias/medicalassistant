using MedicalAssistant.Domain.Common;

namespace MedicalAssistant.Application.Contracts.Persistence;

public interface IGenericRepository<T> where T : BaseEntity
{
    Task<IReadOnlyList<T>> GetAsync();
    Task<IReadOnlyList<T>> GetByUserAsync();
    Task<T> GetByIdAsync(int id);
    Task<T> GetByUserByIdAsync(int id);
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task<T> DeleteAsync(T entity);
}
