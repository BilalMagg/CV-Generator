using JobOfferService.DTOs;
using JobOfferService.Entities;
using JobOfferService.Repositories;

namespace JobOfferService.Services;

public interface IJobOfferService
{
    // Write Operations
    Task<Guid> SubmitRawJobOfferAsync(SubmitJobOfferDto dto);
    Task ProcessExtractedDataAsync(Guid jobId, ExtractedJobDto dto);
    Task UpdateStatusAsync(Guid id, UpdateJobStatusDto dto);
    Task DeleteAsync(Guid id);

    // Read Operations
    Task<JobOfferDetailDto?> GetByIdAsync(Guid id);
    Task<JobOfferListDto> GetAllAsync(Guid userId, int page, int pageSize);
    Task<JobOfferStatisticsDto> GetStatisticsAsync(Guid userId);
}

public class JobOfferService : IJobOfferService
{
    private readonly IJobOfferRepository _repository;
    // private readonly IKafkaPublisher _kafkaPublisher; // (Uncomment when you add Kafka)

    public JobOfferService(IJobOfferRepository repository)
    {
        _repository = repository;
    }

    // 1. STEP ONE: User pastes a link or text
    public async Task<Guid> SubmitRawJobOfferAsync(SubmitJobOfferDto dto)
    {
        var jobOffer = new JobOffer
        {
            UserId = dto.UserId,
            RawDescription = dto.RawText ?? "No description provided",
            SourceUrl = dto.SourceUrl,
            Status = JobOfferStatus.DRAFT, // Starts as a draft while AI works
            
            // Temporary placeholders until the AI extracts the real data
            EnterpriseName = "Pending AI Extraction...",
            JobRole = "Pending AI Extraction..."
        };

        await _repository.CreateAsync(jobOffer);

        // TODO: Publish event to Kafka: "ExtractJobDataEvent(jobOffer.Id)"
        // await _kafkaPublisher.PublishAsync(new ExtractJobDataEvent(jobOffer.Id));

        return jobOffer.Id;
    }

    // 2. STEP TWO: AI finishes and sends the structured JSON here
    public async Task ProcessExtractedDataAsync(Guid jobId, ExtractedJobDto dto)
    {
        // We must fetch it WITH its details to update properly
        var jobOffer = await _repository.GetByIdWithDetailsAsync(jobId);
        if (jobOffer == null) throw new KeyNotFoundException("Job offer not found");

        // Update scalar properties
        jobOffer.EnterpriseName = dto.EnterpriseName;
        jobOffer.EnterpriseDescription = dto.EnterpriseDescription;
        jobOffer.JobRole = dto.JobRole;
        jobOffer.RawDescription = dto.RawDescription;
        jobOffer.RequiredExperienceYears = dto.RequiredExperienceYears;
        jobOffer.SeniorityLevel = dto.SeniorityLevel;
        jobOffer.EmploymentType = dto.EmploymentType;
        jobOffer.Location = dto.Location;
        jobOffer.LocationType = dto.LocationType;
        jobOffer.EducationRequirements = dto.EducationRequirements;
        jobOffer.Status = JobOfferStatus.OPEN; // Now it's ready!

        // Clear existing lists (in case this is a re-extraction)
        jobOffer.Skills.Clear();
        jobOffer.Responsibilities.Clear();
        jobOffer.Benefits.Clear();

        // Map arrays to Database Entities
        foreach (var reqSkill in dto.RequiredSkills)
        {
            jobOffer.Skills.Add(new JobSkill { Name = reqSkill, Type = SkillType.HARD_SKILL, IsMandatory = true });
        }
        foreach (var softSkill in dto.SoftSkills)
        {
            jobOffer.Skills.Add(new JobSkill { Name = softSkill, Type = SkillType.SOFT_SKILL, IsMandatory = false });
        }
        foreach (var resp in dto.Responsibilities)
        {
            jobOffer.Responsibilities.Add(new JobResponsibility { Description = resp });
        }
        foreach (var benefit in dto.Benefits)
        {
            jobOffer.Benefits.Add(new JobBenefit { Description = benefit });
        }

        // Save everything to the database
        await _repository.UpdateWithDetailsAsync(jobOffer);

        // TODO: Publish event: "JobOfferReadyForCvGenerationEvent(jobOffer.Id)"
    }

    // 3. GET FULL DETAILS (Used by the frontend and gRPC)
    public async Task<JobOfferDetailDto?> GetByIdAsync(Guid id)
    {
        var job = await _repository.GetByIdWithDetailsAsync(id);
        if (job == null) return null;

        // Map Entity -> DTO (You can use AutoMapper for this later to save time)
        return new JobOfferDetailDto(
            Id: job.Id,
            UserId: job.UserId,
            EnterpriseName: job.EnterpriseName,
            EnterpriseDescription: job.EnterpriseDescription,
            JobRole: job.JobRole,
            RawDescription: job.RawDescription,
            RequiredExperienceYears: job.RequiredExperienceYears,
            SeniorityLevel: job.SeniorityLevel,
            EmploymentType: job.EmploymentType,
            Location: job.Location,
            LocationType: job.LocationType,
            EducationRequirements: job.EducationRequirements,
            SourceUrl: job.SourceUrl,
            Status: job.Status.ToString(),
            CreatedAt: job.CreatedAt,
            UpdatedAt: job.UpdatedAt,
            Skills: job.Skills.Select(s => new JobSkillDto(s.Id, s.Name, s.Type.ToString(), s.IsMandatory)).ToList(),
            Responsibilities: job.Responsibilities.Select(r => new JobResponsibilityDto(r.Id, r.Description)).ToList(),
            Benefits: job.Benefits.Select(b => new JobBenefitDto(b.Id, b.Description)).ToList()
        );
    }

    // 4. GET ALL (Summary view for the dashboard)
    public async Task<JobOfferListDto> GetAllAsync(Guid userId, int page, int pageSize)
    {
        var total = await _repository.GetTotalCountAsync(userId);
        var jobs = await _repository.GetAllAsync(userId, page, pageSize);

        var items = jobs.Select(job => new JobOfferSummaryDto(
            Id: job.Id,
            UserId: job.UserId,
            EnterpriseName: job.EnterpriseName,
            JobRole: job.JobRole,
            Location: job.Location,
            Status: job.Status.ToString(),
            CreatedAt: job.CreatedAt
        )).ToList();

        return new JobOfferListDto(items, total, page, pageSize);
    }

    public async Task UpdateStatusAsync(Guid id, UpdateJobStatusDto dto)
    {
        var job = await _repository.GetByIdAsync(id);
        if (job == null) throw new KeyNotFoundException("Job not found");

        if (Enum.TryParse<JobOfferStatus>(dto.Status, true, out var status))
        {
            job.Status = status;
            await _repository.UpdateAsync(job);
        }
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<JobOfferStatisticsDto> GetStatisticsAsync(Guid userId)
    {
        var stats = await _repository.GetStatisticsAsync(userId);
        return new JobOfferStatisticsDto(
            Total: stats.Values.Sum(),
            Draft: stats.GetValueOrDefault(JobOfferStatus.DRAFT, 0),
            Open: stats.GetValueOrDefault(JobOfferStatus.OPEN, 0),
            Closed: stats.GetValueOrDefault(JobOfferStatus.CLOSED, 0),
            Archived: stats.GetValueOrDefault(JobOfferStatus.ARCHIVED, 0)
        );
    }
}