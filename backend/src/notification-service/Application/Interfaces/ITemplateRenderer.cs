namespace NotificationService.Application.Interfaces;

public interface ITemplateRenderer
{
    Task<string> RenderAsync(string templateName, object model);
}