using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RepPortal.Controllers;
using RepPortal.Models;
using RepPortal.Services;
using RepPortal.Tests.Support;

namespace RepPortal.Tests.Unit.Controllers;

public class InsuranceRequestControllerTests
{
    [Fact]
    public async Task Post_ShouldReturnBadRequest_WhenDataFieldIsMissing()
    {
        var service = new Mock<IInsuranceRequestService>();
        var controller = CreateController(service);
        TestControllerFormFactory.SetForm(controller.HttpContext);

        var result = await controller.Post(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Missing request payload.", badRequest.Value);
    }

    [Fact]
    public async Task Post_ShouldReturnOkWithId_WhenPayloadIsValid()
    {
        var service = new Mock<IInsuranceRequestService>();
        service
            .Setup(x => x.SaveRequestAsync(It.IsAny<InsuranceRequest>(), It.IsAny<IList<IFormFile>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(123);

        var controller = CreateController(service);
        var payload = JsonSerializer.Serialize(new InsuranceRequest
        {
            RepCode = "REP1",
            ExistingCustomerId = "C123",
            Notes = "Need COI",
            NewCustomer = new NewCustomerInfo { Name = "Acme" }
        });
        var files = new FormFileCollection
        {
            (FormFile)TestControllerFormFactory.CreateFile("proof.txt", "hello")
        };

        TestControllerFormFactory.SetForm(
            controller.HttpContext,
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["data"] = payload },
            files);

        var result = await controller.Post(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("123", json);
        service.Verify(
            x => x.SaveRequestAsync(
                It.Is<InsuranceRequest>(r => r.RepCode == "REP1" && r.ExistingCustomerId == "C123"),
                It.Is<IList<IFormFile>>(f => f.Count == 1 && f[0].FileName == "proof.txt"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TestableInsuranceRequestController CreateController(Mock<IInsuranceRequestService> service)
    {
        var controller = new TestableInsuranceRequestController(service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private sealed class TestableInsuranceRequestController : InsuranceRequestController
    {
        public TestableInsuranceRequestController(IInsuranceRequestService svc) : base(svc)
        {
        }

        public new HttpContext HttpContext => ControllerContext.HttpContext;
    }
}
