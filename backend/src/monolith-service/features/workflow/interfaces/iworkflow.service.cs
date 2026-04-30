using backend.src.features.workflow.dto;

namespace backend.src.features.workflow.interfaces;

public interface IWorkflowService
{
    Task<List<WorkflowResponseDto>> GetAll();
    Task<WorkflowResponseDto> GetById(Guid id);
    Task<WorkflowResponseDto> Create(CreateWorkflowDto dto);
    Task<WorkflowResponseDto> Update(Guid id, UpdateWorkflowDto dto);
    Task Delete(Guid id);
}
