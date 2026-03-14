using AutoMapper;
using backend.src.features.experience.entity;
using backend.src.features.experience.dto;

namespace backend.src.features.experience;

public class ExperienceMappingProfile : Profile
{
    public ExperienceMappingProfile()
    {
        CreateMap<Experience, ExperienceResponseDto>();
        CreateMap<CreateExperienceDto, Experience>();
        CreateMap<UpdateExperienceDto, Experience>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}
