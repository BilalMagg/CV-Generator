using Microsoft.Extensions.DependencyInjection;
using backend.src.features.auth.interfaces;
using backend.src.features.auth.repository;
using backend.src.features.auth.services;

namespace backend.src.features.auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient<KeycloakAdminService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuthRepository, AuthRepository>();

        return services;
    }
}
