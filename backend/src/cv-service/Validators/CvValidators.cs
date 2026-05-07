using FluentValidation;
using CvService.DTOs;

namespace CvService.Validators;

public class CreateCvValidator : AbstractValidator<CreateCvDto>
{
    public CreateCvValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.TemplateId)
            .NotEmpty().WithMessage("Template is required");
    }
}
