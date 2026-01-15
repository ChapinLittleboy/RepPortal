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
        var relativePath = _config["PcfPdf:AssetsPath"];
        return Path.Combine(_env.ContentRootPath, relativePath, fileName);
    }
}