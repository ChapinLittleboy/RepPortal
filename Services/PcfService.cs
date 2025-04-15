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
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IRepCodeContext _repCodeContext;
    private readonly IConfiguration _configuration;
    private readonly CustomerService _customerService;


    public PcfService(IConfiguration configuration, AuthenticationStateProvider authenticationStateProvider,
        IRepCodeContext repCodeContext, DbConnectionFactory dbConnectionFactory, CustomerService customerService)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _repCodeContext = repCodeContext;
        _dbConnectionFactory = dbConnectionFactory;
        _configuration = configuration;
        _customerService = customerService;


    }

    public async Task<List<PCFHeader>> GetPCFHeadersAsync()   // Uses RepCode on PCF
    {
        string query =
            @"SELECT distinct Upper(SRNum) as RepID, ProgControl.CustNum as CustomerNumber, CustName as CustomerName,
               ProgSDate as StartDate, ProgEDate as EndDate, PCFNum as PcfNumber, PCFStatus as ApprovalStatus
                ,PcfType as PcfType, cc.Eut as MarketType, BuyingGroup as BuyingGroup, SubmittedBy as SubmittedBy
                    ,cc.Salesman as Salesman, cc.SalesManager as SalesManager
               FROM ProgControl 
                left join ConsolidatedCustomers cc on ProgControl.CustNum = cc.CustNum and cc.custseq = 0
                WHERE (1 = 1 AND  progcontrol.CustNum is not null AND progcontrol.ProgSDate is not null)
                AND progcontrol.ProgSDate > '2019-12-31'

               ORDER BY PCFNum DESC";

        using var connection = _dbConnectionFactory.CreatePcfConnection();
        var result = await connection.QueryAsync<PCFHeader>(query, new { RepCode = _repCodeContext.CurrentRepCode });
        return result.ToList();
    }


   
    public async Task<List<PCFHeader>> GetPCFHeadersByRepCodeAsync()  // Uses RepCode assigned to Customers
    {
        string query =
            @"SELECT distinct Upper(SRNum) as RepID, ProgControl.CustNum as CustomerNumber, CustName as CustomerName,
               ProgSDate as StartDate, ProgEDate as EndDate, PCFNum as PcfNumber, PCFStatus as ApprovalStatus
                ,PcfType as PcfType, cc.Eut as MarketType, BuyingGroup as BuyingGroup, SubmittedBy as SubmittedBy
                    ,cc.Salesman as Salesman, cc.SalesManager as SalesManager
               FROM ProgControl 
                left join ConsolidatedCustomers cc on ProgControl.CustNum = cc.CustNum and cc.custseq = 0
                WHERE (1 = 1 AND  progcontrol.CustNum is not null AND progcontrol.ProgSDate is not null)
                AND progcontrol.ProgSDate > '2019-12-31'
                AND SRNum = @RepCode
               ORDER BY PCFNum DESC";
        using var connection = _dbConnectionFactory.CreatePcfConnection();
        var result = await connection.QueryAsync<PCFHeader>(query, new { RepCode = _repCodeContext.CurrentRepCode });
        return result.ToList();
    }

    public async Task<List<string>> GetAllowedCustomerNumbersAsync()
    {
        var repCode = _repCodeContext.CurrentRepCode;
        using var connection = _dbConnectionFactory.CreateBatConnection();

        var customerNumbers = await connection.QueryAsync<string>(
            @"SELECT DISTINCT LTRIM(RTRIM(Cust_Num)) as CustNum
              FROM Customer_mst
              WHERE slsman = @RepCode",
            new { RepCode = repCode });

        var custlist = customerNumbers.ToList();

        return custlist;

    }

    public async Task<List<PCFHeader>> GetPCFHeadersForRepBySlsman()
    {
        List<string> allowedCustomerNumbers = await GetAllowedCustomerNumbersAsync();

        string query =
            @"SELECT distinct Upper(SRNum) as RepCode, ProgControl.CustNum as CustomerNumber, ProgControl.CustName as CustomerName,
               ProgSDate as StartDate, ProgEDate as EndDate, PCFNum as PcfNumber, PCFStatus
                ,PcfType as PcfType, cc.Eut as MarketType, BuyingGroup as BuyingGroup, SubmittedBy as SubmittedBy
                    ,cc.Salesman as Salesman, cc.SalesManager as SalesManager
               FROM ProgControl 
                left join ConsolidatedCustomers cc on ProgControl.CustNum = cc.CustNum and cc.custseq = 0
                WHERE (1 = 1 AND  ProgControl.CustNum is not null AND ProgControl.ProgSDate is not null)
                AND ProgControl.ProgSDate > '2019-12-31'
                AND ProgControl.CustNum in @CustNumList
                AND PCFNum >0
               ORDER BY ProgControl.PCFStatus, CustName DESC";
        using var connection = _dbConnectionFactory.CreatePcfConnection();
        var result = await connection.QueryAsync<PCFHeader>(query, new {  CustNumList = allowedCustomerNumbers });
        return result.ToList();
    }

}





