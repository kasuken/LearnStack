using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LearnStack;
using LearnStack.Components;
using LearnStack.Components.Account;
using LearnStack.Data;
using LearnStack.Extensions;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var isAzureAppService = !string.IsNullOrWhiteSpace(builder.Configuration["WEBSITE_INSTANCE_ID"]);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    if (isAzureAppService)
    {
        // App Service routes public traffic through its platform proxy before it reaches the worker.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLearnStackLocalization();
builder.Services.AddSingleton<SiteUrls>();
builder.Services.AddLearnStackRateLimiting();
builder.Services.AddControllers();

builder.Services.AddMudServices();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddLearnStackData(connectionString, migrationsAssembly: "LearnStack");
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database", tags: ["ready"]);

// Persist the Data Protection key ring outside the container so that auth cookies and
// antiforgery tokens survive restarts, deployments, slot swaps and scale-out on App
// Service. The default (ephemeral, in-container) key ring is fine for local Development.
// (AddLearnStackData's AddDbContextFactory<ApplicationDbContext> call above also
// registers ApplicationDbContext itself as a scoped service, which is what
// PersistKeysToDbContext needs to resolve it.)
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("LearnStack");

if (!builder.Environment.IsDevelopment())
{
    dataProtectionBuilder.PersistKeysToDbContext<ApplicationDbContext>();
}

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        // Registration signs the user in directly without sending a confirmation email,
        // so requiring a confirmed account here would lock every new user out after
        // their first session. Re-enable only once a real email sender is configured.
        options.SignIn.RequireConfirmedAccount = false;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddLearnStackEmailSender(builder.Configuration);

// Add application services
builder.Services.AddLearnStackApplicationServices();
builder.Services.AddLearnStackBilling(builder.Configuration);

var app = builder.Build();

// Apply pending migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Apply any pending migrations
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database.");
        throw;
    }
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["Content-Security-Policy-Report-Only"] =
        "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; " +
        "script-src 'self' 'unsafe-inline' https://www.clarity.ms https://*.clarity.ms; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com data:; " +
        "img-src 'self' data: blob:; " +
        "connect-src 'self' wss: https://*.clarity.ms; " +
        "worker-src 'self' blob:";
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Frame-Options"] = "DENY";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// /health/live tells App Service the process is up; /health/ready also checks the database
// and gates every release (see .github/workflows/release.yml).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.UseRequestLocalization();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();
app.MapSeoEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
