using Microsoft.EntityFrameworkCore;
using backend.src.shared.filters;
using backend.src.middleware;
using backend.src.features.user;
using backend.src.features.auth;
using backend.src.features.project;
using backend.src.features.skill;
using backend.src.features.experience;

var builder = WebApplication.CreateBuilder(args);

// ------------------------
// 1. Configure services
// ------------------------

builder.Services.AddUserModule();
builder.Services.AddAuthModule();
builder.Services.AddProjectModule();
builder.Services.AddSkillModule();
builder.Services.AddExperienceModule();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// Add controllers
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});

// Add Swagger for API testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure EF Core with PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// If you want JWT auth later, you can configure it here
// builder.Services.AddAuthentication(...);

// Register your services / repositories here
// builder.Services.AddScoped<IUserService, UserService>();
// builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// ------------------------
// 2. Configure middleware
// ------------------------

// Enable Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Enable HTTPS
app.UseHttpsRedirection();

// Enable authentication & authorization if used
// app.UseAuthentication();
// app.UseAuthorization();
app.UseMiddleware<ExceptionMiddleware>();

// Map controllers
app.MapControllers();

// ------------------------
// 3. Run the app
// ------------------------
app.Run();
