using Microsoft.EntityFrameworkCore;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserEntity>> GetAll()
    {
        return await _context.Set<UserEntity>().ToListAsync();
    }
}
