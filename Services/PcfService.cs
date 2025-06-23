using Dapper;
using RepPortal.Data;
using RepPortal.Models;


namespace RepPortal.Services;

public class PcfService
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IRepCodeContext _repCodeContext;
    private readonly IConfiguration _configuration;
    private readonly CustomerService _customerService;
    private readonly ILogger<PcfService> _logger;



    public PcfService(IConfiguration configuration, AuthenticationStateProvider authenticationStateProvider,
        IRepCodeContext repCodeContext, IDbConnectionFactory dbConnectionFactory, CustomerService customerService, ILogger<PcfService> logger)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _repCodeContext = repCodeContext;
        _dbConnectionFactory = dbConnectionFactory;
        _configuration = configuration;
        _customerService = customerService;
        _logger = logger;




    }

    /*    public async Task<List<PCFHeader>> GetPCFHeadersAsync() // Uses RepCode on PCF
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
    */


    public async Task<List<PCFHeader>> GetPCFHeadersByRepCodeAsync() // Uses RepCode assigned to Customers
    {
        List<string> excludedCustomerList = await _customerService.GetExcludedCustomerListAsync();

        string query =
            @"SELECT distinct Upper(SRNum) as RepID, ProgControl.CustNum as CustomerNumber, CustName as CustomerName,
               ProgSDate as StartDate, ProgEDate as EndDate, PCFNum as PcfNumber, PCFStatus as ApprovalStatus
                ,PcfType as PcfType, cc.Eut as MarketType, BuyingGroup as BuyingGroup, SubmittedBy as SubmittedBy
                    ,cc.Salesman as Salesman, cc.SalesManager as SalesManager
               FROM ProgControl 
                left join ConsolidatedCustomers cc on ProgControl.CustNum = cc.CustNum and cc.custseq = 0
                WHERE (1 = 1 AND  progcontrol.CustNum is not null AND progcontrol.ProgSDate is not null)
                AND ProgControl.CustNum not in @ExcludedCustomerList
                AND progcontrol.ProgSDate > '2019-12-31'
        AND (@RepCode = 'Admin'    
        OR SRNum = @RepCode) 
               ORDER BY PCFNum DESC";

        _logger.LogInformation($"GetPCFHeadersByRepCodeAsync: {query}");

        using var connection = _dbConnectionFactory.CreatePcfConnection();
        var result = await connection.QueryAsync<PCFHeader>(query, new { RepCode = _repCodeContext.CurrentRepCode, ExcludedCustomerList = excludedCustomerList });
        return result.ToList();
    }

    public async Task<List<string>> GetAllowedCustomerNumbersAsync()
    {
        var repCode = _repCodeContext.CurrentRepCode;
        using var connection = _dbConnectionFactory.CreateBatConnection();
        List<string> excludedCustomerList = await _customerService.GetExcludedCustomerListAsync();

        var customerNumbers = await connection.QueryAsync<string>(
            @"SELECT DISTINCT LTRIM(RTRIM(Cust_Num)) as CustNum
              FROM Customer_mst
              WHERE  
        cust_num not in @ExcludedCustomerList AND
        (
        @RepCode = 'Admin'    
        OR slsman = @RepCode) ",
            new { RepCode = _repCodeContext.CurrentRepCode, ExcludedCustomerList = excludedCustomerList });

        _logger.LogInformation("Selected customers");
        var custlist = customerNumbers.ToList();
        foreach (var cust in custlist)
        {
            _logger.LogInformation($"Customer Number: {cust}");
        }

        return custlist;

    }

    public async Task<List<PCFHeader>> GetPCFHeadersForRepBySlsman()
    {
        List<string> allowedCustomerNumbers = await GetAllowedCustomerNumbersAsync();
        // Note if RepCode is Admin, then all customers are allowed

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
                AND PCFNum >0 and SRNum = @RepCode 
               ORDER BY ProgControl.PCFStatus, CustName DESC";


        _logger.LogInformation($"GetPCFHeadersForRepBySlsman: {query}");

        using var connection = _dbConnectionFactory.CreatePcfConnection();
        var result = await connection.QueryAsync<PCFHeader>(query, new { CustNumList = allowedCustomerNumbers, RepCode = _repCodeContext.CurrentRepCode });
        return result.ToList();
    }


    public async Task<PCFHeader> GetPCFHeaderWithItemsAsync(int pcfNum)
    {
        // This query retrieves header fields as well as the associated PCF items.
        // Note: The CAST converts PCFNum (int) to varchar so it can be compared to PCItems.PCFNumber.
        string sql = @"
        SELECT 
            h.PCFNum, 
            h.CustNum as CustomerNumber, 
            h.CustName as CustomerName, 
            h.ProgSDate as StartDate, 
            h.ProgEDate as EndDate, 
            h.PCFStatus, 
            h.PcfType, 
            h.VPSalesDate,
            h.BuyingGroup, 
            h.SubmittedBy,
            h.GenNotes as GeneralNotes,
            h.Promo_Terms_Text as PromoPaymentTermsText,
            h.Standard_Freight_Terms as PromoFreightTerms,
            h.Freight_Minimums as FreightMinimums,
            cc.SalesManager,
            cc.AddressLine1 as BillToAddress,
            cc.City as BillToCity,
            cc.State as BTState,
            cc.Zip as BTZip,
          cu.terms_code as StandardPaymentTerms,
          terms.Description as StandardPaymentTermsText,
            i.PCFNumber,
            i.ItemNum,
            it.Stat as ItemStatus, 
            i.CustNum,
            i.ItemDesc,
            i.ProposedPrice as ApprovedPrice

  
           
        FROM ProgControl h 
        LEFT JOIN PCItems i 
            ON CAST(h.PCFNum AS varchar(50)) = i.PCFNumber
        LEFT JOIN ConsolidatedCustomers cc 
            ON h.CustNum = cc.CustNum and cc.custseq = 0
        LEFT JOIN CIISQL10.Bat_App.dbo.Item_mst it on i.ItemNum = it.Item
      left join CIISQL10.Bat_App.dbo.Customer_mst cu on ltrim(rtrim(cu.cust_num)) = h.CustNum and cu.cust_seq = 0
      left join CIISQL10.BAT_App.dbo.terms_mst terms on cu.terms_code = terms.terms_code
        WHERE h.PCFNum = @PcfNum and (h.SRNum = @RepCode OR @RepCode = 'Admin')";

        _logger.LogInformation($"GetPCFHeaderWithItemsAsync: {sql}");
        using var connection = _dbConnectionFactory.CreatePcfConnection();
        var headerDict = new Dictionary<int, PCFHeader>();

        // Use Dapper's multi-mapping to group PCFHeader with its PCFItem(s)
        var result = await connection.QueryAsync<PCFHeader, PCFItem, PCFHeader>(
            sql,
            (header, item) =>
            {
                if (!headerDict.TryGetValue(header.PcfNum, out var currentHeader))
                {
                    currentHeader = header;
                    currentHeader.PCFLines = new List<PCFItem>();
                    headerDict.Add(currentHeader.PcfNum, currentHeader);
                }
                // Only add the item if it's not null
                if (item != null)
                {
                    currentHeader.PCFLines.Add(item);
                }
                return currentHeader;
            },
            new { PcfNum = pcfNum, RepCode = _repCodeContext.CurrentRepCode },
            splitOn: "PCFNumber"  // Dapper will treat PCFNumber as the start of the PCFItem mapping.
        );

        // Return the unique header (or null if not found)
        var headerResult = headerDict.Values.FirstOrDefault();
        if (headerResult != null)
        {

            var agency = await connection.QueryFirstOrDefaultAsync<string>(
                "Select name from CIISQL10.BAT_App.dbo.Chap_SlsmanNameV where slsman = @RepCode",
                new { RepCode = _repCodeContext.CurrentRepCode });

            headerResult.RepAgency = agency;
            headerResult.RepCode = _repCodeContext.CurrentRepCode;
            headerResult.RepName = $"{_repCodeContext.CurrentFirstName} {_repCodeContext.CurrentLastName}";
            if (_repCodeContext.CurrentRepCode == "Admin")
            {
                headerResult.RepAgency = "Chapin Administrator";
            }

        }

        return headerResult;
    }





}





