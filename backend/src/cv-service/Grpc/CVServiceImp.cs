using CommonProtos.CV;
using CvService.DTOs;
using CvService.Services;
using Grpc.Core;

namespace CvService.Grpc;

public class CvServieImp : CVServiceGrpc.CVServiceGrpcBase
{
  private readonly CvDbContext _db;
  private readonly ILogger<CvServiceImpl> _logger;
  
}



