using AutoMapper;
using backend.src.features.project.entity;
using backend.src.features.project.dto;

namespace backend.src.features.project;

public class ProjectMappingProfile : Profile
{
    public ProjectMappingProfile()
    {
        CreateMap<Project, ProjectResponseDto>();
        CreateMap<CreateProjectDto, Project>();
        CreateMap<UpdateProjectDto, Project>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}
