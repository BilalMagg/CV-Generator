namespace backend.src.config
{
    public static class KeycloakConfig
    {
        public static string Authority { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_AUTHORITY") ?? "";
        public static string ClientId { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_ID") ?? "";
        public static string ClientSecret { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENT_SECRET") ?? "";
        public static string ResponseType { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_RESPONSE") ?? "code";
        public static string CallbackPath { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_CALLBACK_PATH") ?? "/signin-oidc";
        public static bool RequireHttpsMetadata { get; } = Environment.GetEnvironmentVariable("KEYCLOAK_REQUIRE_HTTPS") != "false";
    }
}