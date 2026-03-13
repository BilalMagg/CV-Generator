using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/experience")]
public class ExperienceController : ControllerBase
{
    private readonly IExperienceService _service;

    public ExperienceController(IExperienceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAll();
        return Ok(result);
    }
}
