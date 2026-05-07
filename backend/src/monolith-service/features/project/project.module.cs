using Microsoft.Extensions.DependencyInjection;
using backend.src.features.project.interfaces;
using backend.src.features.project.service;
using backend.src.features.project.repository;

namespace backend.src.features.project;

public static class ProjectModule
{
    public static IServiceCollection AddProjectModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectRepository, ProjectRepository>();

        return services;
    }
}
