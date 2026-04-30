using CommonProtos.Content;
using Microsoft.EntityFrameworkCore;
using UserContentService;
using UserContentService.Entity;

namespace UserContentService.Grpc;

public class ContentServiceImpl : CommonProtos.Content.ContentServiceGrpc.ContentServiceGrpcBase
{
    private readonly ContentDbContext _db;
    private readonly ILogger<ContentServiceImpl> _logger;

    public ContentServiceImpl(ContentDbContext db, ILogger<ContentServiceImpl> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Projects
    public override async Task<ProjectProto> GetProjectById(GetProjectByIdRequest request, Grpc.Core.ServerCallContext context)
    {
        var project = await _db.Projects.FindAsync(Guid.Parse(request.Id));
        if (project == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Project not found"));

        return ToProjectProto(project);
    }

    public override async Task GetProjectsByUserId(GetProjectsByUserIdRequest request, Grpc.Core.IServerStreamWriter<ProjectProto> responseStream, Grpc.Core.ServerCallContext context)
    {
        var projects = await _db.Projects.Where(p => p.UserId == Guid.Parse(request.UserId)).ToListAsync();
        foreach (var project in projects)
            await responseStream.WriteAsync(ToProjectProto(project));
    }

    public override async Task<ProjectProto> CreateProject(CreateProjectRequest request, Grpc.Core.ServerCallContext context)
    {
        var project = new Project
        {
            Title = request.Title,
            Description = request.Description,
            Role = request.Role,
            Achievements = request.Achievements,
            StartDate = DateTime.Parse(request.StartDate),
            EndDate = string.IsNullOrEmpty(request.EndDate) ? null : DateTime.Parse(request.EndDate),
            RepositoryUrl = request.RepositoryUrl,
            DemoUrl = request.DemoUrl,
            Status = request.Status,
            UserId = Guid.Parse(request.UserId),
            SkillsJson = request.SkillsJson
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created project {Id}", project.Id);
        return ToProjectProto(project);
    }

    public override async Task<ProjectProto> UpdateProject(UpdateProjectRequest request, Grpc.Core.ServerCallContext context)
    {
        var project = await _db.Projects.FindAsync(Guid.Parse(request.Id));
        if (project == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Project not found"));

        project.Title = request.Title;
        project.Description = request.Description;
        project.Role = request.Role;
        project.Achievements = request.Achievements;
        project.StartDate = DateTime.Parse(request.StartDate);
        project.EndDate = string.IsNullOrEmpty(request.EndDate) ? null : DateTime.Parse(request.EndDate);
        project.RepositoryUrl = request.RepositoryUrl;
        project.DemoUrl = request.DemoUrl;
        project.Status = request.Status;
        project.SkillsJson = request.SkillsJson;

        await _db.SaveChangesAsync();
        return ToProjectProto(project);
    }

    public override async Task<DeleteProjectResponse> DeleteProject(DeleteProjectRequest request, Grpc.Core.ServerCallContext context)
    {
        var project = await _db.Projects.FindAsync(Guid.Parse(request.Id));
        if (project == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Project not found"));

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync();
        return new DeleteProjectResponse { Success = true };
    }

    // Skills
    public override async Task<SkillProto> GetSkillById(GetSkillByIdRequest request, Grpc.Core.ServerCallContext context)
    {
        var skill = await _db.Skills.FindAsync(Guid.Parse(request.Id));
        if (skill == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Skill not found"));

        return ToSkillProto(skill);
    }

    public override async Task GetSkillsByUserId(GetSkillsByUserIdRequest request, Grpc.Core.IServerStreamWriter<SkillProto> responseStream, Grpc.Core.ServerCallContext context)
    {
        var skills = await _db.Skills.Where(s => s.UserId == Guid.Parse(request.UserId)).ToListAsync();
        foreach (var skill in skills)
            await responseStream.WriteAsync(ToSkillProto(skill));
    }

    public override async Task<SkillProto> CreateSkill(CreateSkillRequest request, Grpc.Core.ServerCallContext context)
    {
        var skill = new Skill
        {
            Name = request.Name,
            Level = request.Level,
            YearsOfExperience = request.YearsOfExperience,
            UserId = Guid.Parse(request.UserId),
            Category = request.Category
        };

        _db.Skills.Add(skill);
        await _db.SaveChangesAsync();

        return ToSkillProto(skill);
    }

    public override async Task<SkillProto> UpdateSkill(UpdateSkillRequest request, Grpc.Core.ServerCallContext context)
    {
        var skill = await _db.Skills.FindAsync(Guid.Parse(request.Id));
        if (skill == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Skill not found"));

        skill.Name = request.Name;
        skill.Level = request.Level;
        skill.YearsOfExperience = request.YearsOfExperience;
        skill.Category = request.Category;

        await _db.SaveChangesAsync();
        return ToSkillProto(skill);
    }

    public override async Task<DeleteSkillResponse> DeleteSkill(DeleteSkillRequest request, Grpc.Core.ServerCallContext context)
    {
        var skill = await _db.Skills.FindAsync(Guid.Parse(request.Id));
        if (skill == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Skill not found"));

        _db.Skills.Remove(skill);
        await _db.SaveChangesAsync();
        return new DeleteSkillResponse { Success = true };
    }

    // Experiences
    public override async Task<ExperienceProto> GetExperienceById(GetExperienceByIdRequest request, Grpc.Core.ServerCallContext context)
    {
        var exp = await _db.Experiences.FindAsync(Guid.Parse(request.Id));
        if (exp == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Experience not found"));

        return ToExperienceProto(exp);
    }

    public override async Task GetExperiencesByUserId(GetExperiencesByUserIdRequest request, Grpc.Core.IServerStreamWriter<ExperienceProto> responseStream, Grpc.Core.ServerCallContext context)
    {
        var exps = await _db.Experiences.Where(e => e.UserId == Guid.Parse(request.UserId)).ToListAsync();
        foreach (var exp in exps)
            await responseStream.WriteAsync(ToExperienceProto(exp));
    }

    public override async Task<ExperienceProto> CreateExperience(CreateExperienceRequest request, Grpc.Core.ServerCallContext context)
    {
        var exp = new Experience
        {
            Title = request.Title,
            Company = request.Company,
            Description = request.Description,
            StartDate = DateTime.Parse(request.StartDate),
            EndDate = string.IsNullOrEmpty(request.EndDate) ? null : DateTime.Parse(request.EndDate),
            ReferenceUrl = request.ReferenceUrl,
            Status = request.Status,
            UserId = Guid.Parse(request.UserId)
        };

        _db.Experiences.Add(exp);
        await _db.SaveChangesAsync();

        return ToExperienceProto(exp);
    }

    public override async Task<ExperienceProto> UpdateExperience(UpdateExperienceRequest request, Grpc.Core.ServerCallContext context)
    {
        var exp = await _db.Experiences.FindAsync(Guid.Parse(request.Id));
        if (exp == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Experience not found"));

        exp.Title = request.Title;
        exp.Company = request.Company;
        exp.Description = request.Description;
        exp.StartDate = DateTime.Parse(request.StartDate);
        exp.EndDate = string.IsNullOrEmpty(request.EndDate) ? null : DateTime.Parse(request.EndDate);
        exp.ReferenceUrl = request.ReferenceUrl;
        exp.Status = request.Status;

        await _db.SaveChangesAsync();
        return ToExperienceProto(exp);
    }

    public override async Task<DeleteExperienceResponse> DeleteExperience(DeleteExperienceRequest request, Grpc.Core.ServerCallContext context)
    {
        var exp = await _db.Experiences.FindAsync(Guid.Parse(request.Id));
        if (exp == null)
            throw new RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "Experience not found"));

        _db.Experiences.Remove(exp);
        await _db.SaveChangesAsync();
        return new DeleteExperienceResponse { Success = true };
    }

    private static ProjectProto ToProjectProto(Project p) => new()
    {
        Id = p.Id.ToString(),
        Title = p.Title,
        Description = p.Description ?? "",
        Role = p.Role ?? "",
        Achievements = p.Achievements ?? "",
        StartDate = p.StartDate.ToString("O"),
        EndDate = p.EndDate?.ToString("O") ?? "",
        RepositoryUrl = p.RepositoryUrl ?? "",
        DemoUrl = p.DemoUrl ?? "",
        Status = p.Status,
        UserId = p.UserId.ToString(),
        SkillsJson = p.SkillsJson ?? "",
        AiSummaryJson = p.AiSummaryJson ?? ""
    };

    private static SkillProto ToSkillProto(Skill s) => new()
    {
        Id = s.Id.ToString(),
        Name = s.Name,
        Level = s.Level ?? "",
        YearsOfExperience = s.YearsOfExperience ?? 0,
        UserId = s.UserId.ToString(),
        Category = s.Category ?? ""
    };

    private static ExperienceProto ToExperienceProto(Experience e) => new()
    {
        Id = e.Id.ToString(),
        Title = e.Title,
        Company = e.Company ?? "",
        Description = e.Description ?? "",
        StartDate = e.StartDate.ToString("O"),
        EndDate = e.EndDate?.ToString("O") ?? "",
        ReferenceUrl = e.ReferenceUrl ?? "",
        Status = e.Status,
        UserId = e.UserId.ToString(),
        AiSummaryJson = e.AiSummaryJson ?? ""
    };
}