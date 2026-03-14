using Microsoft.Extensions.DependencyInjection;
using backend.src.features.user.interfaces;
using backend.src.features.user.service;
using backend.src.features.user.repository;

public static class UserModule
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
