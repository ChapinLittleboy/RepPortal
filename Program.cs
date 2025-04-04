using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.CodeAnalysis.Elfie.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepPortal.Areas.Identity;
using RepPortal.Data;
using RepPortal.Services;
using Syncfusion.Blazor;


Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NMaF5cXmBCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdmWXxfcHVWRWBfV0x/VkQ=");
// Set the global command timeout for Dapper
SqlMapper.Settings.CommandTimeout = 60; // Timeout set to 60 seconds




var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
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


builder.Services.AddScoped<UserManager<ApplicationUser>>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<SalesService>();
builder.Services.AddSyncfusionBlazor();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRepCodeContext, RepCodeContext>();



//builder.Services.AddSingleton<WeatherForecastService>();

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILogger<Program>>();




/*
var scope = app.Services.CreateScope();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

string[] roles = new[] { "Administrator", "Sales", "Manager", "SuperUser" };

foreach (var role in roles)
{
    if (!await roleManager.RoleExistsAsync(role))
    {
        await roleManager.CreateAsync(new IdentityRole(role));
    }
}


*/

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

app.UseStaticFiles();
app.Use(async (context, next) =>
{
    var userName = context.User?.Identity?.Name ?? "<null>";
    logger.LogInformation("Startup middleware — User.Identity.Name = {Name}", userName);
    await next();
});


app.UseRouting();

app.UseAuthorization();
app.UseAuthentication();


app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
