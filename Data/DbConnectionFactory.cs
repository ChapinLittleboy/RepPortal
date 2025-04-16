using Microsoft.Data.SqlClient;
using System.Data;

namespace RepPortal.Data;

public class DbConnectionFactory
{
    private readonly IConfiguration _configuration;
    private readonly string _batConnectionString;
    private readonly string _pcfConnectionString;
    private readonly string _repConnectionString;

    //private readonly DbConnectionFactory _dbConnectionFactory;




    public DbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;


        _pcfConnectionString = _configuration.GetConnectionString("PcfConnection");
        _batConnectionString = _configuration.GetConnectionString("BatAppConnection");
        _repConnectionString = _configuration.GetConnectionString("RepPortalConnection");


    }

    // Methods to create a connections
    public IDbConnection CreateBatConnection() => new SqlConnection(_batConnectionString);
    public IDbConnection CreatePcfConnection() => new SqlConnection(_pcfConnectionString);
    public IDbConnection CreateRepConnection() => new SqlConnection(_repConnectionString);



}

