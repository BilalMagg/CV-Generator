using Microsoft.EntityFrameworkCore;
using Npgsql;
using UserContentService;
using UserContentService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ContentDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));

});

builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddGrpc();
builder.Services.AddSingleton<KafkaProducerService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy.SetIsOriginAllowed(_ => true)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials());
});

var app = builder.Build();

app.UseCors("AllowAll");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var pending = await dbContext.Database.GetPendingMigrationsAsync();
        if (pending.Any())
        {
            logger.LogInformation("Applying {Count} migrations...", pending.Count());
            await dbContext.Database.MigrateAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Migration failed");
    }
}

app.UseRouting();

// Middleware to handle X-User-Id from Gateway
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-User-Id", out var userId))
    {
        // Store it in Items for easy access in controllers
        context.Items["UserId"] = userId.ToString();
    }
    await next();
});

app.UseCors("AllowAll");
app.MapControllers();
app.MapGrpcService<UserContentService.Grpc.ContentServiceImpl>();

app.UseSwagger();
app.UseSwaggerUI();

app.Run();