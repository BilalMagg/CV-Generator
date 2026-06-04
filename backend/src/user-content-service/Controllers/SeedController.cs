using Bogus;
using CVGenerator.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserContentService.Entity;

namespace UserContentService.Controllers;


[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private static readonly string[] TechSkills =
    [
        "C#", "Python", "JavaScript", "TypeScript", "Java", "Go", "Rust",
        "React", "Angular", "Vue.js", "Node.js", "ASP.NET Core", "Django", "Spring Boot",
        "PostgreSQL", "MongoDB", "Redis", "Elasticsearch",
        "Docker", "Kubernetes", "AWS", "Azure", "GCP", "Terraform",
        "CI/CD", "Git", "REST API", "GraphQL",
        "Microservices", "Event-Driven Architecture", "RabbitMQ", "Kafka",
        "SQL", "Entity Framework", "Dapper", "SignalR", "gRPC",
        "Unit Testing", "Integration Testing", "DDD", "CQRS", "OAuth", "JWT"
    ];

    private static readonly Dictionary<string, string[]> SkillCategories = new()
    {
        ["Frontend"] = ["React", "Angular", "Vue.js", "JavaScript", "TypeScript", "SignalR"],
        ["Backend"] = ["C#", "Python", "Java", "Go", "Rust", "Node.js", "ASP.NET Core", "Django", "Spring Boot", "REST API", "GraphQL", "gRPC"],
        ["DevOps"] = ["Docker", "Kubernetes", "AWS", "Azure", "GCP", "Terraform", "CI/CD"],
        ["Data"] = ["PostgreSQL", "MongoDB", "Redis", "Elasticsearch", "Kafka", "SQL"],
        ["Mobile"] = [],
        ["Soft Skill"] = ["Git", "OAuth", "JWT", "Unit Testing", "Integration Testing", "DDD", "CQRS", "Microservices", "Event-Driven Architecture"],
    };

    private static readonly string[] JobTitles =
    [
        "Senior Backend Engineer", "Full Stack Developer", "Frontend Developer",
        "DevOps Engineer", "Software Architect", "Lead Developer",
        "Cloud Engineer", "Data Engineer", "Platform Engineer",
        "Staff Software Engineer", "Principal Engineer"
    ];

    private static readonly string[] Companies =
    [
        "Microsoft", "Google", "Amazon", "Meta", "Apple", "Netflix",
        "Spotify", "Stripe", "Shopify", "GitHub", "Atlassian", "Twilio",
        "Uber", "Airbnb", "Slack", "Datadog", "Cloudflare", "Palantir"
    ];

    private static readonly string[] ExperienceDescriptions =
    [
        "Led a team of 5 engineers to redesign the core payment processing system, reducing latency by 40%",
        "Architected and implemented a microservices-based platform handling 10M+ daily requests",
        "Built CI/CD pipelines using GitHub Actions and ArgoCD, reducing deployment time from 2 hours to 15 minutes",
        "Migrated legacy monolith to microservices architecture on AWS EKS, improving scalability and maintainability",
        "Designed and implemented RESTful APIs serving 50K+ QPS with 99.99% uptime",
        "Optimized database queries and introduced caching layer with Redis, improving query performance by 60%",
        "Implemented event-driven architecture using Kafka for real-time order processing",
        "Led migration from SQL Server to PostgreSQL, ensuring zero downtime during transition",
        "Developed real-time collaboration features using WebSockets and SignalR",
        "Created comprehensive test suite achieving 90% code coverage across all services",
        "Mentored 4 junior developers through code reviews and pair programming sessions",
        "Designed and built a multi-tenant SaaS platform serving 500+ enterprise customers",
        "Reduced infrastructure costs by 35% through Kubernetes resource optimization and spot instances",
        "Implemented OAuth 2.0 / OpenID Connect authentication flow serving 1M+ users",
        "Built real-time data pipeline processing 5TB of data daily using Kafka and Spark"
    ];

    private static readonly string[] ProjectNames =
    [
        "E-Commerce Microservice Platform", "Real-Time Analytics Dashboard",
        "Cloud-Native Payment Gateway", "Event-Driven Notification System",
        "Distributed Task Scheduler", "API Gateway with Rate Limiting",
        "Multi-Tenant SaaS Foundation", "Real-Time Collaboration Hub",
        "Data Pipeline Orchestrator", "Infrastructure as Code Framework"
    ];

    private static readonly string[] ProjectDescriptions =
    [
        "Built a cloud-native platform with auto-scaling, fault tolerance, and observability. Used event-driven architecture to handle peak loads of 100K concurrent users.",
        "Designed and developed a full-stack application with real-time updates, comprehensive test coverage, and CI/CD pipeline. Reduced deployment failures by 80%.",
        "Created a scalable distributed system using message queues and container orchestration. Improved system reliability to 99.99% uptime.",
        "Implemented a microservice-based solution with comprehensive monitoring, logging, and alerting. Reduced mean time to recovery from 4 hours to 15 minutes.",
        "Developed an API-first platform with GraphQL, caching strategies, and rate limiting. Reduced average response time by 65% through query optimization."
    ];

    private static readonly string[] ProjectRoles =
    [
        "Lead Developer", "Full Stack Developer", "Frontend Developer",
        "Backend Developer", "DevOps Engineer", "Software Architect"
    ];

    private static readonly string[] DegreeTypes =
    [
        "Bachelor's", "Master's", "PhD", "Associate", "Diploma"
    ];

    private static readonly string[] FieldsOfStudy =
    [
        "Computer Science", "Software Engineering", "Information Technology",
        "Data Science", "Computer Engineering", "Electrical Engineering"
    ];

    private static readonly string[] Institutions =
    [
        "MIT", "Stanford University", "Carnegie Mellon University",
        "UC Berkeley", "Georgia Tech", "University of Washington",
        "University of Illinois Urbana-Champaign", "Caltech",
        "ETH Zurich", "University of Cambridge", "University of Oxford",
        "National University of Singapore"
    ];

    private readonly ContentDbContext _db;
    private readonly ILogger<SeedController> _logger;

    public SeedController(ContentDbContext db, ILogger<SeedController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Seed([FromBody] SeedRequest? request)
    {
        var r = request ?? new SeedRequest();
        var userId = r.UserId ?? Guid.NewGuid();
        var faker = new Faker("en");

        var usedSkills = new HashSet<string>();
        string NextSkill()
        {
            var pool = TechSkills.Where(s => !usedSkills.Contains(s)).ToList();
            if (pool.Count == 0) return faker.PickRandom(TechSkills);
            var pick = faker.PickRandom(pool);
            usedSkills.Add(pick);
            return pick;
        }

        var skillCategoryMap = SkillCategories
            .SelectMany(kv => kv.Value.Select(s => (Skill: s, Category: kv.Key)))
            .ToDictionary(x => x.Skill, x => x.Category);

        var experiences = Enumerable.Range(0, r.ExperienceCount).Select(_ =>
        {
            var startDate = faker.Date.Past(10).ToUniversalTime();
            var hasEndDate = faker.Random.Bool(0.7f);
            var endDate = hasEndDate
                ? faker.Date.Soon(0, startDate.AddYears(faker.Random.Int(1, 4))).ToUniversalTime()
                : (DateTime?)null;

            return new Experience
            {
                Id = Guid.NewGuid(),
                Title = faker.PickRandom(JobTitles),
                Company = faker.PickRandom(Companies),
                Description = faker.PickRandom(ExperienceDescriptions),
                StartDate = startDate,
                EndDate = endDate,
                Status = hasEndDate ? "Completed" : "Ongoing",
                UserId = userId
            };
        }).ToList();

        var skills = Enumerable.Range(0, r.SkillCount).Select(_ =>
        {
            var name = NextSkill();
            var category = skillCategoryMap.GetValueOrDefault(name, "Backend");
            var level = faker.PickRandom("Beginner", "Intermediate", "Advanced", "Expert");

            return new Skill
            {
                Id = Guid.NewGuid(),
                Name = name,
                Level = level,
                YearsOfExperience = level switch
                {
                    "Beginner" => faker.Random.Int(0, 2),
                    "Intermediate" => faker.Random.Int(2, 4),
                    "Advanced" => faker.Random.Int(4, 7),
                    "Expert" => faker.Random.Int(7, 15),
                    _ => faker.Random.Int(1, 15)
                },
                Category = category,
                UserId = userId
            };
        }).ToList();

        var projects = Enumerable.Range(0, r.ProjectCount).Select(_ =>
        {
            var startDate = faker.Date.Past(5).ToUniversalTime();
            var hasEndDate = faker.Random.Bool(0.8f);

            return new Project
            {
                Id = Guid.NewGuid(),
                Title = faker.PickRandom(ProjectNames),
                Description = faker.PickRandom(ProjectDescriptions),
                Role = faker.PickRandom(ProjectRoles),
                StartDate = startDate,
                EndDate = hasEndDate
                    ? faker.Date.Soon(0, startDate.AddMonths(faker.Random.Int(2, 18))).ToUniversalTime()
                    : null,
                RepositoryUrl = $"https://github.com/user/{faker.Hacker.Noun()}",
                DemoUrl = faker.Random.Bool(0.5f) ? $"https://{faker.Internet.DomainName()}" : null,
                Status = faker.PickRandom("Completed", "Ongoing", "On Hold"),
                UserId = userId,
                SkillsJson = $"[\"{string.Join("\",\"", faker.Make(faker.Random.Int(2, 5), () => faker.PickRandom(TechSkills)))}\"]"
            };
        }).ToList();

        var educations = Enumerable.Range(0, r.EducationCount).Select(_ =>
        {
            var startDate = faker.Date.Past(8).ToUniversalTime();
            var hasEndDate = faker.Random.Bool(0.85f);

            return new Education
            {
                Id = Guid.NewGuid(),
                InstitutionName = faker.PickRandom(Institutions),
                DegreeType = faker.PickRandom(DegreeTypes),
                FieldOfStudy = faker.PickRandom(FieldsOfStudy),
                Specialization = faker.PickRandom("Machine Learning", "Software Engineering", "Data Science", "Cybersecurity", "Cloud Computing", null),
                StartDate = startDate,
                EndDate = hasEndDate
                    ? faker.Date.Soon(0, startDate.AddYears(faker.Random.Int(3, 5))).ToUniversalTime()
                    : null,
                Status = hasEndDate ? "Completed" : "Ongoing",
                City = faker.Address.City(),
                UserId = userId
            };
        }).ToList();

        var certifications = new Faker<Certification>("en")
            .RuleFor(c => c.Id, _ => Guid.NewGuid())
            .RuleFor(c => c.Name, f => f.PickRandom("AWS Solutions Architect", "Google Cloud Professional", "Azure Administrator", "CISSP", "PMP", "Certified Kubernetes Administrator", "TOGAF", "ITIL Foundation"))
            .RuleFor(c => c.IssuingOrganization, f => f.PickRandom("Amazon", "Google", "Microsoft", "ISC2", "PMI", "CNCF", "The Open Group", "AXELOS"))
            .RuleFor(c => c.IssueDate, f => f.Date.Past(3).ToUniversalTime())
            .RuleFor(c => c.CredentialUrl, f => $"https://credential.example.com/{Guid.NewGuid()}")
            .RuleFor(c => c.UserId, userId)
            .Generate(r.CertificationCount);

        var socialLinks = new Faker<SocialLink>("en")
            .RuleFor(s => s.Id, _ => Guid.NewGuid())
            .RuleFor(s => s.Platform, f => f.PickRandom("LinkedIn", "GitHub", "Twitter", "Medium", "Dev.to", "Stack Overflow"))
            .RuleFor(s => s.Url, f => $"https://{f.Internet.DomainName()}/{f.Internet.UserName()}")
            .RuleFor(s => s.UserId, userId)
            .Generate(r.SocialLinkCount);

        var interests = new Faker<Interest>("en")
            .RuleFor(i => i.Id, _ => Guid.NewGuid())
            .RuleFor(i => i.Name, f => f.PickRandom("Open Source", "Machine Learning", "Cloud Computing", "Photography", "Travel", "Reading", "Chess", "Hiking", "Gaming", "Music"))
            .RuleFor(i => i.UserId, userId)
            .Generate(r.InterestCount);

        var languages = new Faker<Language>("en")
            .RuleFor(l => l.Id, _ => Guid.NewGuid())
            .RuleFor(l => l.Name, f => f.PickRandom("English", "French", "Spanish", "German", "Arabic", "Mandarin", "Japanese", "Portuguese", "Italian", "Dutch"))
            .RuleFor(l => l.Level, f => f.PickRandom("Native", "Fluent", "Advanced", "Intermediate", "Beginner"))
            .RuleFor(l => l.UserId, userId)
            .Generate(r.LanguageCount);

        var hackathons = new Faker<Hackathon>("en")
            .RuleFor(h => h.Id, _ => Guid.NewGuid())
            .RuleFor(h => h.Name, f => f.PickRandom("HackMIT", "TechCrunch Disrupt", "NASA Space Apps", "Devpost Global Hackathon", "MLH Fellowship", "ETHGlobal", "HackDavis"))
            .RuleFor(h => h.Organization, f => f.Company.CompanyName())
            .RuleFor(h => h.Date, f => f.Date.Past(3).ToUniversalTime())
            .RuleFor(h => h.Description, f => f.Lorem.Sentence())
            .RuleFor(h => h.Role, f => f.PickRandom("Participant", "Team Lead", "Mentor", "Judge"))
            .RuleFor(h => h.Result, f => f.PickRandom("Winner", "Finalist", "Top 10", "Participant", null))
            .RuleFor(h => h.UserId, userId)
            .Generate(r.HackathonCount);

        var academicActivities = new Faker<AcademicActivity>("en")
            .RuleFor(a => a.Id, _ => Guid.NewGuid())
            .RuleFor(a => a.Title, f => f.PickRandom("Club President", "Volunteer Tutor", "Research Assistant", "Student Ambassador", "Peer Mentor", "Teaching Assistant"))
            .RuleFor(a => a.Organization, f => f.Company.CompanyName())
            .RuleFor(a => a.Description, f => f.Lorem.Sentence())
            .RuleFor(a => a.StartDate, f => f.Date.Past(4).ToUniversalTime())
            .RuleFor(a => a.EndDate, (f, a) => f.Random.Bool(0.7f) ? f.Date.Soon(0, a.StartDate.AddMonths(f.Random.Int(6, 24))).ToUniversalTime() : null)
            .RuleFor(a => a.UserId, userId)
            .Generate(r.AcademicActivityCount);

        var cvProfiles = new Faker<CVProfile>("en")
            .RuleFor(c => c.Id, _ => Guid.NewGuid())
            .RuleFor(c => c.Title, f => $"{f.PickRandom(JobTitles)} — {f.Company.CompanySuffix()}")
            .RuleFor(c => c.Summary, f => string.Join(" ", f.PickRandom(ExperienceDescriptions, 3)))
            .RuleFor(c => c.UserId, userId)
            .Generate(r.CvProfileCount);

        _db.Experiences.AddRange(experiences);
        _db.Skills.AddRange(skills);
        _db.Projects.AddRange(projects);
        _db.Educations.AddRange(educations);
        _db.Certifications.AddRange(certifications);
        _db.SocialLinks.AddRange(socialLinks);
        _db.Interests.AddRange(interests);
        _db.Languages.AddRange(languages);
        _db.Hackathons.AddRange(hackathons);
        _db.AcademicActivities.AddRange(academicActivities);
        _db.CVProfiles.AddRange(cvProfiles);

        await _db.SaveChangesAsync();

        var summary = new SeedSummary
        {
            UserId = userId,
            Experiences = experiences.Count,
            Skills = skills.Count,
            Projects = projects.Count,
            Educations = educations.Count,
            Certifications = certifications.Count,
            SocialLinks = socialLinks.Count,
            Interests = interests.Count,
            Languages = languages.Count,
            Hackathons = hackathons.Count,
            AcademicActivities = academicActivities.Count,
            CvProfiles = cvProfiles.Count,
            Total = experiences.Count + skills.Count + projects.Count + educations.Count
                  + certifications.Count + socialLinks.Count + interests.Count + languages.Count
                  + hackathons.Count + academicActivities.Count + cvProfiles.Count
        };

        _logger.LogInformation("Seeded {Total} records for User {UserId}", summary.Total, userId);
        return Ok(ApiResponse<SeedSummary>.Ok(summary));
    }

    [HttpDelete]
    public async Task<IActionResult> Clear([FromQuery] Guid? userId)
    {
        if (userId.HasValue)
        {
            await _db.Experiences.Where(e => e.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Skills.Where(s => s.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Projects.Where(p => p.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Educations.Where(e => e.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Certifications.Where(c => c.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.SocialLinks.Where(s => s.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Interests.Where(i => i.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Languages.Where(l => l.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.Hackathons.Where(h => h.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.AcademicActivities.Where(a => a.UserId == userId.Value).ExecuteDeleteAsync();
            await _db.CVProfiles.Where(c => c.UserId == userId.Value).ExecuteDeleteAsync();
        }
        else
        {
            _db.Experiences.RemoveRange(await _db.Experiences.ToListAsync());
            _db.Skills.RemoveRange(await _db.Skills.ToListAsync());
            _db.Projects.RemoveRange(await _db.Projects.ToListAsync());
            _db.Educations.RemoveRange(await _db.Educations.ToListAsync());
            _db.Certifications.RemoveRange(await _db.Certifications.ToListAsync());
            _db.SocialLinks.RemoveRange(await _db.SocialLinks.ToListAsync());
            _db.Interests.RemoveRange(await _db.Interests.ToListAsync());
            _db.Languages.RemoveRange(await _db.Languages.ToListAsync());
            _db.Hackathons.RemoveRange(await _db.Hackathons.ToListAsync());
            _db.AcademicActivities.RemoveRange(await _db.AcademicActivities.ToListAsync());
            _db.CVProfiles.RemoveRange(await _db.CVProfiles.ToListAsync());
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Cleared data for User {UserId}", userId?.ToString() ?? "ALL");
        return Ok(ApiResponse<object>.Ok(new { cleared = true, userId = userId?.ToString() ?? "all" }));
    }
}

public class SeedRequest
{
    public Guid? UserId { get; set; }
    public int ExperienceCount { get; set; } = 5;
    public int SkillCount { get; set; } = 10;
    public int ProjectCount { get; set; } = 4;
    public int EducationCount { get; set; } = 2;
    public int CertificationCount { get; set; } = 3;
    public int SocialLinkCount { get; set; } = 3;
    public int InterestCount { get; set; } = 5;
    public int LanguageCount { get; set; } = 2;
    public int HackathonCount { get; set; } = 2;
    public int AcademicActivityCount { get; set; } = 2;
    public int CvProfileCount { get; set; } = 1;
}

public class SeedSummary
{
    public Guid UserId { get; set; }
    public int Experiences { get; set; }
    public int Skills { get; set; }
    public int Projects { get; set; }
    public int Educations { get; set; }
    public int Certifications { get; set; }
    public int SocialLinks { get; set; }
    public int Interests { get; set; }
    public int Languages { get; set; }
    public int Hackathons { get; set; }
    public int AcademicActivities { get; set; }
    public int CvProfiles { get; set; }
    public int Total { get; set; }
}
