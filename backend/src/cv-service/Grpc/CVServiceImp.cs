using CommonProtos.CV;
using Grpc.Core;
using CvService.DTOs;
using CvService.Services;
using Microsoft.EntityFrameworkCore;
using CvService.Entities;

namespace CvService.Grpc;

public class CvServieImp : CVServiceGrpc.CVServiceGrpcBase
{
  private readonly CvDbContext _db;
  private readonly ILogger<CvServiceImp> _logger;
  public CvServieImp(CvDbContext db, ILogger<CvServiceImp> logger)
  {
    _db = db;
    _logger = logger;
  }

  public override async Task<CVProto> GetCVById(GetCVByIdRequest request, ServerCallContext context)
  {
    var cv = await _db.Cvs.FindAsync(Guid.Parse(request.Id));
    if(cv == null)
    {
      throw new RpcException(new Status(StatusCode.NotFound, "CV not found"));
    }
    return ToProto(cv);
   }
   public override async Task<List<CVProto>> GetCVsByUserId(GetCVsByUserIdRequest request, ServerCallContext context)
  {
    var cvs = await _db.Cvs
                  .Where(target => target.UserId == request.userId)
                  .ToListAsync();
    
    if (cvs == null)
    {
      throw new RpcException(new Status(StatusCode.NotFound, $"No CV found for this user with id: {request.userId}"));
    }
    return TOProto(cvs);
  }
  public override async Task<List<CVProto>> GetCvsByTemplateId(GetCVsByTemplateIdRequest request, ServerCallContext context)
  {
    var cvs = await _db.Cvs
                  .Where(target => target.TemplateId ==request.TemplateId)
                  .ToListAsync();
    if (cvs == null)
    {
      throw new RpcException(new Status(StatusCode.NotFound, $"No CV found for this template with id: {request.TemplateId}"));
    }
    return ToProto(cvs);
  }
  public override async Task<CVProto> CreateCV(CreateCVRequest request, ServerCallContext context)
  {
    var cv = new Cv(
      Title = request.Title,
      TemplateId = request.TemplateId,
      UserId =  request.UserId,
      Projects = request.Projects,
      Skills = request.Skills,
      Experiences = request.Experiences
    );
    _db.Cvs.Add(cv);
    await _db.SaveChangesAsync();

    _logger.LogInformation($"Created CV with id: {cv.Id} via gRPC");
    return ToProto(cv);
  }
  public override async Task<CVProto> UpdateCV(UpdateCVRequest request, ServerCallContext context)
  {
    var cv = await _db.Cvs.FindAsync(target => target.Id ==request.Id);
    if (cv == null)
    {
      throw new RpcException(new Status(StatusCode.NotFound, $"CV with id {request.Id} is not found"));
    }
    cv.Title = request.Title;
    cv.TemplateId =request.TemplateId;
    cv.userId = request.UserId;
    cv.Projects = request.Projects;
    cv.Skills = request.Skills;
    cv.Experiences =request.Experiences;
    
    await _db.SaveChangesAsync();
    return ToProto(cv);
  }
  public override async Task<CVProto> DeleteCV(DeleteCVRequest request, ServerCallContext context)
  {
    var cv = await _db.Cvs.FindAsync(target => target.Id ==request.Id);
    if (cv == null)
    {
      throw new RpcException(new Status(StatusCode.NotFound, $"CV with id {request.Id} is not found"));
    }
    _db.Cvs.Remove(cv);
    await _db.SaveChangesAsync();
    return new DeleteCVResponse(cv);
  }

  public static CVProto ToProto(Cv cv)=>new(

    
  );
  
}



