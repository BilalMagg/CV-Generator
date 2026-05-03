using Grpc.Core;
using Grpc.Net.Client;
using CommonProtos.Workflow;
using Microsoft.EntityFrameworkCore;
using WorkflowService;
using WorkflowService.Entity;

namespace WorkflowService.Grpc;

public class WorkflowServiceImpl : CommonProtos.Workflow.WorkflowServiceGrpc.WorkflowServiceGrpcBase
{
    private readonly WorkflowDbContext _db;
    private readonly ILogger<WorkflowServiceImpl> _logger;

    public WorkflowServiceImpl(WorkflowDbContext db, ILogger<WorkflowServiceImpl> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<WorkflowProto> GetWorkflowById(GetWorkflowByIdRequest request, ServerCallContext context)
    {
        var workflow = await _db.Workflows.FindAsync(Guid.Parse(request.Id));
        if (workflow == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Workflow not found"));

        return ToProto(workflow);
    }

    public override async Task GetAllWorkflows(GetAllWorkflowsRequest request, IServerStreamWriter<WorkflowProto> responseStream, ServerCallContext context)
    {
        var workflows = await _db.Workflows.ToListAsync();
        foreach (var workflow in workflows)
        {
            await responseStream.WriteAsync(ToProto(workflow));
        }
    }

    public override async Task<WorkflowProto> CreateWorkflow(CreateWorkflowRequest request, ServerCallContext context)
    {
        var workflow = new Workflow
        {
            Name = request.Name,
            Description = request.Description,
            DefinitionJson = request.DefinitionJson,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Workflows.Add(workflow);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created workflow {Id}", workflow.Id);
        return ToProto(workflow);
    }

    public override async Task<WorkflowProto> UpdateWorkflow(UpdateWorkflowRequest request, ServerCallContext context)
    {
        var workflow = await _db.Workflows.FindAsync(Guid.Parse(request.Id));
        if (workflow == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Workflow not found"));

        workflow.Name = request.Name;
        workflow.Description = request.Description;
        workflow.DefinitionJson = request.DefinitionJson;
        workflow.IsActive = request.IsActive;

        await _db.SaveChangesAsync();
        return ToProto(workflow);
    }

    public override async Task<DeleteWorkflowResponse> DeleteWorkflow(DeleteWorkflowRequest request, ServerCallContext context)
    {
        var workflow = await _db.Workflows.FindAsync(Guid.Parse(request.Id));
        if (workflow == null)
            throw new RpcException(new Status(StatusCode.NotFound, "Workflow not found"));

        _db.Workflows.Remove(workflow);
        await _db.SaveChangesAsync();

        return new DeleteWorkflowResponse { Success = true };
    }

    private static WorkflowProto ToProto(Workflow w) => new()
    {
        Id = w.Id.ToString(),
        Name = w.Name ?? "",
        Description = w.Description ?? "",
        DefinitionJson = w.DefinitionJson ?? "",
        IsActive = w.IsActive,
        CreatedAt = w.CreatedAt.ToString("O")
    };
}