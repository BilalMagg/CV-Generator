cat <<EOF > "$FEATURE_PATH/service/$FEATURE.service.cs"
using $BASE_NAMESPACE.interfaces;
using $BASE_NAMESPACE.entity;
using $BASE_NAMESPACE.dto;
using AutoMapper;

namespace $BASE_NAMESPACE.service;

public class ${FEATURE_CAP}Service : I${FEATURE_CAP}Service
{
    private readonly I${FEATURE_CAP}Repository _repository;
    private readonly IMapper _mapper;

    public ${FEATURE_CAP}Service(I${FEATURE_CAP}Repository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<List<${FEATURE_CAP}ResponseDto>> GetAll()
    {
        var entities = await _repository.GetAll();
        return _mapper.Map<List<${FEATURE_CAP}ResponseDto>>(entities);
    }

    public async Task<${FEATURE_CAP}ResponseDto> GetById(Guid id)
    {
        var entity = await _repository.GetById(id);
        return _mapper.Map<${FEATURE_CAP}ResponseDto>(entity);
    }

    public async Task<${FEATURE_CAP}ResponseDto> Create(Create${FEATURE_CAP}Dto dto)
    {
        var entity = _mapper.Map<${FEATURE_CAP}>(dto);
        var created = await _repository.Create(entity);
        return _mapper.Map<${FEATURE_CAP}ResponseDto>(created);
    }

    public async Task<${FEATURE_CAP}ResponseDto> Update(Guid id, Update${FEATURE_CAP}Dto dto)
    {
        var entity = await _repository.GetById(id);
        _mapper.Map(dto, entity);
        var updated = await _repository.Update(entity);
        return _mapper.Map<${FEATURE_CAP}ResponseDto>(updated);
    }

    public async Task Delete(Guid id)
    {
        await _repository.Delete(id);
    }
}
EOF
