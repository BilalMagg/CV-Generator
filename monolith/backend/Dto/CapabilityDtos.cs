namespace CV_Generator.Dto;

/// <summary>What the agent sidecar can do in this deployment — drives the frontend's
/// graceful degradation (e.g. no LaTeX engine → CV .tex is generated but PDF compilation
/// is disabled, with a compile-on-Overleaf hint).</summary>
public class SystemCapabilitiesDto
{
    public bool LatexEngineAvailable { get; set; }
    /// <summary>"native" (pdflatex on PATH) or "docker-texlive" (texlive/texlive image) when available.</summary>
    public string? LatexEngine { get; set; }
    public string? LatexEngineReason { get; set; }
    /// <summary>fpdf2-backed text→PDF (cover letters) — pure Python, survives containers without texlive.</summary>
    public bool TextPdfAvailable { get; set; }
    public bool SidecarReachable { get; set; }
}
