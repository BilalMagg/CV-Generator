using Microsoft.Extensions.DependencyInjection;
using backend.src.features.skill.interfaces;
using backend.src.features.skill.service;
using backend.src.features.skill.repository;

namespace backend.src.features.skill;

public static class SkillModule
{
    public static IServiceCollection AddSkillModule(this IServiceCollection services)
    {
        services.AddScoped<ISkillService, SkillService>();
        services.AddScoped<ISkillRepository, SkillRepository>();

        return services;
    }
}
