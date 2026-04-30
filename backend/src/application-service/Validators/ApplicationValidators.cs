using FluentValidation;
using ApplicationService.DTOs;

namespace ApplicationService.Validators;

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
    }
}

public class UpdateStatusValidator : AbstractValidator<UpdateStatusDto>
{
    public UpdateStatusValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required")
            .Must(BeValidStatus).WithMessage("Invalid status value. Valid values: PENDING, REVIEWED, INTERVIEW, ACCEPTED, REJECTED, CANCELLED");
    }

    private static bool BeValidStatus(string status)
    {
        var validStatuses = new[] { "PENDING", "REVIEWED", "INTERVIEW", "ACCEPTED", "REJECTED", "CANCELLED" };
        return validStatuses.Contains(status.ToUpperInvariant());
    }
}