namespace backend.src.config;

public static class KeycloakAdminConfig
{
    public static string AdminUrl { get; } =
        Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_URL") ?? "http://keycloak:8080";

    public static string AdminUsername { get; } =
        Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_USERNAME") ?? "admin";

    public static string AdminPassword { get; } =
        Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_PASSWORD") ?? "";

    public static string AdminClientId { get; } =
        Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_CLIENT_ID") ?? "admin-cli";

    public static string Realm => KeycloakConfig.Realm;

    public static string TokenUrl =>
        $"{AdminUrl.TrimEnd('/')}/realms/{Realm}/protocol/openid-connect/token";

    public static string UsersUrl =>
        $"{AdminUrl.TrimEnd('/')}/admin/realms/{Realm}/users";
}
