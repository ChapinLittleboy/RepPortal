using System.Reflection;
using ChapSureShipHistoryLoader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using RepPortal.Models;
using RepPortal.Services;

if (args.Any(argument => argument is "--help" or "-h"))
{
    Console.WriteLine(LoaderOptions.Usage);
    return 0;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var root = FindRepositoryRoot();
    var options = LoaderOptions.Parse(args, root);
    var configuration = new ConfigurationBuilder()
        .SetBasePath(root)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
        .AddEnvironmentVariables()
        .Build();

    var connections = options.Sites.ToDictionary(site => site, site =>
        SourceConnection(configuration[$"Sites:{site}:ConnectionString"]
            ?? throw new InvalidOperationException($"Sites:{site}:ConnectionString is not configured."),
            options.SourceIntegratedSecurity),
        StringComparer.OrdinalIgnoreCase);

    IChapSureShipHistoryApiClient? api = null;
    if (options.Commit)
    {
        var csi = configuration.GetSection("Csi").Get<CsiOptions>() ?? new CsiOptions();
        var validation = new CsiOptionsValidator().Validate(null, csi);
        if (validation.Failed)
            throw new InvalidOperationException(validation.FailureMessage);
        var wrapped = Options.Create(csi);
        var factory = new LoaderHttpClientFactory();
        var tokenProvider = new IonTokenProvider(factory, wrapped);
        var rest = new CsiRestClient(factory.Client, tokenProvider, wrapped);
        api = new ChapSureShipHistoryApiClient(rest);
    }

    await new HistoryLoader(options, connections, api).RunAsync(cancellation.Token);
    Console.WriteLine(options.Commit ? "Load completed." : "Dry run completed; no MT data was changed.");
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Canceled. A commit run can resume from its checkpoint.");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Loader failed: {exception.Message}");
    return 1;
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
         directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "RepPortal.csproj")))
            return directory.FullName;
    }
    throw new DirectoryNotFoundException("Could not locate RepPortal.csproj.");
}

static string SourceConnection(string configured, bool integratedSecurity)
{
    if (!integratedSecurity)
        return configured;
    var builder = new SqlConnectionStringBuilder(configured)
    {
        IntegratedSecurity = true,
        UserID = "",
        Password = ""
    };
    return builder.ConnectionString;
}

internal sealed class LoaderHttpClientFactory : IHttpClientFactory, IDisposable
{
    public HttpClient Client { get; } = new() { Timeout = TimeSpan.FromMinutes(5) };
    public HttpClient CreateClient(string name) => Client;
    public void Dispose() => Client.Dispose();
}
