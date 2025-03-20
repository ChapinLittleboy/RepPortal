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

    public CustomerService(IConfiguration config)
    {
        _batAppConnection = config.GetConnectionString("BatAppConnection");
    }

    public async Task<IEnumerable<Customer>> GetCustomersByRepCodeAsync(string repCode)
    {
        const string sql = @"
            SELECT cu.Cust_Num , ca.Name as Cust_Name , cu.slsman as  RepCode, cu.stat as Status
            FROM Customer_mst cu
            Join CustAddr_mst ca on cu.cust_num = ca.cust_num and cu.cust_seq = ca.cust_seq 
            WHERE slsman = @RepCode and cu.cust_seq = 0
            ORDER BY ca.Name";

        using var connection = new SqlConnection(_batAppConnection);
        return await connection.QueryAsync<Customer>(sql, new { RepCode = repCode });
    }
}
