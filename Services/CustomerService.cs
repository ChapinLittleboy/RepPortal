namespace RepPortal.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using RepPortal.Models;


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



    public async Task<IEnumerable<Customer>> GetCustomersByRepCodeAsync()  // Not used as of 4-25-2025
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
AND (ca.credit_hold_reason IS NULL OR ca.credit_hold_reason NOT IN (
    SELECT Code FROM RepPortal.dbo.CreditHoldReasonCodeExclusions))
    AND (
        -- Admin gets everything
        @RepCode = 'Admin'

        -- DAL gets their normal customers + special customer list
        OR (
                @RepCode = 'DAL'
                AND (
                        cu.slsman = @RepCode
                        OR cu.cust_num IN ('  45424', '  45427', '  45424K', '45424', '45427', '45424K')
                   )
           )

        -- All other reps get only customers matching their rep code
        OR (
                @RepCode NOT IN ('Admin', 'DAL')
                AND cu.slsman = @RepCode
           )
    )";  // if Admin, include all customers otherwise only their own


        using var connection = new SqlConnection(_batAppConnection);
        return await connection.QueryAsync<Customer>(sql, new { RepCode = _repCodeContext.CurrentRepCode });
        // NOTE: Using the repCode from the RepCodeContext!!
    }

    public async Task<List<Customer>> GetCustomersDetailsByRepCodeAsync()  // Cannot do Region as region is ship-to specific
    {
        // NOTE:  Always uses the repCode from the RepCodeContext
        const string sql = @"
            SELECT cu.cust_num as Cust_Num, ca.[name] as Cust_Name, ca.corp_cust as Corp_Cust,
       ca.addr##1 as BillToAddress1, ca.addr##2 as BillToAddress2, ca.addr##3 as BillToAddress3,
       ca.addr##4 as BillToAddress4, ca.city as BillToCity, ca.state as BillToState,
       ca.zip as BillToZip, ca.country as BillToCountry, cu.slsman as RepCode, 
       ca.credit_hold as CreditHold, ca.credit_hold_date as CreditHoldDate, ca.credit_hold_reason as CreditHoldReason,
       cu.terms_code as PaymentTermsCode, cu.Uf_PROG_BASIS as PricingCode, cu.stat as Status,
        cu.uf_c_slsmgr as SalesManager, isnull(sm.SalesManagerName,'To Be Assigned') as SalesManagerName
         ,isnull(r.Description,'') as CreditHoldReasonDescription 
,cu.uf_FrtTerms as FreightTerms
FROM   customer_mst cu 
JOIN custaddr_mst ca ON cu.cust_num = ca.cust_num AND cu.cust_seq = ca.cust_seq AND cu.cust_seq = 0
left JOIN reason_mst r ON ca.credit_hold_reason = r.reason_code and r.reason_class = 'CRED HOLD'
LEFT JOIN Chap_SalesManagers sm ON cu.uf_c_slsmgr = sm.SalesManagerInitials
WHERE  1=1
AND (ca.credit_hold_reason IS NULL OR ca.credit_hold_reason NOT IN (
    SELECT Code FROM RepPortal.dbo.CreditHoldReasonCodeExclusions))
AND (
        -- Admin gets everything
        @RepCode = 'Admin'

        -- DAL gets their normal customers + special customer list
        OR (
                @RepCode = 'DAL'
                AND (
                        cu.slsman = @RepCode
                        OR cu.cust_num IN ('  45424', '  45427', '  45424K', '45424', '45427', '45424K')
                   )
           )

        -- All other reps get only customers matching their rep code
        OR (
                @RepCode NOT IN ('Admin', 'DAL')
                AND cu.slsman = @RepCode
           )
    )
ORDER BY ca.[name]";

        using var connection = new SqlConnection(_batAppConnection);
        var Results = await connection.QueryAsync<Customer>(sql, new { RepCode = _repCodeContext.CurrentRepCode });
        return Results.ToList();
        // NOTE: Using the repCode from the RepCodeContext!!
    }




    public async Task<List<Customer>> GetCustomerNamesByRepCodeAsync()  // customer name, number, rep_code, status, region from ship-to records
    {
        // NOTE:  Always uses the repCode from the RepCodeContext
        const string sql = @"
            SELECT distinct cu.cust_num as Cust_Num, ca0.[name] as Cust_Name,  cu.slsman as RepCode, cu.stat as Status
,       ca0.corp_cust as Corp_Cust,
       ca0.addr##1 as BillToAddress1, ca0.addr##2 as BillToAddress2, ca0.addr##3 as BillToAddress3,
       ca0.addr##4 as BillToAddress4, ca0.city as BillToCity, ca0.state as BillToState,
       ca0.zip as BillToZip, ca0.country as BillToCountry, cu.slsman as RepCode, 
       ca0.credit_hold as CreditHold, ca0.credit_hold_date as CreditHoldDate, ca0.credit_hold_reason as CreditHoldReason,
       cu0.terms_code as PaymentTermsCode, cu0.Uf_PROG_BASIS as PricingCode, cu.stat as Status,
        cu0.uf_c_slsmgr as SalesManager, isnull(sm.SalesManagerName,'To Be Assigned') as SalesManagerName
         ,isnull(r.Description,'') as CreditHoldReasonDescription 
,cu0.uf_FrtTerms as FreightTerms
,ct.cust_type as BuyingGroup, ct.Description as BuyingGroupDescription
            FROM   customer_mst cu 
            JOIN customer_mst cu0 ON cu.cust_num = cu0.cust_num AND cu0.cust_seq = 0
JOIN custaddr_mst ca0 ON cu.cust_num = ca0.cust_num AND ca0.cust_seq  = 0
left JOIN reason_mst r ON ca0.credit_hold_reason = r.reason_code and r.reason_class = 'CRED HOLD'
LEFT JOIN Chap_SalesManagers sm ON cu0.uf_c_slsmgr = sm.SalesManagerInitials
LEFT JOIN custtype_mst ct ON cu0.cust_type = ct.cust_type
            WHERE  1=1
AND (ca0.credit_hold_reason IS NULL OR ca0.credit_hold_reason NOT IN (
    SELECT Code FROM RepPortal.dbo.CreditHoldReasonCodeExclusions))
AND (
        -- Admin gets everything
        @RepCode = 'Admin'

        -- DAL gets their normal customers + special customer list
        OR (
                @RepCode = 'DAL'
                AND (
                        cu0.slsman = @RepCode
                        OR cu0.cust_num IN ('  45424', '  45427', '  45424K', '45424', '45427', '45424K')
                   )
           )

        -- All other reps get only customers matching their rep code
        OR (
                @RepCode NOT IN ('Admin', 'DAL')
                AND cu0.slsman = @RepCode
           )
    )
ORDER BY ca0.[name]";

        using var connection = new SqlConnection(_batAppConnection);
        var Results = await connection.QueryAsync<Customer>(sql, new { RepCode = _repCodeContext.CurrentRepCode });
        return Results.ToList();
        // NOTE: Using the repCode from the RepCodeContext!!
    }


    public async Task<IEnumerable<string>> GetCustomerTypesAsync()
    {
        // NOTE:  Always uses the repCode from the RepCodeContext
        const string sql = @"
            SELECT Distinct cu.cust_type as CustomerType
FROM   customer_mst cu 
join RepPortal.dbo.CustTypes rct on cu.cust_type= rct.Cust_Type
WHERE   (
        @RepCode = 'Admin'

        -- DAL gets their normal customers + special customer list
        OR (
                @RepCode = 'DAL'
                AND (
                        cu.slsman = @RepCode
                        OR cu.cust_num IN ('  45424', '  45427', '  45424K', '45424', '45427', '45424K')
                   )
           )

        -- All other reps get only customers matching their rep code
        OR (
                @RepCode NOT IN ('Admin', 'DAL')
                AND cu.slsman = @RepCode
           ) 
        )

ORDER BY cu.cust_type";

        using var connection = new SqlConnection(_batAppConnection);
        return await connection.QueryAsync<string>(sql, new { RepCode = _repCodeContext.CurrentRepCode });
        // NOTE: Using the repCode from the RepCodeContext!!
    }

    public async Task<IEnumerable<CustType>> GetCustomerTypesListAsync()
    {
        // NOTE:  Always uses the repCode from the RepCodeContext
        const string sql = @"
            SELECT Distinct cu.cust_type as CustomerType, rct.Description as CustTypeName
FROM   customer_mst cu 
join RepPortal.dbo.CustTypes rct on cu.cust_type= rct.Cust_Type
WHERE   (
        @RepCode = 'Admin'

        -- DAL gets their normal customers + special customer list
        OR (
                @RepCode = 'DAL'
                AND (
                        cu.slsman = @RepCode
                        OR cu.cust_num IN ('  45424', '  45427', '  45424K', '45424', '45427', '45424K')
                   )
           )

        -- All other reps get only customers matching their rep code
        OR (
                @RepCode NOT IN ('Admin', 'DAL')
                AND cu.slsman = @RepCode
           )
    )


ORDER BY cu.cust_type";

        using var connection = new SqlConnection(_batAppConnection);
        return await connection.QueryAsync<CustType>(sql, new { RepCode = _repCodeContext.CurrentRepCode });
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

    public async Task<List<CreditHoldReasonCode>> GetAllReasonCodesAsync()
    {
        const string sql = @"Select reason_code as Code, Description from Reason_mst where reason_class = 'CRED HOLD'";
        using var connection = new SqlConnection(_batAppConnection);
        var results = await connection.QueryAsync<CreditHoldReasonCode>(sql);
        return results.ToList();


    }
}
