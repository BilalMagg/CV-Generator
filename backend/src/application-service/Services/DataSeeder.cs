using Bogus;
using Microsoft.EntityFrameworkCore;
using ApplicationService.Entities;
using ApplicationService.DTOs;

namespace ApplicationService.Services;

public interface IDataSeeder
{
    Task<SeedResultDto> GenerateApplicationsAsync(Guid candidateId, int count, int monthsBack);
}

public class DataSeeder : IDataSeeder
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DataSeeder> _logger;

    private static readonly string[] Sources = ["LinkedIn", "Indeed", "Company Website", "Referral", "Glassdoor", "Stack Overflow Jobs", "AngelList", "Other"];

    private static readonly double[] SourceWeights = [0.35, 0.20, 0.15, 0.12, 0.07, 0.05, 0.03, 0.03];

    private static readonly ApplicationStatus[] AllStatuses =
        [ApplicationStatus.PENDING, ApplicationStatus.REVIEWED, ApplicationStatus.INTERVIEW, ApplicationStatus.ACCEPTED, ApplicationStatus.REJECTED];

    public DataSeeder(ApplicationDbContext db, ILogger<DataSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SeedResultDto> GenerateApplicationsAsync(Guid candidateId, int count, int monthsBack)
    {
        var historyCount = 0;
        var batchSize = 100;
        var now = DateTime.UtcNow;

        var companyFaker = new Faker().Company;
        var jobFaker = new Faker().Name;
        var lorem = new Faker().Lorem;

        var applications = new List<Application>(batchSize);

        for (var i = 0; i < count; i++)
        {
            var (appliedAt, daysOffset) = RandomDate(now, monthsBack);

            var app = new Application
            {
                CandidateId = candidateId,
                CompanyName = companyFaker.CompanyName(),
                PositionTitle = jobFaker.JobTitle(),
                OfferSource = PickSource(),
                Notes = i % 3 == 0 ? lorem.Sentence() : null,
                AppliedAt = appliedAt,
                UpdatedAt = appliedAt,
            };

            var (finalStatus, statusChanges) = GenerateStatusProgression(appliedAt, now);
            app.Status = finalStatus;

            foreach (var (status, changedAt, comment) in statusChanges)
            {
                app.StatusHistory.Add(new ApplicationStatusHistory
                {
                    ApplicationId = app.Id,
                    NewStatus = status,
                    ChangedAt = changedAt,
                    ChangedBy = candidateId.ToString(),
                    Comment = comment,
                });
                historyCount++;
            }

            applications.Add(app);

            if (applications.Count >= batchSize)
            {
                await _db.Applications.AddRangeAsync(applications);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Seeded {Count} applications...", i + 1);
                applications.Clear();
            }
        }

        if (applications.Count > 0)
        {
            await _db.Applications.AddRangeAsync(applications);
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation("Seed complete: {Count} applications, {HistoryCount} status history entries", count, historyCount);

        return new SeedResultDto(count, historyCount, candidateId);
    }

    private static (DateTime date, int daysOffset) RandomDate(DateTime now, int monthsBack)
    {
        var end = now;
        var start = end.AddMonths(-monthsBack);
        var faker = new Faker();
        var days = (int)(end - start).TotalDays;
        var offset = faker.Random.Int(0, Math.Max(0, days));
        var date = start.AddDays(offset).AddHours(faker.Random.Int(8, 18)).AddMinutes(faker.Random.Int(0, 59));
        return (date, offset);
    }

    private static string PickSource()
    {
        var roll = new Faker().Random.Double();
        var cumulative = 0.0;
        for (var i = 0; i < Sources.Length; i++)
        {
            cumulative += SourceWeights[i];
            if (roll < cumulative) return Sources[i];
        }
        return Sources[^1];
    }

    private static (ApplicationStatus finalStatus, List<(ApplicationStatus status, DateTime changedAt, string? comment)> changes)
        GenerateStatusProgression(DateTime appliedAt, DateTime now)
    {
        var faker = new Faker();
        var path = faker.Random.Int(0, 100);

        if (path < 40)
        {
            return (ApplicationStatus.PENDING, []);
        }

        var changes = new List<(ApplicationStatus, DateTime, string?)>();

        if (path < 50)
        {
            var reviewedAt = appliedAt.AddDays(faker.Random.Int(1, 7));
            if (reviewedAt > now) return (ApplicationStatus.PENDING, []);
            changes.Add((ApplicationStatus.REJECTED, reviewedAt, PickRejectionReason()));
            return (ApplicationStatus.REJECTED, changes);
        }

        if (path < 70)
        {
            var reviewedAt = appliedAt.AddDays(faker.Random.Int(1, 7));
            if (reviewedAt > now) return (ApplicationStatus.PENDING, []);
            changes.Add((ApplicationStatus.REVIEWED, reviewedAt, null));
            return (ApplicationStatus.REVIEWED, changes);
        }

        if (path < 80)
        {
            var reviewedAt = appliedAt.AddDays(faker.Random.Int(1, 7));
            if (reviewedAt > now) return (ApplicationStatus.PENDING, []);
            changes.Add((ApplicationStatus.REVIEWED, reviewedAt, null));

            var interviewAt = reviewedAt.AddDays(faker.Random.Int(3, 14));
            if (interviewAt > now) return (ApplicationStatus.REVIEWED, changes);
            changes.Add((ApplicationStatus.INTERVIEW, interviewAt, null));
            return (ApplicationStatus.INTERVIEW, changes);
        }

        if (path < 88)
        {
            var reviewedAt = appliedAt.AddDays(faker.Random.Int(1, 7));
            if (reviewedAt > now) return (ApplicationStatus.PENDING, []);
            changes.Add((ApplicationStatus.REVIEWED, reviewedAt, null));

            var interviewAt = reviewedAt.AddDays(faker.Random.Int(3, 14));
            if (interviewAt > now) return (ApplicationStatus.REVIEWED, changes);
            changes.Add((ApplicationStatus.INTERVIEW, interviewAt, null));

            var acceptedAt = interviewAt.AddDays(faker.Random.Int(3, 21));
            if (acceptedAt > now) return (ApplicationStatus.INTERVIEW, changes);
            changes.Add((ApplicationStatus.ACCEPTED, acceptedAt, "Offer accepted"));
            return (ApplicationStatus.ACCEPTED, changes);
        }

        if (path < 95)
        {
            var reviewedAt = appliedAt.AddDays(faker.Random.Int(1, 7));
            if (reviewedAt > now) return (ApplicationStatus.PENDING, []);
            changes.Add((ApplicationStatus.REVIEWED, reviewedAt, null));

            var interviewAt = reviewedAt.AddDays(faker.Random.Int(3, 14));
            if (interviewAt > now) return (ApplicationStatus.REVIEWED, changes);
            changes.Add((ApplicationStatus.INTERVIEW, interviewAt, null));

            var rejectedAt = interviewAt.AddDays(faker.Random.Int(2, 14));
            if (rejectedAt > now) return (ApplicationStatus.INTERVIEW, changes);
            changes.Add((ApplicationStatus.REJECTED, rejectedAt, PickRejectionReason()));
            return (ApplicationStatus.REJECTED, changes);
        }

        {
            var reviewedAt = appliedAt.AddDays(faker.Random.Int(1, 7));
            if (reviewedAt > now) return (ApplicationStatus.PENDING, []);
            changes.Add((ApplicationStatus.REVIEWED, reviewedAt, null));

            var rejectedAt = reviewedAt.AddDays(faker.Random.Int(3, 10));
            if (rejectedAt > now) return (ApplicationStatus.REVIEWED, changes);
            changes.Add((ApplicationStatus.REJECTED, rejectedAt, PickRejectionReason()));
            return (ApplicationStatus.REJECTED, changes);
        }
    }

    private static string PickRejectionReason()
    {
        var reasons = new[]
        {
            "Position filled internally",
            "Experience mismatch",
            "Skills not aligned",
            "Overqualified",
            "Budget constraints",
            "Hiring freeze",
            "Culture fit concerns",
            "Found candidate with more relevant experience",
        };
        return new Faker().PickRandom(reasons);
    }
}
