using Npgsql;

// ── Configuration ──────────────────────────────────────────────────────────────
var databases = new DatabaseConfig[]
{
    new("User DB",          "Host=localhost;Port=5433;Database=user_db;Username=postgres;Password=postgres", false),
    new("Application DB",   "Host=localhost;Port=5436;Database=application_db;Username=postgres;Password=postgres", true),
    new("CV DB",            "Host=localhost;Port=5439;Database=cv_db;Username=postgres;Password=postgres", true),
    new("Job Offer DB",     "Host=localhost;Port=5437;Database=job_offer_db;Username=postgres;Password=postgres", true),
    new("Workflow DB",      "Host=localhost;Port=5435;Database=workflow_db;Username=postgres;Password=postgres", true),
    new("Notification DB",  "Host=localhost;Port=5438;Database=notification_db;Username=postgres;Password=postgres", true),
    new("Content DB",       "Host=localhost;Port=5434;Database=content_db;Username=postgres;Password=postgres", true),
};

// Format: (database_label, table_name, column_name, nullable)
// Table names from EF Core migration snapshots (case-sensitive, must match exactly)
var affectedTables = new List<(string Label, string Table, string Column, bool Nullable)>
{
    // application-service
    ("Application DB", "applications",                "CandidateId",  false),
    ("Application DB", "application_configurations",  "UserId",       false),

    // cv-service
    ("CV DB",          "Cvs",                         "UserId",       false),

    // job-offer-service
    ("Job Offer DB",   "job_offers",                  "UserId",       false),
    ("Job Offer DB",   "user_quotas",                 "UserId",       false),

    // workflow-service
    ("Workflow DB",    "experiences",                 "UserId",       false),
    ("Workflow DB",    "projects",                    "UserId",       false),
    ("Workflow DB",    "skills",                      "UserId",       false),
    ("Workflow DB",    "AgentDocumentChunks",         "UserId",       false),
    ("Workflow DB",    "job_extractions",             "UserId",       true),

    // notification-service
    ("Notification DB","Notifications",               "UserId",       false),
    ("Notification DB","NotificationPreferences",     "UserId",       false),
    ("Notification DB","Reminders",                   "UserId",       false),

    // user-content-service
    ("Content DB",     "cv_profiles",                 "UserId",       false),
    ("Content DB",     "educations",                  "UserId",       false),
    ("Content DB",     "experiences",                 "UserId",       false),
    ("Content DB",     "skills",                      "UserId",       true),
    ("Content DB",     "projects",                    "UserId",       false),
    ("Content DB",     "social_links",                "UserId",       false),
    ("Content DB",     "languages",                   "UserId",       false),
    ("Content DB",     "interests",                   "UserId",       false),
    ("Content DB",     "hackathons",                  "UserId",       false),
    ("Content DB",     "certifications",              "UserId",       false),
    ("Content DB",     "academic_activities",         "UserId",       false),
};

var dryRun = args.Contains("--dry-run");
var verbose = args.Contains("--verbose");

Console.WriteLine($"=== UserId -> KeycloakId Migration Tool ===");
Console.WriteLine($"Mode: {(dryRun ? "DRY RUN (no changes)" : "LIVE")}");
Console.WriteLine();

// ── Step 1: Load user mapping from user_db ─────────────────────────────────────
Console.WriteLine("Step 1: Loading KeycloakId -> Id mapping from user_db...");

var userDb = databases.First(d => d.Label == "User DB");
var userMapping = new List<(Guid KeycloakId, Guid InternalId)>();

await using (var conn = new NpgsqlConnection(userDb.ConnectionString))
{
    await conn.OpenAsync();
    await using var cmd = new NpgsqlCommand(
        """SELECT "KeycloakId", "Id" FROM "users" WHERE "KeycloakId" IS NOT NULL""", conn);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var keycloakId = Guid.Parse(reader.GetString(0));
        var internalId = reader.GetGuid(1);
        userMapping.Add((keycloakId, internalId));
    }
}

Console.WriteLine($"  Found {userMapping.Count} user(s) to map.");
Console.WriteLine();

if (userMapping.Count == 0)
{
    Console.WriteLine("No users found in user_db. Nothing to migrate.");
    return;
}

if (verbose)
{
    foreach (var (keycloakId, internalId) in userMapping)
        Console.WriteLine($"  KeycloakId={keycloakId} -> Id={internalId}");
}

// ── Step 2: Migrate each affected table ────────────────────────────────────────
Console.WriteLine("Step 2: Migrating affected tables...");

var totalUpdated = 0;
var totalErrors = 0;
var totalSkipped = 0;

foreach (var (dbLabel, table, column, nullable) in affectedTables)
{
    var dbConfig = databases.FirstOrDefault(d => d.Label == dbLabel);
    if (dbConfig == null || !dbConfig.IsTarget) continue;

    Console.Write($"  {dbLabel}.{table}.{column}... ");

    try
    {
        await using var conn = new NpgsqlConnection(dbConfig.ConnectionString);
        await conn.OpenAsync();

        // Check if table exists (PostgreSQL folds unquoted names to lowercase)
        await using var checkCmd = new NpgsqlCommand(
            "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = @t)", conn);
        checkCmd.Parameters.AddWithValue("t", table);
        var exists = (bool)(await checkCmd.ExecuteScalarAsync() ?? false);
        if (!exists)
        {
            Console.WriteLine("table not found, skipped");
            totalSkipped++;
            continue;
        }

        // Check if column exists
        await using var checkColCmd = new NpgsqlCommand(
            "SELECT EXISTS (SELECT FROM information_schema.columns WHERE table_name = @t AND column_name = @c)", conn);
        checkColCmd.Parameters.AddWithValue("t", table);
        checkColCmd.Parameters.AddWithValue("c", column);
        var colExists = (bool)(await checkColCmd.ExecuteScalarAsync() ?? false);
        if (!colExists)
        {
            Console.WriteLine("column not found, skipped");
            totalSkipped++;
            continue;
        }

        // Build VALUES clause for the mapping
        var valueClauses = new List<string>();
        for (var i = 0; i < userMapping.Count; i++)
            valueClauses.Add($"(@kc{i}::uuid, @id{i}::uuid)");
        var valuesStr = string.Join(", ", valueClauses);

        // Count rows needing migration
        var nullGuard = nullable ? $""" "{column}" IS NOT NULL AND """ : "";
        var countSql = $"""
            SELECT COUNT(*) FROM "{table}" t
            WHERE {nullGuard}EXISTS (
                SELECT 1 FROM (VALUES {valuesStr}) AS u(kc, id)
                WHERE t."{column}" = u.kc AND t."{column}" != u.id
            )
            """;

        await using var countCmd = new NpgsqlCommand(countSql, conn);
        for (var i = 0; i < userMapping.Count; i++)
        {
            countCmd.Parameters.AddWithValue($"kc{i}", userMapping[i].KeycloakId);
            countCmd.Parameters.AddWithValue($"id{i}", userMapping[i].InternalId);
        }
        var rowCount = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);

        if (rowCount == 0)
        {
            Console.WriteLine("0 rows to update");
            continue;
        }

        if (dryRun)
        {
            Console.WriteLine($"{rowCount} row(s) WOULD be updated (dry run)");
            totalUpdated += (int)rowCount;
            continue;
        }

        // Perform the update
        var updateSql = $"""
            UPDATE "{table}" t SET "{column}" = u.id
            FROM (VALUES {valuesStr}) AS u(kc, id)
            WHERE t."{column}" = u.kc AND t."{column}" != u.id
            """;

        await using var updateCmd = new NpgsqlCommand(updateSql, conn);
        for (var i = 0; i < userMapping.Count; i++)
        {
            updateCmd.Parameters.AddWithValue($"kc{i}", userMapping[i].KeycloakId);
            updateCmd.Parameters.AddWithValue($"id{i}", userMapping[i].InternalId);
        }

        var updated = await updateCmd.ExecuteNonQueryAsync();
        totalUpdated += updated;
        Console.WriteLine($"{updated} row(s) updated");
    }
    catch (Exception ex)
    {
        totalErrors++;
        Console.WriteLine($"ERROR: {ex.Message}");
    }
}

// ── Summary ────────────────────────────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine($"=== Migration Summary ===");
Console.WriteLine($"  Rows to update (or updated): {totalUpdated}");
Console.WriteLine($"  Skipped (table/col not found): {totalSkipped}");
Console.WriteLine($"  Errors:                       {totalErrors}");

if (dryRun)
    Console.WriteLine("  This was a DRY RUN - no changes were persisted.");
if (totalErrors > 0)
    Console.WriteLine("  Some errors occurred. Check logs above.");

Console.WriteLine("Done.");

record DatabaseConfig(string Label, string ConnectionString, bool IsTarget);
