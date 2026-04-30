using Microsoft.Extensions.DependencyInjection;
using backend.src.features.workflow.interfaces;
using backend.src.features.workflow.service;
using backend.src.features.workflow.repository;

namespace backend.src.features.workflow;

public static class WorkflowModule
{
    public static IServiceCollection AddWorkflowModule(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<IWorkflowRepository, WorkflowRepository>();

        return services;
    }
}
