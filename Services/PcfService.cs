using Dapper;
using RepPortal.Data;
using RepPortal.Models;


namespace RepPortal.Services;

/// <summary>
/// Represents a service for handling PCF (Platform Configuration Framework) related operations.
/// </summary>
public class PcfService
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IRepCodeContext _repCodeContext;
    private readonly IConfiguration _configuration;
    private readonly CustomerService _customerService;
    private readonly ILogger<PcfService> _logger;



    /// <summary>
    /// Initializes a new instance of the <c>PcfService</c> class with the specified dependencies.
    /// </summary>
    /// <param name="configuration">The application configuration settings.</param>
    /// <param name="authenticationStateProvider">Provider for authentication state information.</param>
    /// <param name="repCodeContext">Context for representative code information.</param>
    /// <param name="dbConnectionFactory">Factory for creating database connections.</param>
    /// <param name="customerService">Service for handling customer-related operations.</param>
    /// <param name="logger">Logger instance for logging service operations.</param>
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


    /// <summary>
    /// Retrieves a list of distinct PCF header records for customers assigned to the current representative code, 
    /// while excluding customers specified in an excluded list. 
    /// Optionally, returns all records if the current RepCode is 'Admin'.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation, containing a list of <see cref="PCFHeader"/> objects 
    /// that match the filtering and sorting criteria.
    /// </returns>
    public async Task<List<PCFHeader>> GetPCFHeadersByRepCodeAsync() // Uses RepCode assigned to Customers
    {
        // Get the list of customer numbers that should be excluded from the query
        List<string> excludedCustomerList = await _customerService.GetExcludedCustomerListAsync();

        // Create the SQL query string to select distinct PCF headers
        string query =
            @"SELECT distinct Upper(SRNum) as RepID,                   -- Representative ID (uppercase)
             ProgControl.CustNum as CustomerNumber,            -- Customer number
             CustName as CustomerName,                         -- Customer name
             ProgSDate as StartDate,                           -- Program start date
             ProgEDate as EndDate,                             -- Program end date
             PCFNum as PcfNumber,                              -- PCF number
             PCFStatus as ApprovalStatus,                      -- PCF approval status
             PcfType as PcfType,                               -- PCF type
             cc.Eut as MarketType,                             -- Market type from ConsolidatedCustomers
             BuyingGroup as BuyingGroup,                       -- Buying group
             SubmittedBy as SubmittedBy,                       -- User who submitted the PCF
             cc.Salesman as Salesman,                          -- Associated salesman
             cc.SalesManager as SalesManager                   -- Associated sales manager
      FROM ProgControl 
      left join ConsolidatedCustomers cc 
        on ProgControl.CustNum = cc.CustNum 
        and cc.custseq = 0
      WHERE (1 = 1                                        -- Always true; allows for easier query modifications
             AND progcontrol.CustNum is not null           -- Ensure customer number is present
             AND progcontrol.ProgSDate is not null)        -- Ensure program start date is present
        AND ProgControl.CustNum not in @ExcludedCustomerList  -- Filter out excluded customers
        AND progcontrol.ProgSDate > '2019-12-31'           -- Only include programs starting after 2019
        AND (@RepCode = 'Admin'                            -- Allow admin to see all records,
             OR SRNum = @RepCode)                          -- otherwise filter by sales rep code
      ORDER BY PCFNum DESC"; // Order results by the most recent PCF number

        // Log the query for debugging or informational purposes
        _logger.LogInformation($"GetPCFHeadersByRepCodeAsync: {query}");

        // Create the SQL connection using the database connection factory
        using var connection = _dbConnectionFactory.CreatePcfConnection();

        // Execute the query asynchronously, passing RepCode and ExcludedCustomerList as SQL parameters, and map to PCFHeader objects
        var result = await connection.QueryAsync<PCFHeader>(
            query,
            new { RepCode = _repCodeContext.CurrentRepCode, ExcludedCustomerList = excludedCustomerList }
        );

        // Convert the result to a list and return it
        return result.ToList();
    }

    /// <summary>
    /// Asynchronously retrieves a distinct list of allowed customer numbers from the database,
    /// excluding those in the provided excluded customer list and filtered by the current rep code.
    /// If the user is 'Admin', all customers except the excluded ones are returned.
    /// Logs the process and each returned customer number.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation, with a list of allowed customer numbers as result.
    /// </returns>
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

    /// <summary>
    /// Retrieves a list of PCFHeader records for the current sales representative,
    /// filtered by allowed customer numbers and relevant criteria.
    /// If the user is an admin, all customers are included.
    /// Results are ordered by PCF status and customer name.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation, containing a list of <see cref="PCFHeader"/> objects
    /// corresponding to the permitted customers and current representative code.
    /// </returns>
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


    /// <summary>
    /// Asynchronously retrieves a <see cref="PCFHeader"/> record along with its associated <see cref="PCFItem"/> child lines from the database for the specified PCF number.
    /// This method performs a join between PCF header, items, customer, and related tables. Supports multi-mapping with Dapper to group line items under the header.
    /// Optionally applies a sales rep code filter unless 'Admin' is the rep code, in which case all results for the PCF number are included.
    /// Appends sales agency and representative info to the result.
    /// </summary>
    /// <param name="pcfNum">The PCF number for which to retrieve header and line items.</param>
    /// <returns>
    /// A <see cref="PCFHeader"/> object containing populated header fields and a collection of linked <see cref="PCFItem"/> objects (if present), 
    /// or <c>null</c> if not found.
    /// </returns>
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
        WHERE h.PCFNum = @PcfNum and (h.SRNum = @RepCode OR @RepCode = 'Admin' OR (
                @RepCode = 'DAL'
                AND (
                        h.SRNum = @RepCode
                        OR h.CustNum IN ('  45424', '  45427', '  45424K', '45424', '45427', '45424K')
                   )
           )) ";

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