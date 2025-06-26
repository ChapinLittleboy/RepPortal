
using System.Data;
using System.IO;
using System.Text;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RepPortal.Models;
using RepPortal.Data;

namespace RepPortal.Services;

public interface IInsuranceRequestService
{
    Task<int> SaveRequestAsync(
        InsuranceRequest request,
        IList<IFormFile> attachments,
        CancellationToken ct = default);
}

public class InsuranceRequestService : IInsuranceRequestService
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<InsuranceRequestService> _logger;
    private readonly IConfiguration _config;

    private const string UploadRootFolder = "uploads/insurance";

    public InsuranceRequestService(
        IDbConnectionFactory dbFactory,
        IWebHostEnvironment env,
        IConfiguration config,
        ILogger<InsuranceRequestService> logger)
    {
        _dbFactory = dbFactory;
        _env = env;
        _config = config;
        _logger = logger;
    }

    public async Task<int> SaveRequestAsync(
        InsuranceRequest request,
        IList<IFormFile> attachments,
        CancellationToken ct = default)
    {
        // 1. Insert header row and obtain InsuranceRequestId
        int requestId;
        const string sql = """
            INSERT dbo.InsuranceRequests
                (RepCode, ExistingCustomerId, NewCustomerName, NewCustomerAddress,
                 NewCustomerContact, NewCustomerEmail, NewCustomerPhone, Notes)
            OUTPUT INSERTED.InsuranceRequestId
            VALUES (@RepCode, @ExistingCustomerId, @NewName, @NewAddr,
                    @NewContact, @NewEmail, @NewPhone, @Notes);
        """;

        using var conn = _dbFactory.CreateRepConnection();
        requestId = await conn.ExecuteScalarAsync<int>(sql, new
        {
            request.RepCode,
            request.ExistingCustomerId,
            NewName = request.NewCustomer?.Name,
            NewAddr = request.NewCustomer?.Address,
            NewContact = request.NewCustomer?.ContactName,
            NewEmail = request.NewCustomer?.Email,
            NewPhone = request.NewCustomer?.Phone,
            request.Notes
        });

        // 2. Persist each attachment under /wwwroot/uploads/insurance/{requestId}
        var saveFolder = Path.Combine(_env.WebRootPath, UploadRootFolder, requestId.ToString());
        Directory.CreateDirectory(saveFolder);

        const string attSql = """
            INSERT dbo.InsuranceRequestAttachments
                  (InsuranceRequestId, FileName, StoredPath, ContentType, SizeBytes)
            VALUES (@InsuranceRequestId, @FileName, @StoredPath, @ContentType, @SizeBytes);
        """;

        foreach (var file in attachments)
        {
            var safeFileName = Path.GetRandomFileName() + Path.GetExtension(file.FileName);
            var storedPath = Path.Combine(saveFolder, safeFileName);

            await using var fs = File.Create(storedPath);
            await file.CopyToAsync(fs, ct);

            await conn.ExecuteAsync(attSql, new
            {
                InsuranceRequestId = requestId,
                FileName = file.FileName,
                StoredPath = storedPath,
                ContentType = file.ContentType,
                SizeBytes = file.Length
            });
        }

        // 3. (Optional) Trigger email notification
        await SendEmailToFinanceAsync(requestId, request, attachments);

        return requestId;
    }

    private async Task SendEmailToFinanceAsync(
        int requestId, InsuranceRequest req, IList<IFormFile> files)
    {
        var financeAddress = _config["Finance:InsuranceRequestEmail"] ??
                             "smeyers@chapinmfg.com";

        var sb = new StringBuilder()
            .AppendLine($"New Certificate of Insurance request #{requestId}")
            .AppendLine($"Rep Code : {req.RepCode}")
            .AppendLine(req.ExistingCustomerId is null
                ? $"NEW CUSTOMER : {req.NewCustomer!.Name}"
                : $"Customer Id : {req.ExistingCustomerId}")
            .AppendLine()
            .AppendLine("Notes:")
            .AppendLine(req.Notes)
            .AppendLine()
            .AppendLine($"Attachments : {files.Count}");

         _logger.LogInformation($"[Email->Finance] {sb}");
        //  hook in your IEmailSender or equivalent
    }
}
