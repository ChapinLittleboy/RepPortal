using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.SqlClient;
using RepPortal.Data;
using RepPortal.Models;
using Syncfusion.XlsIO.Implementation.XmlSerialization;
using System.Data;


namespace RepPortal.Services;

public class PcfService
{
    private readonly DbConnectionFactory _dbConnectionFactory;
    //private readonly string _batConnectionString;
   // private readonly string _pcfConnectionString;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IRepCodeContext _repCodeContext;
    private readonly IConfiguration _configuration;
    private readonly CustomerService _customerService;


    public PcfService(IConfiguration configuration, AuthenticationStateProvider authenticationStateProvider,
        IRepCodeContext repCodeContext, DbConnectionFactory dbConnectionFactory, CustomerService customerService)
    {
        //_batConnectionString = configuration.GetConnectionString("BatAppConnection");
        //_pcfConnectionString = configuration.GetConnectionString("PcfConnection");
        _authenticationStateProvider = authenticationStateProvider;
        _repCodeContext = repCodeContext;
        _dbConnectionFactory = dbConnectionFactory;
        _configuration = configuration;
        _customerService = customerService;


    }

    public async Task<List<PCFHeader>> GetPCFHeadersAsync()
    {
        string query =
            @"SELECT distinct Upper(SRNum) as RepID, ProgControl.CustNum as CustomerNumber, CustName as CustomerName,
               ProgSDate as StartDate, ProgEDate as EndDate, PCFNum as PcfNumber, PCFStatus as ApprovalStatus
                ,PcfType as PcfType, cc.Eut as MarketType, BuyingGroup as BuyingGroup, SubmittedBy as SubmittedBy
                    ,cc.Salesman as Salesman, cc.SalesManager as SalesManager
               FROM ProgControl --join UserCustomerAccesses uca on ProgControl.CustNum = uca.CustNum
                left join ConsolidatedCustomers cc on ProgControl.CustNum = cc.CustNum and cc.custseq = 0
                WHERE (1 = 1 AND  progcontrol.CustNum is not null AND progcontrol.ProgSDate is not null)
                AND progcontrol.ProgSDate > '2019-12-31'

               ORDER BY PCFNum DESC";

        using var connection = _dbConnectionFactory.CreatePcfConnection();
        var result = await connection.QueryAsync<PCFHeader>(query, new { RepCode = _repCodeContext.CurrentRepCode });
        return result.ToList();
    }


    public async Task<IEnumerable<PCFHeader>> GetPCFsForCurrentRepAsync()
    {
        var customers = await _customerService.GetCustomersByRepCodeAsync();
        var customerNumbers = customers.Select(c => c.Cust_Num.Trim()).ToList();  // NOTE THE TRIMMING!

        using var connection = _dbConnectionFactory.CreatePcfConnection();
        const string sql = @"
        SELECT *
        FROM PCF
        WHERE CustomerNumber IN @CustomerNumbers"
        ;

        return await connection.QueryAsync<PCFHeader>(sql, new { CustomerNumbers = customerNumbers });
    }



}





