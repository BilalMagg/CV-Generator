cat <<EOF > "$FEATURE_PATH/controller/$FEATURE.controller.cs"
using Microsoft.AspNetCore.Mvc;
using $BASE_NAMESPACE.interfaces;
using $BASE_NAMESPACE.dto;
using backend.src.shared.responses;

namespace $BASE_NAMESPACE.controller;

[ApiController]
[Route("api/${FEATURE}s")]
public class ${FEATURE_CAP}Controller : ControllerBase
{
    private readonly I${FEATURE_CAP}Service _service;

    public ${FEATURE_CAP}Controller(I${FEATURE_CAP}Service service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAll();
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _service.GetById(id);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Create${FEATURE_CAP}Dto dto)
    {
        var result = await _service.Create(dto);
        return Ok(ApiResponse<object>.SuccessResponse(result, "${FEATURE_CAP} created"));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Update${FEATURE_CAP}Dto dto)
    {
        var result = await _service.Update(id, dto);
        return Ok(ApiResponse<object>.SuccessResponse(result));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.Delete(id);
        return Ok(ApiResponse<object>.SuccessResponse(null, "${FEATURE_CAP} deleted"));
    }
}
EOF
