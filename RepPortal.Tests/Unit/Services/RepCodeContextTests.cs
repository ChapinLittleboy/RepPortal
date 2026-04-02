using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RepPortal.Services;

namespace RepPortal.Tests.Unit.Services;

public class RepCodeContextTests
{
    [Fact]
    public void CurrentRepCode_ShouldComeFromClaims_WhenNotOverridden()
    {
        var sut = CreateContext(
            new Claim("RepCode", "REP1"),
            new Claim("Region", "NE"));

        Assert.Equal("REP1", sut.CurrentRepCode);
    }

    [Fact]
    public void CurrentRegions_ShouldComeFromClaims_WhenNotOverridden()
    {
        var sut = CreateContext(
            new Claim("RepCode", "REP1"),
            new Claim("Region", "NE"),
            new Claim("Region", "SE"));

        Assert.Equal(new[] { "NE", "SE" }, sut.CurrentRegions);
    }

    [Fact]
    public void OverrideRepCode_ShouldChangeRepCode_AndClearRegions()
    {
        var sut = CreateContext(
            new Claim("RepCode", "REP1"),
            new Claim("Region", "NE"));

        sut.OverrideRepCode("REP2");

        Assert.Equal("REP2", sut.CurrentRepCode);
        Assert.Empty(sut.CurrentRegions);
    }

    [Fact]
    public void OverrideRepCodeWithRegions_ShouldChangeRepCode_AndRegions()
    {
        var sut = CreateContext(new Claim("RepCode", "REP1"));

        sut.OverrideRepCode("LAW", new List<string> { "MW", "SW" });

        Assert.Equal("LAW", sut.CurrentRepCode);
        Assert.Equal(new[] { "MW", "SW" }, sut.CurrentRegions);
    }

    [Fact]
    public void ResetRepCode_ShouldRestoreClaimValues()
    {
        var sut = CreateContext(
            new Claim("RepCode", "REP1"),
            new Claim("Region", "NE"),
            new Claim("Region", "SE"));
        sut.OverrideRepCode("LAW", new List<string> { "MW" });

        sut.ResetRepCode();

        Assert.Equal("REP1", sut.CurrentRepCode);
        Assert.Equal(new[] { "NE", "SE" }, sut.CurrentRegions);
    }

    [Fact]
    public void IsAdministrator_ShouldReflectRoleMembership()
    {
        var sut = CreateContext(
            new Claim(ClaimTypes.Role, "Administrator"),
            new Claim("RepCode", "REP1"));

        Assert.True(sut.IsAdministrator);
    }

    [Fact]
    public void UserName_ShouldReturnNameClaim_WhenPresent()
    {
        var withName = CreateContext(new Claim(ClaimTypes.Name, "fallback-name"));

        Assert.Equal("fallback-name", withName.UserName);
    }

    [Fact]
    public void CurrentFirstAndLastName_ShouldComeFromClaims()
    {
        var sut = CreateContext(
            new Claim("FirstName", "Will"),
            new Claim("LastName", "Tester"));

        Assert.Equal("Will", sut.CurrentFirstName);
        Assert.Equal("Tester", sut.CurrentLastName);
    }

    [Fact]
    public void AssignedRegionAndRepRegion_ShouldComeFromClaims()
    {
        var sut = CreateContext(
            new Claim("Region", "NE"),
            new Claim("AssignedRegion", "MW"));

        Assert.Equal("NE", sut.RepRegion);
        Assert.Equal("MW", sut.AssignedRegion);
    }

    [Fact]
    public void OverrideAndReset_ShouldRaiseOnRepCodeChanged()
    {
        var sut = CreateContext(new Claim("RepCode", "REP1"));
        var count = 0;
        sut.OnRepCodeChanged += () => count++;

        sut.OverrideRepCode("REP2");
        sut.ResetRepCode();

        Assert.Equal(2, count);
    }

    private static RepCodeContext CreateContext(params Claim[] claims)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new RepCodeContext(accessor);
    }
}
