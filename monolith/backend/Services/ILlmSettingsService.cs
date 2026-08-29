using CV_Generator.Dto;

namespace CV_Generator.Services;

public interface ILlmSettingsService
{
    Task<LlmSettingsDto?> GetAsync(Guid? userId);
    Task<LlmSettingsDto> SetAsync(Guid? userId, string provider, string model);
}
