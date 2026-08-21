using FluentValidation;
using ApplicationService.DTOs;
using ApplicationService.Entities;

namespace ApplicationService.Validators;

public static class ApplicationStatusValues
{
    public static readonly string[] All =
        ["SAVED", "APPLIED", "SCREENING", "INTERVIEW", "OFFER", "ACCEPTED", "REJECTED", "WITHDRAWN"];
}

public class CreateApplicationValidator : AbstractValidator<CreateApplicationDto>
{
    public CreateApplicationValidator()
    {
        RuleFor(x => x.CandidateId)
            .NotEmpty().WithMessage("CandidateId is required");

        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required")
            .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters");

        RuleFor(x => x.PositionTitle)
            .NotEmpty().WithMessage("Position title is required")
            .MaximumLength(150).WithMessage("Position title cannot exceed 150 characters");

        RuleFor(x => x.OfferSource)
            .MaximumLength(100).WithMessage("Offer source cannot exceed 100 characters");

        RuleFor(x => x.Status)
            .Must(s => ApplicationStatusValues.All.Contains(s.ToUpperInvariant()))
            .WithMessage($"Invalid initial status. Valid values: {string.Join(", ", ApplicationStatusValues.All)}")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));

        RuleFor(x => x.Origin)
            .Must(BeValidOrigin)
            .WithMessage("Invalid origin value. Valid values: MANUAL, FROM_JOB_OFFER, AI_AGENT_AUTO_APPLY, IMPORT")
            .When(x => !string.IsNullOrWhiteSpace(x.Origin));
    }

    private static bool BeValidOrigin(string origin) =>
        Enum.TryParse<ApplicationOrigin>(origin, true, out _);
}

public class UpdateApplicationValidator : AbstractValidator<UpdateApplicationDto>
{
    public UpdateApplicationValidator()
    {
        RuleFor(x => x.CompanyName)
            .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters")
            .When(x => x.CompanyName != null);

        RuleFor(x => x.PositionTitle)
            .MaximumLength(150).WithMessage("Position title cannot exceed 150 characters")
            .When(x => x.PositionTitle != null);

        RuleFor(x => x.OfferSource)
            .MaximumLength(100).WithMessage("Offer source cannot exceed 100 characters")
            .When(x => x.OfferSource != null);
    }
}

public class UpdateStatusValidator : AbstractValidator<UpdateStatusDto>
{
    public UpdateStatusValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required")
            .Must(s => ApplicationStatusValues.All.Contains(s.ToUpperInvariant()))
            .WithMessage($"Invalid status value. Valid values: {string.Join(", ", ApplicationStatusValues.All)}");
    }
}

public class DuplicateCheckValidator : AbstractValidator<DuplicateCheckRequestDto>
{
    public DuplicateCheckValidator()
    {
        RuleFor(x => x.CompanyName)
            .NotEmpty().WithMessage("Company name is required")
            .MaximumLength(200).WithMessage("Company name cannot exceed 200 characters");

        RuleFor(x => x.PositionTitle)
            .NotEmpty().WithMessage("Position title is required")
            .MaximumLength(150).WithMessage("Position title cannot exceed 150 characters");
    }
}

public class CreateAttemptValidator : AbstractValidator<CreateAttemptDto>
{
    public CreateAttemptValidator()
    {
        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel is required")
            .Must(c => Enum.TryParse<AttemptChannel>(c, true, out _))
            .WithMessage("Invalid channel value. Valid values: EMAIL_GMAIL, EMAIL_SMTP, WHATSAPP, LINKEDIN_MESSAGE, LINKEDIN_CONNECTION, WEB_FORM, IN_PERSON, OTHER");

        RuleFor(x => x.InitiatedBy)
            .Must(b => Enum.TryParse<AttemptInitiatedBy>(b, true, out _))
            .WithMessage("Invalid initiatedBy value. Valid values: USER, AI_AGENT, SCHEDULE")
            .When(x => !string.IsNullOrWhiteSpace(x.InitiatedBy));

        RuleFor(x => x.Status)
            .Must(s => Enum.TryParse<AttemptStatus>(s, true, out _))
            .WithMessage("Invalid status value. Valid values: DRAFT, SCHEDULED, SENT, FAILED")
            .When(x => !string.IsNullOrWhiteSpace(x.Status));

        RuleFor(x => x.Subject)
            .MaximumLength(300).WithMessage("Subject cannot exceed 300 characters")
            .When(x => x.Subject != null);

        RuleFor(x => x.RecipientName)
            .MaximumLength(200).WithMessage("Recipient name cannot exceed 200 characters")
            .When(x => x.RecipientName != null);

        RuleFor(x => x.RecipientContact)
            .MaximumLength(300).WithMessage("Recipient contact cannot exceed 300 characters")
            .When(x => x.RecipientContact != null);

        RuleFor(x => x.FailureReason)
            .MaximumLength(500).WithMessage("Failure reason cannot exceed 500 characters")
            .When(x => x.FailureReason != null);
    }
}

public class UpdateAttemptValidator : AbstractValidator<UpdateAttemptDto>
{
    public UpdateAttemptValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s == null || Enum.TryParse<AttemptStatus>(s, true, out _))
            .WithMessage("Invalid status value. Valid values: DRAFT, SCHEDULED, SENT, FAILED");

        RuleFor(x => x.Subject)
            .MaximumLength(300).WithMessage("Subject cannot exceed 300 characters")
            .When(x => x.Subject != null);

        RuleFor(x => x.RecipientName)
            .MaximumLength(200).WithMessage("Recipient name cannot exceed 200 characters")
            .When(x => x.RecipientName != null);

        RuleFor(x => x.RecipientContact)
            .MaximumLength(300).WithMessage("Recipient contact cannot exceed 300 characters")
            .When(x => x.RecipientContact != null);

        RuleFor(x => x.FailureReason)
            .MaximumLength(500).WithMessage("Failure reason cannot exceed 500 characters")
            .When(x => x.FailureReason != null);
    }
}
