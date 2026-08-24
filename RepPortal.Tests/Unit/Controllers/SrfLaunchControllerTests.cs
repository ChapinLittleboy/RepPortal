using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RepPortal.Controllers;
using RepPortal.Models;
using RepPortal.Services;

namespace RepPortal.Tests.Unit.Controllers;

public sealed class SrfLaunchControllerTests
{
    [Fact]
    public void Controller_RequiresAuthentication()
    {
        var attribute = typeof(SrfLaunchController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(attribute);
    }

    [Fact]
    public void Get_AppendsTokenAndRedirectsToConfiguredLaunchUrl()
    {
        const string token = "v1.1786986060.nonce.signature";
        var emptyError = string.Empty;
        var tokenService = new Mock<ISrfLaunchTokenService>();
        tokenService
            .Setup(service => service.TryCreateToken(
                It.IsAny<DateTimeOffset>(),
                out It.Ref<string>.IsAny,
                out It.Ref<string>.IsAny))
            .Callback(new TryCreateTokenCallback(
                (DateTimeOffset _, out string createdToken, out string error) =>
                {
                    createdToken = token;
                    error = emptyError;
                }))
            .Returns(true);
        var controller = CreateController(tokenService.Object);

        var result = Assert.IsType<RedirectResult>(controller.Get());

        var target = new Uri(result.Url!);
        Assert.Equal("https", target.Scheme);
        Assert.Equal("chapinlink.com", target.Host);
        Assert.Equal("/SalesNew/SRF/launch.php", target.AbsolutePath);
        Assert.Equal(token, QueryHelpers.ParseQuery(target.Query)["token"]);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl);
        Assert.Equal("no-referrer", controller.Response.Headers["Referrer-Policy"]);
    }

    [Fact]
    public void Get_ReturnsServiceUnavailableWhenTokenCannotBeCreated()
    {
        var tokenService = new Mock<ISrfLaunchTokenService>();
        tokenService
            .Setup(service => service.TryCreateToken(
                It.IsAny<DateTimeOffset>(),
                out It.Ref<string>.IsAny,
                out It.Ref<string>.IsAny))
            .Callback(new TryCreateTokenCallback(
                (DateTimeOffset _, out string token, out string error) =>
                {
                    token = string.Empty;
                    error = "Secret missing.";
                }))
            .Returns(false);
        var controller = CreateController(tokenService.Object);

        var result = Assert.IsType<ObjectResult>(controller.Get());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Sample Request Form launch is not configured", problem.Title);
    }

    private static SrfLaunchController CreateController(
        ISrfLaunchTokenService tokenService)
    {
        var options = Options.Create(new SrfLaunchOptions
        {
            LaunchUrl = "https://chapinlink.com/SalesNew/SRF/launch.php"
        });
        var controller = new SrfLaunchController(
            tokenService,
            options,
            NullLogger<SrfLaunchController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private delegate void TryCreateTokenCallback(
        DateTimeOffset now,
        out string token,
        out string error);
}
