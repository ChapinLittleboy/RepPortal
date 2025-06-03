using Dapper;
using RepPortal.Data;
using RepPortal.Models;



namespace RepPortal.Services;
public class CreditHoldExclusionService
{

    private readonly IDbConnectionFactory _dbConnectionFactory;

    public CreditHoldExclusionService(IDbConnectionFactory DbConnectionFactory)
        => _dbConnectionFactory = DbConnectionFactory;

    public async Task<List<CreditHoldReasonCode>> GetAllAsync()
    {
        using var conn = _dbConnectionFactory.CreateRepConnection();
        var sql = "SELECT Code, Description FROM dbo.CreditHoldReasonCodeExclusions ORDER BY Code";
        return (await conn.QueryAsync<CreditHoldReasonCode>(sql)).ToList();
    }
    public async Task<List<string>> GetAllExcludedCodesAsync()
    {
        using var conn = _dbConnectionFactory.CreateRepConnection();
        var sql = "SELECT Code FROM dbo.CreditHoldReasonCodeExclusions ORDER BY Code";
        return (await conn.QueryAsync<string>(sql)).ToList();
    }

    public async Task<List<string>> GetAllExcludedCustNumsAsync()
    {

        var excludedHoldCodes = await GetAllExcludedCodesAsync();
        if (excludedHoldCodes == null || excludedHoldCodes.Count == 0)
        {
            return new List<string>();
        }

        using var bdbconn = _dbConnectionFactory.CreateBatConnection();
        var sql = "SELECT distinct Cust_Num  FROM CustAddr_mst where credit_hold_reason in @ExcludedHoldCodes ORDER BY Cust_Num";
        var parameters = new
        {

            ExcludedHoldCodes = excludedHoldCodes
        };

        var excludedCustomers = (await bdbconn.QueryAsync<string>(sql, parameters))
            .ToList();

        return excludedCustomers;


    }

    public async Task AddAsync(string code, string description)
    {
        using var conn = _dbConnectionFactory.CreateRepConnection();
        var sql = "INSERT INTO dbo.CreditHoldReasonCodeExclusions  (Code, Description) VALUES (@Code, @Description)";
        await conn.ExecuteAsync(sql, new { Code = code, Description = description });
    }

    public async Task DeleteAsync(string code)
    {
        using var conn = _dbConnectionFactory.CreateRepConnection();
        var sql = "DELETE FROM dbo.CreditHoldReasonCodeExclusions  WHERE Code = @Code";
        await conn.ExecuteAsync(sql, new { Code = code });
    }
}

