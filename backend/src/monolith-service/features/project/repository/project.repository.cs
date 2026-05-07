using Microsoft.EntityFrameworkCore;
using backend.src.features.project.entity;
using backend.src.features.project.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.project.repository;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Project>> GetAll()
    {
        return await _context.Set<Project>().ToListAsync();
    }

    public async Task<Project> GetById(Guid id)
    {
        var entity = await _context.Set<Project>().FindAsync(id);
        
        if (entity == null)
            throw new NotFoundException($"Project with ID {id} not found");
            
        return entity;
    }

    public async Task<Project> Create(Project entity)
    {
        _context.Set<Project>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Project> Update(Project entity)
    {
        _context.Set<Project>().Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task Delete(Guid id)
    {
        var entity = await GetById(id);

        _context.Set<Project>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
