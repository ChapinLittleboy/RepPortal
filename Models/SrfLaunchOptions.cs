namespace RepPortal.Models;

public sealed class SrfLaunchOptions
{
    public const string SectionName = "SampleRequestForm";

    public string LaunchUrl { get; set; } =
        "https://chapinlink.com/SalesNew/SRF/launch.php";

    public string? LaunchSecret { get; set; }

    public int TokenLifetimeSeconds { get; set; } = 60;
}
