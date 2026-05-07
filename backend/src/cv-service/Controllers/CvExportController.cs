using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CvService.Controllers;

[ApiController]
[Route("api/cv/{cvId}/export")]
[Authorize]
public class CvExportController : ControllerBase
{
    /// POST /api/cv/{cvId}/export/pdf
    [HttpPost("pdf")]
    public async Task<IActionResult> ExportPdf(Guid cvId, [FromQuery] Guid? versionId)
    {
        // TODO: Generate PDF from CV data
        return Ok(new { message = "PDF export endpoint - implementation pending" });
    }

    /// POST /api/cv/{cvId}/export/docx
    [HttpPost("docx")]
    public async Task<IActionResult> ExportDocx(Guid cvId, [FromQuery] Guid? versionId)
    {
        // TODO: Generate DOCX from CV data
        return Ok(new { message = "DOCX export endpoint - implementation pending" });
    }
}
