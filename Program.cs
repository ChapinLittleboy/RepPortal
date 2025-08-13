﻿using System.Reflection;
using Blazored.LocalStorage;
using Dapper;
using DbUp;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using RepPortal.Areas.Identity;
using RepPortal.Data;
using RepPortal.Services;
using Serilog;
using Serilog.Events;
using SixLabors.ImageSharp.Web.DependencyInjection;
using Syncfusion.Blazor;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;


Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NNaF5cXmBCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWXxcd3VVRGVYUkV3WUBWYEo=");
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
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddClaimsPrincipalFactory<CustomUserClaimsPrincipalFactory>();

builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<ApplicationUser>>();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.SignIn.RequireConfirmedEmail = true;
});


builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

//builder.Services.AddServerSideBlazor();
builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(o =>
    {
        o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(15); // default ~3m
        o.DisconnectedCircuitMaxRetained = 1000; // raise if you have lots of users
    });
builder.Services.AddSignalR(o =>
{
    o.KeepAliveInterval = TimeSpan.FromSeconds(10);   // default 15s
    o.ClientTimeoutInterval = TimeSpan.FromSeconds(60); // default 30s
});
builder.Services.AddScoped<CreditHoldExclusionService>();
builder.Services.AddScoped<UserManager<ApplicationUser>>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddSyncfusionBlazor();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRepCodeContext, RepCodeContext>();
builder.Services.AddSingleton<UserConnectionTracker>();
builder.Services.AddScoped<CircuitHandler, TrackingCircuitHandler>();
builder.Services.AddScoped<SignInManager<ApplicationUser>, CustomSignInManager>();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<PcfService>();
builder.Services.AddScoped<TitleService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IDownloadPriceBookService, DownloadPriceBookService>();
builder.Services.AddScoped<IFormsDownloadService, DownloadFormsService>();
builder.Services.AddScoped<IMarketingService, DownloadMarketingInfoService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<StateContainer>();
builder.Services.AddScoped<FolderAdminService>();
builder.Services.AddScoped<MarketingFileService>();
builder.Services.AddScoped<HelpContentService>();
builder.Services.AddScoped<IPageDefinitionService, PageDefinitionService>();
builder.Services.AddHttpClient<AIService>();
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<IUsageAnalyticsService, UsageAnalyticsService>();
builder.Services.AddScoped<IPriceBookService, PriceBookService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();


builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ForgotPwdLimiter", opt =>
    {
        opt.PermitLimit = 6;                 // max requests
        opt.Window = TimeSpan.FromMinutes(10);
        opt.QueueLimit = 0;                 // reject extra immediately
       // opt.AutoReplenishment = true;
        //opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Friendly JSON body when the limiter kicks in
    options.OnRejected = static (context, cancellationToken) =>
    {
        // If the pipeline already started the response, bail out
        if (context.HttpContext.Response.HasStarted)
            return ValueTask.CompletedTask;

        var resp = context.HttpContext.Response;
        resp.StatusCode = StatusCodes.Status429TooManyRequests;
        resp.ContentType = "application/json";

        return new ValueTask(resp.WriteAsync(
            """
            { "error": "Too many reset requests – try again in a few minutes." }
            """,
            cancellationToken));
    };
});

builder.Services.AddRazorPages()
    .AddRazorPagesOptions(o =>
    {
        // stick the limiter on Identity/Account/ForgotPassword
        o.Conventions.AddAreaPageApplicationModelConvention(
            "Identity", "/Account/ForgotPassword",
            m => m.EndpointMetadata.Add(
                new EnableRateLimitingAttribute("ForgotPwdLimiter")));
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    // Absolute lifetime for a “Remember me” cookie
    options.ExpireTimeSpan = TimeSpan.FromDays(7);   // default is 14 days
    options.SlidingExpiration = false;                    // re-issue the cookie when half-used
    // optional niceties
    options.Cookie.Name     = ".ChapPortal.Auth";
    // options.LoginPath       = "/login";
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

// Add the new service
builder.Services.AddScoped<IInsuranceRequestService, InsuranceRequestService>();

var app = builder.Build();




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

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower();

    if (path != null && path.StartsWith("/chapinrep"))
    {
        context.Response.Redirect("/", permanent: false); // Or use internal rewrite
        return;
    }

    await next();
});

// This serves files from wwwroot (default behavior)

app.UseStaticFiles();

var cfg = app.Configuration.GetSection("PriceBooks");
var rootPath = cfg["RootPath"]!;      // should be \\ciiws01\ChapinRepDocs
var requestPath = cfg["RequestPath"]!;   //

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(rootPath),
    RequestPath = requestPath,
    ServeUnknownFileTypes = true
});


app.UseRouting();
app.UseRateLimiter();                       // ❷  Enable the middleware


app.UseAuthentication();
app.UseAuthorization();



app.UseEndpoints(endpoints =>
{
    endpoints.MapRazorPages();
    endpoints.MapControllers();
});




//app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

//RunDbUp(connectionString);
await RunConfigureRoles();


app.Run();





void RunDbUp(string connectionString)
{
    var upgrader = DeployChanges.To
        .SqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly()) // or use FromFileSystem
        .LogToConsole()
        .Build();

    var result = upgrader.PerformUpgrade();

    if (!result.Successful)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(result.Error);
        Console.ResetColor();
        throw new Exception("Database upgrade failed");
    }

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Database upgrade successful");
    Console.ResetColor();
}

async Task RunConfigureRoles()
{
    var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = new[] { "Administrator", "SalesManager", "SalesRep", "User", "SuperUser" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

}