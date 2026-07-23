using MedicalAssistant.Application.Contracts.Persistence;
using MedicalAssistant.Application.Exceptions;
using MedicalAssistant.Domain.Common;
using MedicalAssistant.Persistence.DatabaseContext;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MedicalAssistant.Persistence.Repositories;

public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly MedicalAssistantDatabaseContext _context;
    protected readonly IHttpContextAccessor _httpContextAccessor;

    public GenericRepository(MedicalAssistantDatabaseContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<T> CreateAsync(T entity)
    {
        await _context.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<T> DeleteAsync(T entity)
    {
        _context.Remove(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<IReadOnlyList<T>> GetAsync()
    {
        return await _context.Set<T>().AsNoTracking().ToListAsync();
    }

    public async Task<IReadOnlyList<T>> GetByUserAsync()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst("uid")?.Value ?? "Unknown";
        return await _context.Set<T>()
            .AsNoTracking()
            .Where(e => e.CreatedBy == userId)
            .ToListAsync();
    }

    public virtual async Task<T> GetByIdAsync(int id)
    {
        return await _context.Set<T>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException(nameof(T), id);
    }

    public async Task<T> GetByUserByIdAsync(int id)
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst("uid")?.Value ?? "Unknown";
        return await _context.Set<T>()
            .AsNoTracking()
            .Where(x => x.Id == id && x.CreatedBy == userId)
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException(nameof(T), id);
    }

    public async Task<T> UpdateAsync(T entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return entity;
    }
}
