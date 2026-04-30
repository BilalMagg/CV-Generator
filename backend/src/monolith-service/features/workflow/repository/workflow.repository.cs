using Microsoft.EntityFrameworkCore;
using backend.src.features.workflow.entity;
using backend.src.features.workflow.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.workflow.repository;

public class WorkflowRepository : IWorkflowRepository
{
    private readonly AppDbContext _context;

    public WorkflowRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Workflow>> GetAll()
    {
        return await _context.Set<Workflow>().ToListAsync();
    }

    public async Task<Workflow> GetById(Guid id)
    {
        var entity = await _context.Set<Workflow>().FindAsync(id);
        
        if (entity == null)
            throw new NotFoundException($"Workflow with ID {id} not found");
            
        return entity;
    }

    public async Task<Workflow> Create(Workflow entity)
    {
        _context.Set<Workflow>().Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<Workflow> Update(Workflow entity)
    {
        _context.Set<Workflow>().Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task Delete(Guid id)
    {
        var entity = await GetById(id);

        _context.Set<Workflow>().Remove(entity);
        await _context.SaveChangesAsync();
    }
}
