cat <<EOF > "$FEATURE_PATH/interfaces/i$FEATURE.repository.cs"
using $BASE_NAMESPACE.entity;

namespace $BASE_NAMESPACE.interfaces;

public interface I${FEATURE_CAP}Repository
{
    Task<List<${FEATURE_CAP}>> GetAll();
    Task<${FEATURE_CAP}> GetById(Guid id);
    Task<${FEATURE_CAP}> Create(${FEATURE_CAP} entity);
    Task<${FEATURE_CAP}> Update(${FEATURE_CAP} entity);
    Task Delete(Guid id);
}
EOF

cat <<EOF > "$FEATURE_PATH/interfaces/i$FEATURE.service.cs"
using $BASE_NAMESPACE.dto;

namespace $BASE_NAMESPACE.interfaces;

public interface I${FEATURE_CAP}Service
{
    Task<List<${FEATURE_CAP}ResponseDto>> GetAll();
    Task<${FEATURE_CAP}ResponseDto> GetById(Guid id);
    Task<${FEATURE_CAP}ResponseDto> Create(Create${FEATURE_CAP}Dto dto);
    Task<${FEATURE_CAP}ResponseDto> Update(Guid id, Update${FEATURE_CAP}Dto dto);
    Task Delete(Guid id);
}
EOF
