using System.ComponentModel;
using Microsoft.IdentityModel.Tokens;

namespace RepPortal.Models;

public class PCFHeader : INotifyPropertyChanged
{
    #region Private Fields
    private readonly IHttpContextAccessor _httpContextAccessor;
    #endregion

    #region Constructors
    // Parameterless constructor for Dapper
    public PCFHeader()
    {
    }
    #endregion

    #region Primary Identifiers
    public int PcfNum { get; set; }

    public string? PcfNumber
    {
        get => PcfNum.ToString();
        set
        {
            if (int.TryParse(value, out int result))
            {
                PcfNum = result;
            }
        }
    }

    public string? DisplayNumAndDates => $"PCF {PcfNumber} Dates {StartDate:MM-dd-yyyy} to {EndDate:MM-dd-yyyy}";
    #endregion

    #region Customer Information
    public string? CustomerNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; } = string.Empty;
    public string? BuyingGroup { get; set; } = string.Empty;
    public Customer CustomerInfo { get; set; }
    public string? CustContact { get; set; }
    public string? CustContactEmail { get; set; }
    #endregion

    #region Billing Information
    public string? BillToAddress { get; set; } = string.Empty;
    public string? BillToCity { get; set; } = string.Empty;
    public string? BTState { get; set; } = string.Empty;
    public string? BTZip { get; set; } = string.Empty;
    public string? BillToCountry { get; set; } = string.Empty;
    public string? BillToPhone { get; set; } = string.Empty;
    #endregion

    #region Date Information
    public DateTime DateEntered { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime? LastEditDate { get; set; }
    public DateTime? LastUpdatedDate { get; set; }
    public DateTime SubmittedDate { get; set; }
    #endregion
    #region PCF Configuration
    public string? PcfType { get; set; }

    public string? PCFTypeDescription
    {
        get
        {
            if (!PcfType.IsNullOrEmpty())
            {
                // If the dictionary contains your code, return the matching description
                if (PcfTypeDescriptions.TryGetValue(PcfType, out string description))
                {
                    return description;
                }
            }
            // Otherwise, return something default or empty
            return "Unknown";
        }
    }

    public string? MarketType
    {
        get => EUT;
        set => EUT = value;
    }

    public string? EUT { get; set; }
    public string? CmaRef { get; set; }
    #endregion

    #region Payment and Shipping Terms
    public List<PaymentTerm> PaymentTermsList { get; set; } // Inject the list
    public string PromoPaymentTermsDescription =>
        PaymentTermsList?.FirstOrDefault(t => t.Terms_Code == PromoPaymentTerms)?.Description ?? "";


    public string? StandardPaymentTerms { get; set; }  // This is the Terms code

    public string? StandardPaymentTermsText { get; set; }

    //public string StandardPaymentTermsDescription =>
   //     PaymentTermsList?.FirstOrDefault(t => t.Terms_Code == StandardPaymentTermsType)?.Description ?? "";

    public string? PromoPaymentTerms { get; set; }
    public string? PromoPaymentTermsText { get; set; }
    public string? PromoFreightTerms { get; set; }
    public string? PromoFreightMinimums { get; set; }
    public string? FreightTerms { get; set; }
    public string? FreightMinimums { get; set; }
    #endregion

    #region Representative Information
    public string? RepCode { get; set; } = string.Empty;
    public string? RepName { get; set; } = string.Empty;
    public string? RepEmail { get; set; } = string.Empty;
    public string? RepAgency { get; set; } = string.Empty;
    public string? RepPhone { get; set; } = string.Empty;
    public string? SalesMgrEmail { get; set; } = string.Empty;
   
    #endregion

    #region User and Buyer Information
    public string? Buyer { get; set; }
    public string? BuyerEmail { get; set; }
    public string? BuyerPhone { get; set; }
    #endregion

    #region Notes and Edit Information
    public string? GeneralNotes { get; set; }
    public string? LastEditedBy { get; set; }
    public string? LastUpdatedBy { get; set; }
    #endregion

    #region Approval Information
    public int? PCFStatus { get; set; } // 0 = New, 1 = Awaiting SM Approval, 2 = Awaiting VP Approval, 3 = Approved, -1 = Reopened, 99 = Expired

    public string? PCFStatusDescription
    {
        get
        {
            return PCFStatus switch
            {
                0 => "New",
                1 => "Awaiting SM Approval",
                2 => "Awaiting VP Approval",
                3 => "Approved",
                -1 => "Reopened",
                99 => "Expired",
                _ => "Unknown"
            };
        }
    }

    public int? Approved { get; set; }
    public string? Salesman { get; set; }
    public string? SalesManager { get; set; } = string.Empty;
    public DateTime? VPSalesDate { get; set; }
    #endregion

    public DateTime? ApprovedDate => VPSalesDate;

    #region Related Collections
    // Navigation property for details
    public List<PCFItem>? PCFLines { get; set; } = new();
    #endregion
    #region Constants and Static Data
    private static readonly Dictionary<string, string> PcfTypeDescriptions = new Dictionary<string, string>
    {
        { "W",   "Warehouse (Standard)" },
        { "DS",  "Dropship (Standard)" },
        { "PW",  "Promo Warehouse" },
        { "PD",  "Promo Dropship" },
        { "T",   "Truckload" },
        { "PL",  "Private Label Only" },
        { "D",   "Direct" },
        { "PART","Parts Only" }
    };
    #endregion

    #region INotifyPropertyChanged Implementation
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    #endregion


    public string PaymentTermsDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PromoPaymentTermsText))
                return $"{PromoPaymentTermsText} (promo)";

            if (!string.IsNullOrWhiteSpace(StandardPaymentTermsText))
                return $"{StandardPaymentTermsText} (standard)";

            return "Special Terms";
        }
    }
}

public class PaymentTerm
{
    public string? Terms_Code { get; set; }
    public string? Description { get; set; }
    public int? Uf_BillingTermActive { get; set; }
}

public class PCFItem
{
    public string? PCFNumber { get; set; }
    public string? ItemNum { get; set; }
    public string? CustNum { get; set; }
    public string? ItemDesc { get; set; }
    public double ApprovedPrice { get; set; }

    public string? Family_Code { get; set; }
    public string? Family_Code_Description { get; set; }
    public string? UserName { get; set; }  // set and used only in the update query
    public string? ItemStatus { get; set; }
    public string? ItemStatusDescription
    {
        get
        {
            return ItemStatus switch
            {
                "A" => "Active",
                "O" => "Obsolete",
                "S" => "Slow Moving",
                _ => "Unknown"
            };
        }
    }
}

public class StatusOptions
{
    public string? StatusCode { get; set; }
}
