using Microsoft.Extensions.DependencyInjection;

public static class SkillModule
{
    public static IServiceCollection AddSkillModule(this IServiceCollection services)
    {
        services.AddScoped<ISkillService, SkillService>();
        services.AddScoped<ISkillRepository, SkillRepository>();

        return services;
    }
}
