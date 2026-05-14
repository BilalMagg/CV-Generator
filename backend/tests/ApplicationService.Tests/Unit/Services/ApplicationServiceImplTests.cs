using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Xunit;
using ApplicationService.DTOs;
using ApplicationService.Entities;
using ApplicationService.Events;
using ApplicationService.Repositories;
using ApplicationService.Services;

namespace ApplicationService.Tests.Unit.Services;

public class ApplicationServiceImplTests
{
    private readonly IApplicationRepository _appRepo;
    private readonly IApplicationStatusHistoryRepository _historyRepo;
    private readonly IKafkaPublisher _kafka;
    private readonly ApplicationServiceImpl _sut;

    public ApplicationServiceImplTests()
    {
        _appRepo = Substitute.For<IApplicationRepository>();
        _historyRepo = Substitute.For<IApplicationStatusHistoryRepository>();
        _kafka = Substitute.For<IKafkaPublisher>();
        var logger = Substitute.For<ILogger<ApplicationServiceImpl>>();

        _sut = new ApplicationServiceImpl(_appRepo, _historyRepo, _kafka, logger);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnPaginatedResults()
    {
        var candidateId = Guid.NewGuid();
        var apps = new List<Application>
        {
            new() { Id = Guid.NewGuid(), CandidateId = candidateId, CompanyName = "Acme", PositionTitle = "Dev" },
            new() { Id = Guid.NewGuid(), CandidateId = candidateId, CompanyName = "Beta", PositionTitle = "QA" },
        };

        _appRepo.GetAllAsync(candidateId, 1, 20).Returns(apps);
        _appRepo.GetTotalCountAsync(candidateId).Returns(2);

        var result = await _sut.GetAllAsync(candidateId, 1, 20);

        result.Items.Should().HaveCount(2);
        result.Total.Should().Be(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task CreateAsync_ShouldPublishEvent()
    {
        var dto = new CreateApplicationDto(
            CandidateId: Guid.NewGuid(),
            CvVersionId: null,
            JobOfferId: null,
            CompanyName: "Test Corp",
            PositionTitle: "Engineer",
            OfferSource: "LinkedIn",
            Notes: null
        );

        var created = new Application
        {
            Id = Guid.NewGuid(),
            CandidateId = dto.CandidateId,
            CompanyName = dto.CompanyName,
            PositionTitle = dto.PositionTitle,
            OfferSource = dto.OfferSource,
            Status = ApplicationStatus.PENDING,
            AppliedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _appRepo.CreateAsync(Arg.Any<Application>()).Returns(created);

        var result = await _sut.CreateAsync(dto, "user-123");

        result.Should().NotBeNull();
        result.CompanyName.Should().Be("Test Corp");
        result.PositionTitle.Should().Be("Engineer");

        await _kafka.Received(1).PublishAsync(
            Arg.Is<ApplicationCreatedEvent>(e =>
                e.ApplicationId == created.Id &&
                e.CandidateId == created.CandidateId &&
                e.CompanyName == "Test Corp"),
            "application.created");
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldReturnNull_WhenAppNotFound()
    {
        _appRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((Application?)null);

        var result = await _sut.UpdateStatusAsync(
            Guid.NewGuid(),
            new UpdateStatusDto("REVIEWED", "Looks good"),
            "user-123");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenAppNotFound()
    {
        _appRepo.GetByIdAsync(Arg.Any<Guid>()).Returns((Application?)null);

        var result = await _sut.DeleteAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldCalculateCounts()
    {
        var candidateId = Guid.NewGuid();
        var stats = new Dictionary<ApplicationStatus, int>
        {
            { ApplicationStatus.PENDING, 3 },
            { ApplicationStatus.REVIEWED, 2 },
            { ApplicationStatus.ACCEPTED, 1 },
        };

        _appRepo.GetStatisticsAsync(candidateId).Returns(stats);

        var result = await _sut.GetStatisticsAsync(candidateId);

        result.Total.Should().Be(6);
        result.Pending.Should().Be(3);
        result.Reviewed.Should().Be(2);
        result.Accepted.Should().Be(1);
        result.Rejected.Should().Be(0);
    }
}
