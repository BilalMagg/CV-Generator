using Microsoft.EntityFrameworkCore;
using backend.src.features.experience.entity;
using backend.src.features.experience.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.experience.repository;

public class ExperienceRepository : IExperienceRepository
{
    private readonly AppDbContext _context;

    public ExperienceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Experience>> GetAll()
    {
        return await _context.Set<Experience>().ToListAsync();
    }

    public async Task<Experience> GetById(Guid id)
    {
        var entity = await _context.Set<Experience>().FindAsync(id);
        
        if (entity == null)
            throw new NotFoundException($"Experience with ID {id} not found");
            
        return entity;
    }

    public async Task<Experience> Create(Experience entity)
    {
        _context.Set<Experience>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Experience> Update(Experience entity)
    {
        _context.Set<Experience>().Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task Delete(Guid id)
    {
        var entity = await GetById(id);

        _context.Set<Experience>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
