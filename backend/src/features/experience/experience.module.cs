using Microsoft.Extensions.DependencyInjection;

public static class ExperienceModule
{
    public static IServiceCollection AddExperienceModule(this IServiceCollection services)
    {
        services.AddScoped<IExperienceService, ExperienceService>();
        services.AddScoped<IExperienceRepository, ExperienceRepository>();

        return services;
    }
}
