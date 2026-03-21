using backend.src.features.workflow.interfaces;
using backend.src.features.workflow.entity;
using backend.src.features.workflow.dto;
using AutoMapper;

namespace backend.src.features.workflow.service;

public class WorkflowService : IWorkflowService
{
    private readonly IWorkflowRepository _repository;
    private readonly IMapper _mapper;

    public WorkflowService(IWorkflowRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<WorkflowResponseDto>> GetAll()
    {
        var entities = await _repository.GetAll();
        return _mapper.Map<List<WorkflowResponseDto>>(entities);
    }

    public async Task<WorkflowResponseDto> GetById(Guid id)
    {
        var entity = await _repository.GetById(id);
        return _mapper.Map<WorkflowResponseDto>(entity);
    }

    public async Task<WorkflowResponseDto> Create(CreateWorkflowDto dto)
    {
        var entity = _mapper.Map<Workflow>(dto);
        var created = await _repository.Create(entity);
        return _mapper.Map<WorkflowResponseDto>(created);
    }

    public async Task<WorkflowResponseDto> Update(Guid id, UpdateWorkflowDto dto)
    {
        var entity = await _repository.GetById(id);
        _mapper.Map(dto, entity);
        var updated = await _repository.Update(entity);
        return _mapper.Map<WorkflowResponseDto>(updated);
    }

    public async Task Delete(Guid id)
    {
        await _repository.Delete(id);
    }
}
