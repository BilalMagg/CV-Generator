using Microsoft.EntityFrameworkCore;

public class SkillRepository : ISkillRepository
{
    private readonly AppDbContext _context;

    public SkillRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<SkillEntity>> GetAll()
    {
        return await _context.Set<SkillEntity>().ToListAsync();
    }
}
