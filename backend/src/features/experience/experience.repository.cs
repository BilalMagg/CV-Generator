using Microsoft.EntityFrameworkCore;

public class ExperienceRepository : IExperienceRepository
{
    private readonly AppDbContext _context;

    public ExperienceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ExperienceEntity>> GetAll()
    {
        return await _context.Set<ExperienceEntity>().ToListAsync();
    }
}
