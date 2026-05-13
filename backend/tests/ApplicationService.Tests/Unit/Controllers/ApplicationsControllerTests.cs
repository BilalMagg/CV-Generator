using System.Security.Claims;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using ApplicationService.Controllers;
using ApplicationService.DTOs;
using ApplicationService.Entities;
using ApplicationService.Services;

namespace ApplicationService.Tests.Unit.Controllers;

public class ApplicationsControllerTests
{
    private readonly IApplicationService _service;
    private readonly IValidator<CreateApplicationDto> _createValidator;
    private readonly IValidator<UpdateStatusDto> _statusValidator;
    private readonly IValidator<UpdateApplicationDto> _updateValidator;
    private readonly ApplicationsController _sut;

    public ApplicationsControllerTests()
    {
        _service = Substitute.For<IApplicationService>();
        _createValidator = Substitute.For<IValidator<CreateApplicationDto>>();
        _statusValidator = Substitute.For<IValidator<UpdateStatusDto>>();
        _updateValidator = Substitute.For<IValidator<UpdateApplicationDto>>();
        var logger = Substitute.For<ILogger<ApplicationsController>>();

        _sut = new ApplicationsController(
            _service, _createValidator, _statusValidator, _updateValidator, logger);

        // Set up authenticated user
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000001"),
            new Claim("sub", "00000000-0000-0000-0000-000000000001"),
        ], "test"));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
    }

    [Fact]
    public async Task GetAll_ShouldReturnOkWithApplications()
    {
        var candidateId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var appList = new ApplicationListDto([], 0, 1, 20);
        _service.GetAllAsync(candidateId, 1, 20).Returns(appList);

        var result = await _sut.GetAll(1, 20);

        result.Should().BeOfType<OkObjectResult>();
        var ok = result as OkObjectResult;
        ok!.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenDifferentUser()
    {
        var otherCandidateId = Guid.Parse("00000000-0000-0000-0000-000000000099");
        _service.GetByIdAsync(Arg.Any<Guid>()).Returns(new ApplicationResponseDto(
            Guid.NewGuid(), otherCandidateId, null, null,
            "Acme", "Dev", null, "PENDING",
            DateTime.UtcNow, DateTime.UtcNow, null));

        var result = await _sut.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnCreated_WithForcedCandidateId()
    {
        var dto = new CreateApplicationDto(
            CandidateId: Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            CvVersionId: null, JobOfferId: null,
            CompanyName: "NewCo", PositionTitle: "Manager",
            OfferSource: null, Notes: null);

        _createValidator.ValidateAsync(dto).Returns(new ValidationResult());

        var expectedCandidateId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        _service.CreateAsync(Arg.Is<CreateApplicationDto>(x =>
                x.CandidateId == expectedCandidateId), Arg.Any<string?>())
            .Returns(new ApplicationResponseDto(
                Guid.NewGuid(), expectedCandidateId, null, null,
                "NewCo", "Manager", null, "PENDING",
                DateTime.UtcNow, DateTime.UtcNow, null));

        var result = await _sut.Create(dto);

        result.Should().BeOfType<CreatedResult>();
    }

    [Fact]
    public async Task GetAll_ShouldReturnUnauthorized_WhenNoUser()
    {
        var unauthenticatedController = new ApplicationsController(
            _service, _createValidator, _statusValidator, _updateValidator,
            Substitute.For<ILogger<ApplicationsController>>());

        unauthenticatedController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        var result = await unauthenticatedController.GetAll(1, 20);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
