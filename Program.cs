using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using RepPortal.Areas.Identity;
using RepPortal.Data;
using RepPortal.Services;
using Serilog;
using Serilog.Events;
using Syncfusion.Blazor;

Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NMaF5cXmBCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWXxfcHVWRWBfV0x/VkQ=");
// Set the global command timeout for Dapper
SqlMapper.Settings.CommandTimeout = 60; // Timeout set to 60 seconds



// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        "Logs/RepPortal-log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,  // Keep 14 days of logs
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("RepPortalConnection") ?? throw new InvalidOperationException("Connection string 'RepPortalConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<CustomUserClaimsPrincipalFactory>();

builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<ApplicationUser>>();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddScoped<CreditHoldExclusionService>();
builder.Services.AddScoped<UserManager<ApplicationUser>>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddSyncfusionBlazor();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRepCodeContext, RepCodeContext>();
builder.Services.AddSingleton<UserConnectionTracker>();
builder.Services.AddScoped<CircuitHandler, TrackingCircuitHandler>();
builder.Services.AddScoped<SignInManager<ApplicationUser>, CustomSignInManager>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddSingleton<DbConnectionFactory>();
builder.Services.AddScoped<PcfService>();
builder.Services.AddScoped<TitleService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IPriceBookService, PriceBookService>();
builder.Services.AddScoped<IFormsDownloadService, FormsDownloadService>();
builder.Services.AddScoped<CreditHoldExclusionService>();



var app = builder.Build();
var cfg = app.Configuration.GetSection("PriceBooks");
var rootPath = cfg["RootPath"]!;      // should be \\ciiws01\ChapinRepDocs
var requestPath = cfg["RequestPath"]!;   // should be /RepDocs

var scope = app.Services.CreateScope();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// This serves files from wwwroot (default behavior)
app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(rootPath),
    RequestPath = requestPath,
    ServeUnknownFileTypes = true
});

app.UseRouting();

app.UseAuthorization();
app.UseAuthentication();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
