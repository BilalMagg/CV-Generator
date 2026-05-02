using Microsoft.EntityFrameworkCore;
using WorkflowService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WorkflowDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddGrpc();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
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

app.UseSwagger();
app.UseSwaggerUI();
app.UseRouting();
app.MapControllers();
app.MapGrpcService<GrpcServices.WorkflowServiceImpl>();

app.Run();