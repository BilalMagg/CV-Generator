using AutoMapper;
using backend.src.features.skill.entity;
using backend.src.features.skill.dto;

namespace backend.src.features.skill;

public class SkillMappingProfile : Profile
{
    public SkillMappingProfile()
    {
        CreateMap<Skill, SkillResponseDto>();
        CreateMap<CreateSkillDto, Skill>();
        CreateMap<UpdateSkillDto, Skill>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}
