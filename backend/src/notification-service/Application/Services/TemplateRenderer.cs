using NotificationService.Application.Interfaces;
using Scriban;

namespace NotificationService.Application.Services;

public class TemplateRenderer : ITemplateRenderer
{
    private readonly string _templatesPath;

    public TemplateRenderer()
    {
        _templatesPath = Path.Combine(AppContext.BaseDirectory, "Templates");
    }

    public async Task<string> RenderAsync(string templateName, object model)
    {
        var filePath = Path.Combine(_templatesPath, $"{templateName}.html");

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Template '{templateName}' not found at {filePath}");

        var templateContent = await File.ReadAllTextAsync(filePath);
        var template = Template.Parse(templateContent);
        return await template.RenderAsync(model);
    }
}