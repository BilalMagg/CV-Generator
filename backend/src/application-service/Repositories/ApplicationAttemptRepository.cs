using Microsoft.EntityFrameworkCore;
using ApplicationService.Entities;

namespace ApplicationService.Repositories;

public interface IApplicationAttemptRepository
{
    Task<List<ApplicationAttempt>> GetByApplicationAsync(Guid applicationId);
    Task<ApplicationAttempt?> GetAsync(Guid applicationId, Guid attemptId);
    Task<int> GetNextAttemptNumberAsync(Guid applicationId);
    Task<bool> HasSentAttemptAsync(Guid applicationId);
    Task<ApplicationAttempt> CreateAsync(ApplicationAttempt attempt);
    Task<ApplicationAttempt> UpdateAsync(ApplicationAttempt attempt);
}

public class ApplicationAttemptRepository : IApplicationAttemptRepository
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ApplicationAttemptRepository> _logger;

    public ApplicationAttemptRepository(ApplicationDbContext db, ILogger<ApplicationAttemptRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ApplicationAttempt>> GetByApplicationAsync(Guid applicationId)
        => await _db.ApplicationAttempts
            .AsNoTracking()
            .Where(t => t.ApplicationId == applicationId)
            .OrderBy(t => t.AttemptNumber)
            .ToListAsync();

    public async Task<ApplicationAttempt?> GetAsync(Guid applicationId, Guid attemptId)
        => await _db.ApplicationAttempts
            .FirstOrDefaultAsync(t => t.ApplicationId == applicationId && t.Id == attemptId);

    public async Task<int> GetNextAttemptNumberAsync(Guid applicationId)
    {
        var max = await _db.ApplicationAttempts
            .Where(t => t.ApplicationId == applicationId)
            .MaxAsync(t => (int?)t.AttemptNumber);
        return (max ?? 0) + 1;
    }

    public async Task<bool> HasSentAttemptAsync(Guid applicationId)
        => await _db.ApplicationAttempts
            .AnyAsync(t => t.ApplicationId == applicationId && t.Status == AttemptStatus.SENT);

    public async Task<ApplicationAttempt> CreateAsync(ApplicationAttempt attempt)
    {
        _db.ApplicationAttempts.Add(attempt);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Created attempt #{Number} ({Channel}) for application {ApplicationId}",
            attempt.AttemptNumber, attempt.Channel, attempt.ApplicationId);
        return attempt;
    }

    public async Task<ApplicationAttempt> UpdateAsync(ApplicationAttempt attempt)
    {
        attempt.UpdatedAt = DateTime.UtcNow;
        _db.ApplicationAttempts.Update(attempt);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Updated attempt {AttemptId} of application {ApplicationId}",
            attempt.Id, attempt.ApplicationId);
        return attempt;
    }
}
