namespace backend.src.features.auth.repository;

using Microsoft.EntityFrameworkCore;
using backend.src.features.auth.interfaces;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext _context;

    public AuthRepository(AppDbContext context)
    {
        _context = context;
    }

}
