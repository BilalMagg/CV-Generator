using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;
using ApplicationService.DTOs;
using ApplicationService.Validators;

namespace ApplicationService.Tests.Unit.Validators;

public class CreateApplicationValidatorTests
{
    private readonly CreateApplicationValidator _sut = new();

    [Fact]
    public void ShouldPass_WhenAllFieldsValid()
    {
        var dto = new CreateApplicationDto(
            CandidateId: Guid.NewGuid(),
            CvVersionId: null,
            JobOfferId: null,
            CompanyName: "Acme Corp",
            PositionTitle: "Software Engineer",
            OfferSource: "LinkedIn",
            Notes: "Interesting offer");

        var result = _sut.TestValidate(dto);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ShouldFail_WhenCandidateIdIsEmpty()
    {
        var dto = new CreateApplicationDto(
            CandidateId: Guid.Empty,
            CvVersionId: null, JobOfferId: null,
            CompanyName: "Acme", PositionTitle: "Dev",
            OfferSource: null, Notes: null);

        var result = _sut.TestValidate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CandidateId");
    }

    [Fact]
    public void ShouldFail_WhenCompanyNameIsEmpty()
    {
        var dto = new CreateApplicationDto(
            CandidateId: Guid.NewGuid(),
            CvVersionId: null, JobOfferId: null,
            CompanyName: "", PositionTitle: "Dev",
            OfferSource: null, Notes: null);

        var result = _sut.TestValidate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CompanyName");
    }

    [Fact]
    public void ShouldFail_WhenPositionTitleIsEmpty()
    {
        var dto = new CreateApplicationDto(
            CandidateId: Guid.NewGuid(),
            CvVersionId: null, JobOfferId: null,
            CompanyName: "Acme", PositionTitle: "",
            OfferSource: null, Notes: null);

        var result = _sut.TestValidate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PositionTitle");
    }

    [Fact]
    public void ShouldFail_WhenCompanyNameExceedsMaxLength()
    {
        var dto = new CreateApplicationDto(
            CandidateId: Guid.NewGuid(),
            CvVersionId: null, JobOfferId: null,
            CompanyName: new string('x', 201),
            PositionTitle: "Dev",
            OfferSource: null, Notes: null);

        var result = _sut.TestValidate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CompanyName");
    }

    [Fact]
    public void ShouldFail_WhenPositionTitleExceedsMaxLength()
    {
        var dto = new CreateApplicationDto(
            CandidateId: Guid.NewGuid(),
            CvVersionId: null, JobOfferId: null,
            CompanyName: "Acme",
            PositionTitle: new string('x', 151),
            OfferSource: null, Notes: null);

        var result = _sut.TestValidate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PositionTitle");
    }
}
