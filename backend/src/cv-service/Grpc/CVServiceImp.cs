using System.Text.Json;
using CommonProtos.CV;
using CvService.Entities;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace CvService.Grpc;

public class CvServiceImp : CVServiceGrpc.CVServiceGrpcBase
{
    private readonly CvDbContext _db;
    private readonly ILogger<CvServiceImp> _logger;

    public CvServiceImp(CvDbContext db, ILogger<CvServiceImp> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<CVProto> GetCVById(GetCVByIdRequest request, ServerCallContext context)
    {
        var cv = await _db.Cvs.FindAsync(Guid.Parse(request.Id));
        if (cv == null)
            throw new RpcException(new Status(StatusCode.NotFound, "CV not found"));

        return ToProto(cv);
    }

    public override async Task GetCVsByUserId(GetCVsByUserIdRequest request, IServerStreamWriter<CVProto> responseStream, ServerCallContext context)
    {
        var cvs = await _db.Cvs
            .Where(c => c.UserId == Guid.Parse(request.UserId))
            .ToListAsync();

        foreach (var cv in cvs)
            await responseStream.WriteAsync(ToProto(cv));
    }

    public override async Task GetCVsByTemplateID(GetCVsByTemplateIdRequest request, IServerStreamWriter<CVProto> responseStream, ServerCallContext context)
    {
        var cvs = await _db.Cvs
            .Where(c => c.TemplateId == request.Id)
            .ToListAsync();

        foreach (var cv in cvs)
            await responseStream.WriteAsync(ToProto(cv));
    }

    public override async Task<CVProto> CreateCV(CreateCVRequest request, ServerCallContext context)
    {
        var cv = new Cv
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            TemplateId = request.TemplateId,
            UserId = Guid.Parse(request.UserId),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var initialVersion = new CvVersion
        {
            Id = Guid.NewGuid(),
            CvId = cv.Id,
            VersionNumber = 1,
            Label = "v1",
            CreatedAt = DateTime.UtcNow,
            ContentJson = JsonSerializer.Serialize(new
            {
                projects = request.Projects,
                skills = request.Skills,
                experiences = request.Experiences
            })
        };

        _db.Cvs.Add(cv);
        _db.CvVersions.Add(initialVersion);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created CV {Id} via gRPC", cv.Id);
        return ToProto(cv);
    }

    public override async Task<CVProto> UpdateCV(UpdateCVRequest request, ServerCallContext context)
    {
        var cv = await _db.Cvs.FindAsync(Guid.Parse(request.Id));
        if (cv == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"CV with id {request.Id} not found"));

        cv.Title = request.Title;
        cv.TemplateId = request.TemplateId;
        cv.UserId = Guid.Parse(request.UserId);
        cv.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return ToProto(cv);
    }

    public override async Task<DeleteCVResponse> DeleteCV(DeleteCVRequest request, ServerCallContext context)
    {
        var cv = await _db.Cvs.FindAsync(Guid.Parse(request.Id));
        if (cv == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"CV with id {request.Id} not found"));

        _db.Cvs.Remove(cv);
        await _db.SaveChangesAsync();
        return new DeleteCVResponse { Success = true };
    }

    private static CVProto ToProto(Cv cv) => new()
    {
        Id = cv.Id.ToString(),
        Title = cv.Title,
        TemplateId = cv.TemplateId,
        UserId = cv.UserId.ToString(),
        CreatedAt = cv.CreatedAt.ToString("O"),
        ModifiedAt = cv.UpdatedAt.ToString("O")
    };
}
