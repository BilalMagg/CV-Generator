using AutoMapper;
using backend.src.features.workflow.entity;
using backend.src.features.workflow.dto;

namespace backend.src.features.workflow;

public class WorkflowMappingProfile : Profile
{
    public WorkflowMappingProfile()
    {
        CreateMap<Workflow, WorkflowResponseDto>();
        CreateMap<CreateWorkflowDto, Workflow>();
        CreateMap<UpdateWorkflowDto, Workflow>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}
