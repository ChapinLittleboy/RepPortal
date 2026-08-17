using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using RepPortal.Models;
using RepPortal.Services;

namespace RepPortal.Controllers;

[Authorize]
[Route("srf-launch")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class SrfLaunchController : ControllerBase
{
    private readonly ISrfLaunchTokenService _tokenService;
    private readonly SrfLaunchOptions _options;
    private readonly ILogger<SrfLaunchController> _logger;

    public SrfLaunchController(
        ISrfLaunchTokenService tokenService,
        IOptions<SrfLaunchOptions> options,
        ILogger<SrfLaunchController> logger)
    {
        _tokenService = tokenService;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";

        if (!_tokenService.TryCreateToken(
                DateTimeOffset.UtcNow,
                out var token,
                out var error))
        {
            _logger.LogError("Unable to create an SRF launch token: {Reason}", error);
            return Problem(
                title: "Sample Request Form launch is not configured",
                detail: "Please contact the Rep Portal administrator.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!Uri.TryCreate(_options.LaunchUrl, UriKind.Absolute, out var launchUri) ||
            !string.Equals(launchUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("The configured SRF launch URL is not a valid HTTPS URL.");
            return Problem(
                title: "Sample Request Form launch is not configured",
                detail: "Please contact the Rep Portal administrator.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var target = QueryHelpers.AddQueryString(_options.LaunchUrl, "token", token);
        return Redirect(target);
    }
}
