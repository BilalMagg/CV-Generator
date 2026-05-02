using Microsoft.Extensions.DependencyInjection;
using backend.src.features.experience.interfaces;
using backend.src.features.experience.service;
using backend.src.features.experience.repository;

namespace backend.src.features.experience;

public static class ExperienceModule
{
    public static IServiceCollection AddExperienceModule(this IServiceCollection services)
    {
        services.AddScoped<IExperienceService, ExperienceService>();
        services.AddScoped<IExperienceRepository, ExperienceRepository>();

        return services;
    }
}
