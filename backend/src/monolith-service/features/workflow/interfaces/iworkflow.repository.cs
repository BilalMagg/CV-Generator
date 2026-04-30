using backend.src.features.workflow.entity;

namespace backend.src.features.workflow.interfaces;

public interface IWorkflowRepository
{
    Task<List<Workflow>> GetAll();
    Task<Workflow> GetById(Guid id);
    Task<Workflow> Create(Workflow entity);
    Task<Workflow> Update(Workflow entity);
    Task Delete(Guid id);
}
