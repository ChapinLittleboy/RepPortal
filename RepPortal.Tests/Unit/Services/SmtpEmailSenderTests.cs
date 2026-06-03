using MailKit.Net.Smtp;
using RepPortal.Services;

namespace RepPortal.Tests.Unit.Services;

public class SmtpEmailSenderTests
{
    [Fact]
    public void ShouldAuthenticate_ReturnsFalse_WhenServerDoesNotAdvertiseAuthentication()
    {
        var shouldAuthenticate = SmtpEmailSender.ShouldAuthenticate(
            "Administrator",
            "password",
            SmtpCapabilities.Size);

        Assert.False(shouldAuthenticate);
    }

    [Fact]
    public void ShouldAuthenticate_ReturnsTrue_WhenCredentialsAndAuthenticationCapabilityExist()
    {
        var shouldAuthenticate = SmtpEmailSender.ShouldAuthenticate(
            "Administrator",
            "password",
            SmtpCapabilities.Authentication | SmtpCapabilities.Size);

        Assert.True(shouldAuthenticate);
    }

    [Theory]
    [InlineData(null, "password")]
    [InlineData("Administrator", null)]
    [InlineData("", "password")]
    [InlineData("Administrator", "")]
    public void ShouldAuthenticate_ReturnsFalse_WhenCredentialsAreIncomplete(string? user, string? pass)
    {
        var shouldAuthenticate = SmtpEmailSender.ShouldAuthenticate(
            user,
            pass,
            SmtpCapabilities.Authentication);

        Assert.False(shouldAuthenticate);
    }
}
