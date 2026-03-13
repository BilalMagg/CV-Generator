namespace backend.src.features.user.repository;

using backend.src.features.user.entity;
using backend.src.features.user.interfaces;
using Microsoft.EntityFrameworkCore;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<User>> GetAll()
    {
        return await _context.Set<User>().ToListAsync();
    }
}
