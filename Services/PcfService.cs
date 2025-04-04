using Dapper;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Data.SqlClient;
using RepPortal.Models;
using System.Data;


namespace RepPortal.Services;

public class PcfService
{
    private readonly string _batConnectionString;
    private readonly string _pcfConnectionString;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IRepCodeContext _repCodeContext;


    public PcfService(IConfiguration configuration, AuthenticationStateProvider authenticationStateProvider,
        IRepCodeContext repCodeContext)
    {
        _batConnectionString = configuration.GetConnectionString("BatAppConnection");
        _pcfConnectionString = configuration.GetConnectionString("PcfConnection");
        _authenticationStateProvider = authenticationStateProvider;
        _repCodeContext = repCodeContext;

    }

    public async Task<List<PCFHeaderDTO>> GetPCFHeadersAsync(string status)
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
               ORDER BY PCFNum DESC"
            ;


        //var parameters = new { CurrentDate = DateTime.Now.Date};
        using var connection = new SqlConnection(_pcfConnectionString);
        return await connection.QueryAsync<PCFHeaderDTO>(query, new { RepCode = _repCodeContext.CurrentRepCode });
        // NOTE: Using the repCode from the RepCodeContext!!
       // using var connection = _dbConnectionFactory.CreateReadWriteConnection(_userService.CurrentPCFDatabaseName);
       // return (await connection.QueryAsync<PCFHeaderDTO>(query)).ToList();
    }





}





