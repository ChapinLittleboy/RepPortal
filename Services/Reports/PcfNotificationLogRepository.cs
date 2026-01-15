using Dapper;
using Microsoft.Data.SqlClient;
using RepPortal.Data;
using RepPortal.Models;
using static RepPortal.Models.PcfExpirationNotice;


namespace RepPortal.Services.Reports;

public interface IPcfNotificationLogRepository
{
    Task<bool> ExistsAsync(
        int pcfId,
        PcfNoticeType noticeType,
        DateTime expirationDate);

    Task InsertAsync(
        int pcfId,
        PcfNoticeType noticeType,
        DateTime expirationDate,
        string sentTo);
}



public sealed class PcfNotificationLogRepository : IPcfNotificationLogRepository
{
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<PcfNotificationLogRepository> _logger;

    public PcfNotificationLogRepository(
        IDbConnectionFactory dbFactory,
        ILogger<PcfNotificationLogRepository> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<bool> ExistsAsync(
        int pcfId,
        PcfNoticeType noticeType,
        DateTime expirationDate)
    {
        const string sql = @"
SELECT 1
FROM dbo.PcfNotificationLog
WHERE PcfId = @PcfId
  AND NoticeType = @NoticeType
  AND ExpirationDate = @ExpirationDate;
";

        using var conn = _dbFactory.CreateRepConnection();

        //await conn.Open();

        var result = await conn.ExecuteScalarAsync<int?>(
            sql,
            new
            {
                PcfId = pcfId,
                NoticeType = noticeType.ToString(), // 'Day30', 'Day15' or map if you prefer
                ExpirationDate = expirationDate.Date
            });

        return result.HasValue;
    }

    public async Task InsertAsync(
        int pcfId,
        PcfNoticeType noticeType,
        DateTime expirationDate,
        string sentTo)
    {
        const string sql = @"
INSERT INTO dbo.PcfNotificationLog
(
    PcfId,
    NoticeType,
    ExpirationDate,
    SentOnDate,
    SentTo
)
VALUES
(
    @PcfId,
    @NoticeType,
    @ExpirationDate,
    getdate(),
    @SentTo
);
";

        using var conn = _dbFactory.CreateRepConnection();
        //await conn.OpenAsync();

        try
        {
            await conn.ExecuteAsync(
                sql,
                new
                {
                    PcfId = pcfId,
                    NoticeType = noticeType.ToString(),
                    ExpirationDate = expirationDate.Date,
                    SentTo = sentTo
                });
        }
        catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
        {
            // Primary key violation — another run already inserted it
            _logger.LogInformation(
                "PCF notification already logged. PCF={PcfId}, Notice={NoticeType}, Expiration={ExpirationDate}",
                pcfId, noticeType, expirationDate);

            // Swallow — this is expected in retry scenarios
        }
    }
}
