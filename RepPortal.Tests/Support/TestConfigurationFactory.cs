using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using RepPortal.Models;

namespace RepPortal.Tests.Support;

internal static class TestConfigurationFactory
{
    public static IConfiguration Create(params (string Key, string? Value)[] entries)
    {
        var dict = entries.ToDictionary(x => x.Key, x => x.Value);
        return new ConfigurationBuilder()
            .AddInMemoryCollection(dict)
            .Build();
    }

    public static IOptions<CsiOptions> CreateCsiOptions(Action<CsiOptions>? configure = null)
    {
        var options = new CsiOptions();
        configure?.Invoke(options);
        return Options.Create(options);
    }
}
