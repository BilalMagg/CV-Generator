using CommonProtos.Application;
using ApplicationService.Entities;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace ApplicationService.Services;

public class ApplicationGrpcServiceImpl : ApplicationServiceGrpc.ApplicationServiceGrpcBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ApplicationGrpcServiceImpl> _logger;

    public ApplicationGrpcServiceImpl(ApplicationDbContext db, ILogger<ApplicationGrpcServiceImpl> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<ApplicationProto> GetApplicationById(
        GetApplicationByIdRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid application ID"));

        var app = await _db.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (app is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Application not found"));

        _logger.LogInformation("gRPC GetApplicationById {Id} → {Company} / {Position}", id, app.CompanyName, app.PositionTitle);

        return new ApplicationProto
        {
            Id = app.Id.ToString(),
            CandidateId = app.CandidateId.ToString(),
            CvVersionId = app.CvVersionId?.ToString() ?? "",
            JobOfferId = app.JobOfferId?.ToString() ?? "",
            CompanyName = app.CompanyName,
            PositionTitle = app.PositionTitle,
            OfferSource = app.OfferSource ?? "",
            Status = app.Status.ToString(),
            AppliedAt = app.AppliedAt?.ToString("O") ?? "",
            UpdatedAt = app.UpdatedAt.ToString("O"),
            Notes = app.Notes ?? "",
            IsSaved = app.Status == ApplicationStatus.SAVED
        };
    }
}
