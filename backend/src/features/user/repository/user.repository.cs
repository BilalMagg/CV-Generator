using Microsoft.EntityFrameworkCore;
using backend.src.features.user.entity;
using backend.src.features.user.interfaces;
using backend.src.shared.exceptions;

namespace backend.src.features.user.repository;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<User>> GetAll()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<User> GetById(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        
        if (user == null)
            throw new NotFoundException($"User with ID {id} not found");
            
        return user;
    }

    public async Task<User?> GetByEmail(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User> Create(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> Update(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task Delete(Guid id)
    {
        var user = await GetById(id);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }
}