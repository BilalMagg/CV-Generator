using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace ApplicationService.Tests.Fixtures;

public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("testdb")
        .WithUsername("test")
        .WithPassword("test1234")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await CreateDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    private async Task CreateDatabaseAsync()
    {
        await using var conn = new NpgsqlConnection(_container.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS "Applications" (
                "Id" UUID PRIMARY KEY,
                "CandidateId" UUID NOT NULL,
                "CvVersionId" UUID,
                "JobOfferId" UUID,
                "CompanyName" TEXT NOT NULL,
                "PositionTitle" TEXT NOT NULL,
                "OfferSource" TEXT,
                "Status" TEXT NOT NULL DEFAULT 'PENDING',
                "AppliedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                "Notes" TEXT
            );

            CREATE TABLE IF NOT EXISTS "ApplicationStatusHistory" (
                "Id" UUID PRIMARY KEY,
                "ApplicationId" UUID NOT NULL REFERENCES "Applications"("Id"),
                "OldStatus" TEXT,
                "NewStatus" TEXT NOT NULL,
                "ChangedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                "ChangedBy" TEXT,
                "Comment" TEXT
            );
            """;
        await cmd.ExecuteNonQueryAsync();
    }
}
