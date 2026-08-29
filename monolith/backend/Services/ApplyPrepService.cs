using CV_Generator.Data;
using CV_Generator.Dto;
using CV_Generator.Services.AgentClients;

namespace CV_Generator.Services;

public interface IApplyPrepService
{
    Task<ApplyPrepFormResult> GenerateFormResponsesAsync(Guid userId, ApplyPrepFormRequest dto, CancellationToken cancellationToken = default);
    Task<ApplyPrepMessageResult> GenerateMessageAsync(Guid userId, ApplyPrepMessageRequest dto, CancellationToken cancellationToken = default);
}

public class ApplyPrepService : IApplyPrepService
{
    private readonly IApplyPrepClient _client;
    private readonly ILlmSettingsService _llmSettings;
    private readonly ApplyService _apply;
    private readonly ILogger<ApplyPrepService> _logger;

    public ApplyPrepService(
        IApplyPrepClient client,
        ILlmSettingsService llmSettings,
        ApplyService apply,
        ILogger<ApplyPrepService> logger)
    {
        _client = client;
        _llmSettings = llmSettings;
        _apply = apply;
        _logger = logger;
    }

    public async Task<ApplyPrepFormResult> GenerateFormResponsesAsync(Guid userId, ApplyPrepFormRequest dto, CancellationToken cancellationToken = default)
    {
        if (dto.Fields.Count == 0) throw new ArgumentException("At least one form field is required");
        if (string.IsNullOrWhiteSpace(dto.CompanyName)) throw new ArgumentException("CompanyName is required");

        await ApplyResolveLlmAsync(dto, userId, cancellationToken);

        var result = await _client.GenerateFormResponsesAsync(dto, cancellationToken)
            ?? new ApplyPrepFormResult();

        if (dto.SaveTracked && result.Responses.Count > 0)
        {
            result.ApplicationId = await TrackAsync(userId, dto);
        }

        return result;
    }

    public async Task<ApplyPrepMessageResult> GenerateMessageAsync(Guid userId, ApplyPrepMessageRequest dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CompanyName)) throw new ArgumentException("CompanyName is required");

        await ApplyResolveLlmAsync(dto, userId, cancellationToken);

        var result = await _client.GenerateMessageAsync(dto, cancellationToken)
            ?? new ApplyPrepMessageResult();

        if (dto.SaveTracked && !string.IsNullOrWhiteSpace(result.Message))
        {
            result.ApplicationId = await TrackAsync(userId, dto);
        }

        return result;
    }

    private async Task ApplyResolveLlmAsync(object dto, Guid userId, CancellationToken cancellationToken)
    {
        var settings = await _llmSettings.GetAsync(userId);
        if (dto is ApplyPrepFormRequest form)
        {
            form.UserId = userId.ToString();
            form.JobRole = form.PositionTitle;
            form.Provider = settings?.Provider;
            form.Model = settings?.Model;
        }
        else if (dto is ApplyPrepMessageRequest msg)
        {
            msg.UserId = userId.ToString();
            msg.JobRole = msg.PositionTitle;
            msg.Provider = settings?.Provider;
            msg.Model = settings?.Model;
        }
    }

    private async Task<Guid> TrackAsync(Guid userId, object dto)
    {
        var tracked = dto switch
        {
            ApplyPrepFormRequest f => new ApplyPrepTrackedRequest
            {
                CompanyName = f.CompanyName,
                PositionTitle = f.PositionTitle,
                CompanyDescription = f.CompanyDescription,
                RecipientName = f.RecipientName,
                RecipientEmail = f.RecipientEmail,
                ContactNotes = f.ContactNotes,
                CvVersionId = f.CvVersionId,
            },
            ApplyPrepMessageRequest m => new ApplyPrepTrackedRequest
            {
                CompanyName = m.CompanyName,
                PositionTitle = m.PositionTitle,
                CompanyDescription = m.CompanyDescription,
                RecipientName = m.RecipientName,
                RecipientEmail = m.RecipientEmail,
                ContactNotes = m.ContactNotes,
                CvVersionId = m.CvVersionId,
            },
            _ => throw new ArgumentException("Unsupported prep request"),
        };

        try
        {
            return await _apply.CreateTrackedApplicationAsync(userId, tracked);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "apply-prep: failed to create tracked application (content still returned)");
            return Guid.Empty;
        }
    }
}
