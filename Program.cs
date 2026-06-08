using DirectorComercialIA.Data;
using DirectorComercialIA.Mail;
using DirectorComercialIA.Mail.Abstractions;
using DirectorComercialIA.Mail.Endpoints;
using DirectorComercialIA.Mail.Services;
using DirectorComercialIA.Repositories;
using DirectorComercialIA.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=director_comercial.db";

var useSqlServer = DatabaseProvider.IsSqlServer(connectionString);

builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    if (useSqlServer)
        options.UseSqlServer(connectionString);
    else
        options.UseSqlite(connectionString);
});
builder.Services.AddScoped<AppDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddDataProtection();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<GmailOptions>(builder.Configuration.GetSection(GmailOptions.SectionName));

builder.Services.AddScoped<MailGmailOAuthService>();
builder.Services.AddScoped<IMailGmailOAuthService>(sp => sp.GetRequiredService<MailGmailOAuthService>());
builder.Services.AddScoped<IMailExcelImportService, MailExcelImportService>();
builder.Services.AddScoped<IMailContactQueryService, MailContactQueryService>();
builder.Services.AddScoped<IMailContactCommandService, MailContactCommandService>();
builder.Services.AddScoped<IMailEmailDispatchService, MailEmailDispatchService>();
builder.Services.AddScoped<IMailReplyCheckService, MailReplyCheckService>();

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

    if (useSqlServer)
    {
        if (!await db.Database.CanConnectAsync())
        {
            throw new InvalidOperationException(
                "Cannot connect to SQL Server. Verify the connection string, that database 'CommercialFollowing' exists on RDS, " +
                "and that this machine can reach the server (VPN/firewall). For local dev without RDS, set ConnectionStrings:Default to " +
                "\"Data Source=director_comercial.db\".");
        }

        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        db.Database.EnsureCreated();
        SqliteSchemaBootstrap.Run(db);
    }

    await MailSeedData.EnsureDefaultTemplatesAsync(db);
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseAntiforgery();

app.MapMailGmailOAuthEndpoints();

app.MapRazorComponents<global::DirectorComercialIA.AppRoot>()
    .AddInteractiveServerRenderMode();

app.Run();
