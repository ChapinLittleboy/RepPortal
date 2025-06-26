using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepPortal.Models;
using RepPortal.Services;
using System.Text.Json;

namespace RepPortal.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]              // Rep must be logged in
public class InsuranceRequestController : ControllerBase
{
    private readonly IInsuranceRequestService _svc;

    public InsuranceRequestController(IInsuranceRequestService svc)
        => _svc = svc;

    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<IActionResult> Post(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        if (!form.TryGetValue("data", out var json))
            return BadRequest("Missing request payload.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var request = JsonSerializer.Deserialize<InsuranceRequest>(json!, options)
                      ?? throw new InvalidOperationException("Invalid JSON");

        var files = form.Files.ToList();
        var id = await _svc.SaveRequestAsync(request, files, ct);

        return Ok(new { id });
    }
}