cat <<EOF > "$FEATURE_PATH/$FEATURE.mapper.cs"
using AutoMapper;
using $BASE_NAMESPACE.entity;
using $BASE_NAMESPACE.dto;

namespace $BASE_NAMESPACE;

public class ${FEATURE_CAP}MappingProfile : Profile
{
    public ${FEATURE_CAP}MappingProfile()
    {
        CreateMap<${FEATURE_CAP}, ${FEATURE_CAP}ResponseDto>();
        CreateMap<Create${FEATURE_CAP}Dto, ${FEATURE_CAP}>();
        CreateMap<Update${FEATURE_CAP}Dto, ${FEATURE_CAP}>()
            .ForAllMembers(opt => opt.Condition(
                (src, dest, srcMember) => srcMember != null
            ));
    }
}
EOF
