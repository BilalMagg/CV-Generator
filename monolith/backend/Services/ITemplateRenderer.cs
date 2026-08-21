namespace CV_Generator.Services;

public interface ITemplateRenderer
{
    Task<string> RenderAsync(string templateName, object model);
}
