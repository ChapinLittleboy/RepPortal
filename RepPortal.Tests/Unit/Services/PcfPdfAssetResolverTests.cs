using Microsoft.AspNetCore.Hosting;
using Moq;
using RepPortal.Services;
using RepPortal.Tests.Support;

namespace RepPortal.Tests.Unit.Services;

public class PcfPdfAssetResolverTests
{
    [Fact]
    public void GetAssetPath_ShouldCombineContentRoot_ConfiguredAssetsPath_AndFileName()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(x => x.ContentRootPath).Returns(@"C:\Repos\RepPortal");
        var config = TestConfigurationFactory.Create(("PcfPdf:AssetsPath", "wwwroot/images"));
        var sut = new PcfPdfAssetResolver(env.Object, config);

        var result = sut.GetAssetPath("pdfheader.png");
        var expected = Path.GetFullPath(Path.Combine(@"C:\Repos\RepPortal", "wwwroot/images", "pdfheader.png"));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetAssetPath_ShouldAllowNestedRelativeFileNames_WithCurrentImplementation()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(x => x.ContentRootPath).Returns(@"C:\Repos\RepPortal");
        var config = TestConfigurationFactory.Create(("PcfPdf:AssetsPath", "wwwroot/images"));
        var sut = new PcfPdfAssetResolver(env.Object, config);

        var result = sut.GetAssetPath(@"subfolder\logo.png");
        var expected = Path.GetFullPath(Path.Combine(@"C:\Repos\RepPortal", "wwwroot/images", @"subfolder\logo.png"));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetAssetPath_ShouldThrow_WhenFileNameEscapesAssetDirectory()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(x => x.ContentRootPath).Returns(@"C:\Repos\RepPortal");
        var config = TestConfigurationFactory.Create(("PcfPdf:AssetsPath", "wwwroot/images"));
        var sut = new PcfPdfAssetResolver(env.Object, config);

        var ex = Assert.Throws<InvalidOperationException>(() => sut.GetAssetPath(@"..\..\secrets.txt"));

        Assert.Contains("within the configured asset directory", ex.Message);
    }

    [Fact]
    public void GetAssetPath_ShouldThrow_WhenFileNameIsBlank()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(x => x.ContentRootPath).Returns(@"C:\Repos\RepPortal");
        var config = TestConfigurationFactory.Create(("PcfPdf:AssetsPath", "wwwroot/images"));
        var sut = new PcfPdfAssetResolver(env.Object, config);

        var ex = Assert.Throws<ArgumentException>(() => sut.GetAssetPath(" "));

        Assert.Contains("required", ex.Message);
    }

    [Fact]
    public void GetAssetPath_ShouldThrow_WhenAssetsPathIsMissing()
    {
        var env = new Mock<IWebHostEnvironment>();
        env.SetupGet(x => x.ContentRootPath).Returns(@"C:\Repos\RepPortal");
        var config = TestConfigurationFactory.Create();
        var sut = new PcfPdfAssetResolver(env.Object, config);

        var ex = Assert.Throws<InvalidOperationException>(() => sut.GetAssetPath("pdfheader.png"));

        Assert.Contains("AssetsPath", ex.Message);
    }
}
