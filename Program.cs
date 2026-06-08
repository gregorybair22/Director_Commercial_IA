using DirectorComercialIA.Data;
using DirectorComercialIA.Repositories;
using DirectorComercialIA.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=director_comercial.db";

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<SyncLogService>();
builder.Services.AddSingleton<ProjectSelectionService>();
builder.Services.AddHttpClient<AsanaService>();
builder.Services.AddScoped<LocalStatusInferenceService>();
builder.Services.AddScoped<CountryLanguageInferenceService>();
builder.Services.AddScoped<ClientTypeInferenceService>();
builder.Services.AddScoped<PriorityInferenceService>();
builder.Services.AddScoped<ContactInferenceService>();
builder.Services.AddScoped<CommercialTaskEnrichmentService>();
builder.Services.AddScoped<CommercialDashboardService>();
builder.Services.AddScoped<CommercialTaskService>();
builder.Services.AddScoped<PipelineAnalyticsService>();
builder.Services.AddScoped<IOpportunityRepository, OpportunityRepository>();
builder.Services.AddScoped<IMoveHistoryRepository, MoveHistoryRepository>();
builder.Services.AddScoped<OpportunityMoveService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.EnsureCreated();

    using var conn = db.Database.GetDbConnection();
    conn.Open();

    // Legacy migration from old schema (PK string Gid) to new mini-CRM schema.
    var hasId = false;
    var hasLegacyGid = false;
    using (var checkCmd = conn.CreateCommand())
    {
        checkCmd.CommandText = "PRAGMA table_info('CommercialTasks');";
        using var reader = checkCmd.ExecuteReader();
        while (reader.Read())
        {
            var columnName = reader.GetString(1);
            if (string.Equals(columnName, "Id", StringComparison.OrdinalIgnoreCase)) hasId = true;
            if (string.Equals(columnName, "Gid", StringComparison.OrdinalIgnoreCase)) hasLegacyGid = true;
        }
    }

    if (!hasId && hasLegacyGid)
    {
        using var createNewCmd = conn.CreateCommand();
        createNewCmd.CommandText = @"
CREATE TABLE IF NOT EXISTS CommercialTasks_New (
    Id INTEGER NOT NULL CONSTRAINT PK_CommercialTasks PRIMARY KEY AUTOINCREMENT,
    AsanaTaskGid TEXT NULL,
    ProjectGid TEXT NOT NULL DEFAULT '',
    ProjectName TEXT NOT NULL DEFAULT '',
    SectionGid TEXT NULL,
    SectionName TEXT NOT NULL DEFAULT '',
    Name TEXT NOT NULL DEFAULT '',
    Description TEXT NULL,
    AssigneeName TEXT NULL,
    AssigneeGid TEXT NULL,
    DueOn TEXT NULL,
    CreatedAt TEXT NULL,
    ModifiedAt TEXT NOT NULL,
    CompletedAt TEXT NULL,
    IsCompleted INTEGER NOT NULL DEFAULT 0,
    PermalinkUrl TEXT NULL,
    TagsJson TEXT NULL,
    CustomFieldsJson TEXT NULL,
    StoryCount INTEGER NULL,
    LastSyncAt TEXT NOT NULL,
    Source TEXT NOT NULL DEFAULT 'Asana',
    LocalStatus TEXT NOT NULL DEFAULT 'Nuevo',
    Priority TEXT NOT NULL DEFAULT 'Media',
    EstimatedValue TEXT NULL,
    Probability INTEGER NULL,
    Country TEXT NULL,
    Language TEXT NULL,
    ClientType TEXT NOT NULL DEFAULT 'Otro',
    ClientName TEXT NULL,
    ContactName TEXT NULL,
    ContactPhone TEXT NULL,
    ContactMobile TEXT NULL,
    ContactRole TEXT NULL,
    ContactEmail TEXT NULL,
    ContactsJson TEXT NULL,
    NextAction TEXT NULL,
    NextActionDate TEXT NULL,
    InternalNotes TEXT NULL,
    CommercialOwner TEXT NULL,
    IsArchivedLocally INTEGER NOT NULL DEFAULT 0,
    IsLocalStatusManual INTEGER NOT NULL DEFAULT 0,
    IsPriorityManual INTEGER NOT NULL DEFAULT 0,
    IsCountryManual INTEGER NOT NULL DEFAULT 0,
    IsLanguageManual INTEGER NOT NULL DEFAULT 0,
    IsClientTypeManual INTEGER NOT NULL DEFAULT 0,
    IsClientNameManual INTEGER NOT NULL DEFAULT 0,
    IsContactNameManual INTEGER NOT NULL DEFAULT 0,
    IsContactPhoneManual INTEGER NOT NULL DEFAULT 0,
    IsContactMobileManual INTEGER NOT NULL DEFAULT 0,
    IsContactRoleManual INTEGER NOT NULL DEFAULT 0,
    IsContactEmailManual INTEGER NOT NULL DEFAULT 0,
    IsNextActionManual INTEGER NOT NULL DEFAULT 0,
    IsCommercialOwnerManual INTEGER NOT NULL DEFAULT 0,
    IsProbabilityManual INTEGER NOT NULL DEFAULT 0,
    IsEstimatedValueManual INTEGER NOT NULL DEFAULT 0,
    AutoEnrichmentNotes TEXT NULL,
    LastAutoEnrichedAt TEXT NULL
);";
        createNewCmd.ExecuteNonQuery();

        using var copyCmd = conn.CreateCommand();
        copyCmd.CommandText = @"
INSERT INTO CommercialTasks_New
(AsanaTaskGid, ProjectGid, ProjectName, SectionGid, SectionName, Name, AssigneeName, DueOn, ModifiedAt, IsCompleted, PermalinkUrl, LastSyncAt)
SELECT
    Gid,
    COALESCE(ProjectGid, ''),
    '',
    NULL,
    COALESCE(Section, ''),
    COALESCE(Name, ''),
    Assignee,
    DueOn,
    COALESCE(ModifiedAt, CURRENT_TIMESTAMP),
    COALESCE(Completed, 0),
    PermalinkUrl,
    COALESCE(SyncedAt, CURRENT_TIMESTAMP)
FROM CommercialTasks;";
        copyCmd.ExecuteNonQuery();

        using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = "DROP TABLE CommercialTasks;";
        dropCmd.ExecuteNonQuery();

        using var renameCmd = conn.CreateCommand();
        renameCmd.CommandText = "ALTER TABLE CommercialTasks_New RENAME TO CommercialTasks;";
        renameCmd.ExecuteNonQuery();
    }

    var requiredColumns = new Dictionary<string, string>
    {
        ["AsanaTaskGid"] = "TEXT NULL",
        ["ProjectGid"] = "TEXT NOT NULL DEFAULT ''",
        ["ProjectName"] = "TEXT NOT NULL DEFAULT ''",
        ["SectionGid"] = "TEXT NULL",
        ["SectionName"] = "TEXT NOT NULL DEFAULT ''",
        ["Description"] = "TEXT NULL",
        ["AssigneeName"] = "TEXT NULL",
        ["AssigneeGid"] = "TEXT NULL",
        ["CreatedAt"] = "TEXT NULL",
        ["CompletedAt"] = "TEXT NULL",
        ["IsCompleted"] = "INTEGER NOT NULL DEFAULT 0",
        ["TagsJson"] = "TEXT NULL",
        ["CustomFieldsJson"] = "TEXT NULL",
        ["StoryCount"] = "INTEGER NULL",
        ["LastSyncAt"] = "TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP",
        ["Source"] = "TEXT NOT NULL DEFAULT 'Asana'",
        ["LocalStatus"] = "TEXT NOT NULL DEFAULT 'Nuevo'",
        ["Priority"] = "TEXT NOT NULL DEFAULT 'Media'",
        ["EstimatedValue"] = "TEXT NULL",
        ["Probability"] = "INTEGER NULL",
        ["Country"] = "TEXT NULL",
        ["Language"] = "TEXT NULL",
        ["ClientType"] = "TEXT NOT NULL DEFAULT 'Otro'",
        ["ClientName"] = "TEXT NULL",
        ["ContactName"] = "TEXT NULL",
        ["ContactPhone"] = "TEXT NULL",
        ["ContactMobile"] = "TEXT NULL",
        ["ContactRole"] = "TEXT NULL",
        ["ContactEmail"] = "TEXT NULL",
        ["ContactsJson"] = "TEXT NULL",
        ["NextAction"] = "TEXT NULL",
        ["NextActionDate"] = "TEXT NULL",
        ["InternalNotes"] = "TEXT NULL",
        ["CommercialOwner"] = "TEXT NULL",
        ["IsArchivedLocally"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsLocalStatusManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsPriorityManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsCountryManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsLanguageManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsClientTypeManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsClientNameManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsContactNameManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsContactPhoneManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsContactMobileManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsContactRoleManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsContactEmailManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsNextActionManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsCommercialOwnerManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsProbabilityManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["IsEstimatedValueManual"] = "INTEGER NOT NULL DEFAULT 0",
        ["AutoEnrichmentNotes"] = "TEXT NULL",
        ["LastAutoEnrichedAt"] = "TEXT NULL"
    };

    var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    using (var checkCmd = conn.CreateCommand())
    {
        checkCmd.CommandText = "PRAGMA table_info('CommercialTasks');";
        using var reader = checkCmd.ExecuteReader();
        while (reader.Read())
        {
            existingColumns.Add(reader.GetString(1));
        }
    }

    foreach (var col in requiredColumns)
    {
        if (!existingColumns.Contains(col.Key))
        {
            using var alterCmd = conn.CreateCommand();
            alterCmd.CommandText = $"ALTER TABLE CommercialTasks ADD COLUMN {col.Key} {col.Value};";
            alterCmd.ExecuteNonQuery();
        }
    }

    using var moveHistoryCmd = conn.CreateCommand();
    moveHistoryCmd.CommandText = @"
CREATE TABLE IF NOT EXISTS OpportunityMoveHistories (
    Id INTEGER NOT NULL CONSTRAINT PK_OpportunityMoveHistories PRIMARY KEY AUTOINCREMENT,
    CommercialTaskId INTEGER NULL,
    AsanaTaskGid TEXT NULL,
    TaskName TEXT NOT NULL DEFAULT '',
    FromProjectGid TEXT NOT NULL DEFAULT '',
    FromProjectName TEXT NOT NULL DEFAULT '',
    ToProjectGid TEXT NOT NULL DEFAULT '',
    ToProjectName TEXT NOT NULL DEFAULT '',
    MovedBy TEXT NOT NULL DEFAULT 'local-user',
    MovedAtUtc TEXT NOT NULL,
    Success INTEGER NOT NULL DEFAULT 0,
    ErrorMessage TEXT NULL
);";
    moveHistoryCmd.ExecuteNonQuery();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<global::DirectorComercialIA.AppRoot>()
    .AddInteractiveServerRenderMode();

app.Run();
