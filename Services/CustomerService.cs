namespace RepPortal.Services;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RepPortal.Models;
using System.Collections.Generic;
using System.Threading.Tasks;


public class CustomerService
{
    private readonly string _batAppConnection;
    private readonly IRepCodeContext _repCodeContext;
    //private readonly CreditHoldExclusionService _creditHoldExclusionService;

    public CustomerService(IConfiguration config, IRepCodeContext repCodeContext, CreditHoldExclusionService creditHoldExclusionService)
    {
        _batAppConnection = config.GetConnectionString("BatAppConnection");
        _repCodeContext = repCodeContext;
        //_creditHoldExclusionService = creditHoldExclusionService;  // gets list from RepPortal.dbo.CreditHoldReasonCodeExceptions
    }



    public async Task<IEnumerable<Customer>> GetCustomersByRepCodeAsync()
    {
        const string sql = @"
            SELECT cu.Cust_Num , ca.Name as Cust_Name , cu.slsman as  RepCode, cu.stat as Status,
            cu.uf_c_slsmgr as SalesManager, sm.SalesManagerName as SalesManagerName
            FROM Customer_mst cu
            Join CustAddr_mst ca on cu.cust_num = ca.cust_num and cu.cust_seq = ca.cust_seq 
            Join CustAddr_mst ca0 on cu.cust_num = ca.cust_num and  ca.cust_seq = 0
            left join Chap_SalesManagers sm on cu.uf_c_slsmgr = sm.SalesManagerInitials
           WHERE 
    cu.cust_seq = 0
AND ca0.credit_hold_reason not in (Select Code from RepPortal.dbo.CreditHoldReasonCodeExclusions)
    AND (
        @RepCode = 'Admin'    
        OR cu.slsman = @RepCode)";  // if Admin, include all customers otherwise only their own
   

        using var connection = new SqlConnection(_batAppConnection);
        return await connection.QueryAsync<Customer>(sql, new { RepCode = _repCodeContext.CurrentRepCode });
        // NOTE: Using the repCode from the RepCodeContext!!
    }

    public async Task<IEnumerable<Customer>> GetCustomersDetailsByRepCodeAsync()
    {
        // NOTE:  Always uses the repCode from the RepCodeContext
        const string sql = @"
            SELECT cu.cust_num as Cust_Num, ca.[name] as Cust_Name, ca.corp_cust as Corp_Cust,
       ca.addr##1 as BillToAddress1, ca.addr##2 as BillToAddress2, ca.addr##3 as BillToAddress3,
       ca.addr##4 as BillToAddress4, ca.city as BillToCity, ca.state as BillToState,
       ca.zip as BillToZip, ca.country as BillToCountry, cu.slsman as RepCode, 
       ca.credit_hold as CreditHold, ca.credit_hold_date as CreditHoldDate, ca.credit_hold_reason as CreditHoldReason,
       cu.terms_code as PaymentTermsCode, cu.Uf_PROG_BASIS as PricingCode, cu.stat as Status,
        cu.uf_c_slsmgr as SalesManager, sm.SalesManagerName as SalesManagerName
FROM   customer_mst cu 
JOIN custaddr_mst ca ON cu.cust_num = ca.cust_num AND cu.cust_seq = ca.cust_seq AND cu.cust_seq = 0
LEFT JOIN Chap_SalesManagers sm ON cu.slsman = sm.SalesManagerInitials
WHERE  1=1
AND ca.credit_hold_reason not in (Select Code from RepPortal.dbo.CreditHoldReasonCodeExclusions)
AND (        @RepCode = 'Admin'    
        OR cu.slsman = @RepCode) 
ORDER BY ca.[name]";

        using var connection = new SqlConnection(_batAppConnection);
        return await connection.QueryAsync<Customer>(sql, new { RepCode = _repCodeContext.CurrentRepCode});
        // NOTE: Using the repCode from the RepCodeContext!!
    }


    public async Task<IEnumerable<string>> GetCustomerTypesAsync()
    {
        // NOTE:  Always uses the repCode from the RepCodeContext
        const string sql = @"
            SELECT Distinct cust_type as CustomerType
FROM   customer_mst cu 
WHERE   (
        @RepCode = 'Admin'    
        OR cu.slsman = @RepCode) ORDER BY cust_type";

        using var connection = new SqlConnection(_batAppConnection);
        return await connection.QueryAsync<string>(sql, new { RepCode = _repCodeContext.CurrentRepCode });
        // NOTE: Using the repCode from the RepCodeContext!!
    }

    public async Task<List<string>> GetExcludedCustomerListAsync()
    {
        const string sql = @"
        SELECT DISTINCT cust_num 
        FROM custaddr_mst 
        WHERE credit_hold_reason IN (
            SELECT Code 
            FROM RepPortal.dbo.CreditHoldReasonCodeExclusions
        )";

        using var connection = new SqlConnection(_batAppConnection);
        var results = await connection.QueryAsync<string>(sql);
        return results.ToList();
    }
}
