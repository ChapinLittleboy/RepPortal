using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace RepPortal.Tests.Integration;

public sealed class RepPortalWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "RepPortalTestStaticFiles");
            Directory.CreateDirectory(tempRoot);

            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:RepPortalConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=RepPortalTest;Trusted_Connection=True;TrustServerCertificate=True;",
                ["ConnectionStrings:BatAppConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=BatAppTest;Trusted_Connection=True;TrustServerCertificate=True;",
                ["PriceBooks:RootPath"] = tempRoot,
                ["PriceBooks:RequestPath"] = "/RepDocs",
                ["Sites:BAT:ConnectionString"] = "Server=(localdb)\\MSSQLLocalDB;Database=BatSiteTest;Trusted_Connection=True;TrustServerCertificate=True;",
                ["Sites:BAT:PackingListProc"] = "dbo.Rep_Rpt_PackingSlipByBOLSp",
                ["Csi:UseApi"] = "false",
                ["RunIdentityEmailMigration"] = "false",
                ["TestSettings:SkipStartupTasks"] = "true",
                ["TestSettings:DisableHangfire"] = "true",
                ["Smtp:Host"] = "localhost",
                ["Smtp:Port"] = "25"
            });
        });
    }
}
