using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// 1. Add CORS so the frontend can talk to the Gateway
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200") 
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("Proxy"));

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = Environment.GetEnvironmentVariable("KEYCLOAK_AUTHORITY") ?? "";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = false, // Set to false to avoid docker/localhost issuer mismatches
            ValidateAudience = false,
            ValidateLifetime = true,
        };
    });

// 2. Define the "default" authorization policy that we reference in appsettings.json
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("default", policy => policy.RequireAuthenticatedUser());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 3. Use CORS middleware (Must be before Auth)
app.UseCors("AllowFrontend");

app.UseSwagger();
app.UseSwaggerUI();

// 4. Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// 5. Finally, map the YARP reverse proxy
app.MapReverseProxy();

app.Run();
