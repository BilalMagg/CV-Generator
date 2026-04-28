using Microsoft.EntityFrameworkCore;
using backend.src.features.auth.interfaces;
using backend.src.features.user.entity;

namespace backend.src.features.auth.repository;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _context;

    public AuthRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByKeycloakIdAsync(string keycloakId)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }
}
