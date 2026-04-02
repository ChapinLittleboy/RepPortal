using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using RepPortal.Data;
using RepPortal.Services;
using RepPortal.Tests.Support;

namespace RepPortal.Tests.Unit.Services;

public class IdentityEmailDomainMigrationTests
{
    [Fact]
    public async Task RunAsync_ShouldUpdateUsers_OnOldDomain()
    {
        var snapshot = new ApplicationUser { Id = "1", Email = "alice@chapinmfg.com", UserName = "alice@chapinmfg.com" };
        var live = new ApplicationUser { Id = "1", Email = "alice@chapinmfg.com", UserName = "alice@chapinmfg.com" };
        var userManager = UserManagerMockFactory.Create();
        userManager.SetupGet(x => x.Users).Returns(new[] { snapshot }.AsQueryable());
        userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(live);
        userManager.Setup(x => x.FindByEmailAsync("alice@chapinusa.com")).ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(x => x.FindByNameAsync("alice@chapinusa.com")).ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(x => x.UpdateNormalizedEmailAsync(live)).Returns(Task.CompletedTask);
        userManager.Setup(x => x.UpdateNormalizedUserNameAsync(live)).Returns(Task.CompletedTask);
        userManager.Setup(x => x.UpdateAsync(live)).ReturnsAsync(IdentityResult.Success);

        var sut = new IdentityEmailDomainMigration(userManager.Object, Mock.Of<ILogger<IdentityEmailDomainMigration>>());

        await sut.RunAsync();

        Assert.Equal("alice@chapinusa.com", live.Email);
        Assert.Equal("alice@chapinusa.com", live.UserName);
        userManager.Verify(x => x.UpdateAsync(live), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ShouldSkipUsers_AlreadyOnNewDomain()
    {
        var snapshot = new ApplicationUser { Id = "1", Email = "alice@chapinusa.com", UserName = "alice@chapinusa.com" };
        var userManager = UserManagerMockFactory.Create();
        userManager.SetupGet(x => x.Users).Returns(new[] { snapshot }.AsQueryable());

        var sut = new IdentityEmailDomainMigration(userManager.Object, Mock.Of<ILogger<IdentityEmailDomainMigration>>());

        await sut.RunAsync();

        userManager.Verify(x => x.FindByIdAsync(It.IsAny<string>()), Times.Never);
        userManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ShouldSkipUser_WhenTargetEmailAlreadyExists()
    {
        var snapshot = new ApplicationUser { Id = "1", Email = "alice@chapinmfg.com", UserName = "alice@chapinmfg.com" };
        var live = new ApplicationUser { Id = "1", Email = "alice@chapinmfg.com", UserName = "alice@chapinmfg.com" };
        var collision = new ApplicationUser { Id = "2", Email = "alice@chapinusa.com", UserName = "alice@chapinusa.com" };
        var userManager = UserManagerMockFactory.Create();
        userManager.SetupGet(x => x.Users).Returns(new[] { snapshot }.AsQueryable());
        userManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(live);
        userManager.Setup(x => x.FindByEmailAsync("alice@chapinusa.com")).ReturnsAsync(collision);

        var sut = new IdentityEmailDomainMigration(userManager.Object, Mock.Of<ILogger<IdentityEmailDomainMigration>>());

        await sut.RunAsync();

        userManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        Assert.Equal("alice@chapinmfg.com", live.Email);
    }

    [Fact]
    public async Task RunAsync_ShouldContinue_WhenOneUserThrows()
    {
        var snapshots = new[]
        {
            new ApplicationUser { Id = "1", Email = "broken@chapinmfg.com", UserName = "broken@chapinmfg.com" },
            new ApplicationUser { Id = "2", Email = "good@chapinmfg.com", UserName = "good@chapinmfg.com" }
        };
        var goodLive = new ApplicationUser { Id = "2", Email = "good@chapinmfg.com", UserName = "good@chapinmfg.com" };
        var userManager = UserManagerMockFactory.Create();
        userManager.SetupGet(x => x.Users).Returns(snapshots.AsQueryable());
        userManager.Setup(x => x.FindByIdAsync("1")).ThrowsAsync(new InvalidOperationException("boom"));
        userManager.Setup(x => x.FindByIdAsync("2")).ReturnsAsync(goodLive);
        userManager.Setup(x => x.FindByEmailAsync("good@chapinusa.com")).ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(x => x.FindByNameAsync("good@chapinusa.com")).ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(x => x.UpdateNormalizedEmailAsync(goodLive)).Returns(Task.CompletedTask);
        userManager.Setup(x => x.UpdateNormalizedUserNameAsync(goodLive)).Returns(Task.CompletedTask);
        userManager.Setup(x => x.UpdateAsync(goodLive)).ReturnsAsync(IdentityResult.Success);

        var sut = new IdentityEmailDomainMigration(userManager.Object, Mock.Of<ILogger<IdentityEmailDomainMigration>>());

        await sut.RunAsync();

        Assert.Equal("good@chapinusa.com", goodLive.Email);
        userManager.Verify(x => x.UpdateAsync(goodLive), Times.Once);
    }
}
