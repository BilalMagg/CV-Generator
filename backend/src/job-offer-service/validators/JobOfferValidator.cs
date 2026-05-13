using FluentValidation;
using JobOfferService.DTOs;
using JobOfferService.Entities; // Needed for the enum validation

namespace JobOfferService.Validators;

public class SubmitJobOfferValidator : AbstractValidator<SubmitJobOfferDto>
{
    public SubmitJobOfferValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        // Must have either raw text OR a URL to be valid
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.RawText) || !string.IsNullOrWhiteSpace(x.SourceUrl))
            .WithMessage("You must provide either RawText or a SourceUrl.");
    }
}

public class ExtractedJobValidator : AbstractValidator<ExtractedJobDto>
{
    public ExtractedJobValidator()
    {
        RuleFor(x => x.EnterpriseName)
            .NotEmpty().WithMessage("Enterprise name is required")
            .MaximumLength(200).WithMessage("Enterprise name cannot exceed 200 characters");

        RuleFor(x => x.JobRole)
            .NotEmpty().WithMessage("Job role is required")
            .MaximumLength(150).WithMessage("Job role cannot exceed 150 characters");

        RuleFor(x => x.RawDescription)
            .NotEmpty().WithMessage("Raw description cannot be empty");
            
        // Ensure lists aren't null (they can be empty, but not null)
        RuleFor(x => x.RequiredSkills).NotNull();
        RuleFor(x => x.Responsibilities).NotNull();
    }
}

public class UpdateJobStatusValidator : AbstractValidator<UpdateJobStatusDto>
{
    public UpdateJobStatusValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required")
            .Must(BeValidStatus).WithMessage("Invalid status value. Valid values: DRAFT, OPEN, CLOSED, ARCHIVED");
    }

    private static bool BeValidStatus(string status)
    {
        var validStatuses = new[] { "DRAFT", "OPEN", "CLOSED", "ARCHIVED" };
        return validStatuses.Contains(status.ToUpperInvariant());
    }
}