using Microsoft.EntityFrameworkCore;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;

    public ProjectRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProjectEntity>> GetAll()
    {
        return await _context.Set<ProjectEntity>().ToListAsync();
    }
}
