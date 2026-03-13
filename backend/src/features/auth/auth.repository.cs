using Microsoft.EntityFrameworkCore;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _context;

    public AuthRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AuthEntity>> GetAll()
    {
        return await _context.Set<AuthEntity>().ToListAsync();
    }
}
