using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using DotNetEnv;

namespace backend.src.infrastructure.database
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            Env.Load();
            // 1. Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory) // make sure it finds appsettings.json
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            // 2. Read connection string
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") 
                                    ?? configuration.GetConnectionString("DefaultConnection");

            // 3. Build DbContext options
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
