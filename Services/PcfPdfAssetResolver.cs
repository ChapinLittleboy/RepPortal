namespace RepPortal.Services;

public class PcfPdfAssetResolver
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public PcfPdfAssetResolver(
        IWebHostEnvironment env,
        IConfiguration config)
    {
        _env = env;
        _config = config;
    }

    public string GetAssetPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Asset file name is required.", nameof(fileName));

        var relativePath = _config["PcfPdf:AssetsPath"];
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidOperationException("PcfPdf:AssetsPath is not configured.");

        var assetRoot = Path.GetFullPath(Path.Combine(_env.ContentRootPath, relativePath));
        var candidatePath = Path.GetFullPath(Path.Combine(assetRoot, fileName));

        var assetRootWithSeparator = assetRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                   + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(assetRootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidatePath, assetRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Asset path must remain within the configured asset directory.");
        }

        return candidatePath;
    }
}
