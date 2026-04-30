cat <<EOF > "$FEATURE_PATH/$FEATURE.module.cs"
using Microsoft.Extensions.DependencyInjection;
using $BASE_NAMESPACE.interfaces;
using $BASE_NAMESPACE.service;
using $BASE_NAMESPACE.repository;

namespace $BASE_NAMESPACE;

public static class ${FEATURE_CAP}Module
{
    public static IServiceCollection Add${FEATURE_CAP}Module(this IServiceCollection services)
    {
        services.AddScoped<I${FEATURE_CAP}Service, ${FEATURE_CAP}Service>();
        services.AddScoped<I${FEATURE_CAP}Repository, ${FEATURE_CAP}Repository>();

        return services;
    }
}
EOF
