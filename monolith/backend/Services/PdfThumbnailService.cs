namespace CV_Generator.Services;

// Renders a PDF's first page to a PNG thumbnail via the agents sidecar.
// Generation is best-effort: a failed/unavailable sidecar must not break uploads.
public interface IPdfThumbnailService
{
    Task<byte[]?> RenderFirstPageAsync(byte[] pdf, CancellationToken ct = default);
}

public class PdfThumbnailService : IPdfThumbnailService
{
    private readonly HttpClient _http;

    public PdfThumbnailService(HttpClient http) => _http = http;

    public async Task<byte[]?> RenderFirstPageAsync(byte[] pdf, CancellationToken ct = default)
    {
        try
        {
            using var content = new ByteArrayContent(pdf);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            using var response = await _http.PostAsync("thumbnail", content, ct);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception)
        {
            // e.g. sidecar down — thumbnail stays absent, upload proceeds.
            return null;
        }
    }
}