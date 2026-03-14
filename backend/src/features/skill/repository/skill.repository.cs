using Microsoft.EntityFrameworkCore;
using backend.src.features.skill.entity;
using backend.src.features.skill.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.skill.repository;

public class SkillRepository : ISkillRepository
{
    private readonly AppDbContext _context;

    public SkillRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Skill>> GetAll()
    {
        return await _context.Set<Skill>().ToListAsync();
    }

    public async Task<Skill> GetById(Guid id)
    {
        var entity = await _context.Set<Skill>().FindAsync(id);
        
        if (entity == null)
            throw new NotFoundException($"Skill with ID {id} not found");
            
        return entity;
    }

    public async Task<Skill> Create(Skill entity)
    {
        _context.Set<Skill>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Skill> Update(Skill entity)
    {
        _context.Set<Skill>().Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task Delete(Guid id)
    {
        var entity = await GetById(id);

        _context.Set<Skill>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
