using backend.src.features.user.entity;
using backend.src.features.auth.entity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ------------------------
// 1. Configure services
// ------------------------

builder.Services.AddUserModule();

// Add controllers
builder.Services.AddControllers();

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

// Map controllers
app.MapControllers();

// ------------------------
// 3. Run the app
// ------------------------
app.Run();
