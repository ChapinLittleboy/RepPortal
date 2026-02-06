using System.ComponentModel;
using System.Reflection;

namespace RepPortal.Models;

public class Customer
{
    [CsiField("CustNum")]
    public string? Cust_Num { get; set; }

    [CsiField("Name")]
    public string? Cust_Name { get; set; }

    [CsiField("Slsman")]
    public string? RepCode { get; set; }
    // No Region because region is ship-to specific

    [CsiField("CustType")]
    public string? BuyingGroup { get; set; }

    [CsiField("CustTypeDescription")]
    public string? BuyingGroupDescription { get; set; }


    public string BuyingGroupDisplay => $"{BuyingGroup} - {BuyingGroupDescription}";

    [CsiField("Name")]
    public string? BillToName { get; set; }

    [CsiField("Addr_1")]
    public string? BillToAddress1 { get; set; }

    [CsiField("Addr_2")]
    public string? BillToAddress2 { get; set; }

    [CsiField("Addr_3")]
    public string? BillToAddress3 { get; set; }

    [CsiField("Addr_4")]
    public string? BillToAddress4 { get; set; }

    [CsiField("City")]
    public string? BillToCity { get; set; }

    [CsiField("StateCode")]
    public string? BillToState { get; set; }

    [CsiField("Zip")]
    public string? BillToZip { get; set; }

    [CsiField("Country")]
    public string? BillToCountry { get; set; }


    public string? BillToPhone { get; set; }


    public string? BillToBuyer { get; set; }


    public string? PaymentTerms { get; set; }

    [CsiField("cusUf_PROG_BASIS")]
    public string? PricingMethod { get; set; }  // from field Uf_PROG_BASIS -- has full string? in it

    [CsiField("cusUf_FrtTerms")]
    public string? FreightTerms { get; set; }


    public double FreightMinimums { get; set; }

    [CsiField("cusuf_c_slsmgr")]
    public string? SalesManager { get; set; }


    public string? SalesManagerName { get; set; }
    public string? SalesManagerEmail { get; set; }
    public string? PaymentTermsCode { get; set; }
    public string? PaymentTermsDescription { get; set; }

    [CsiField("CorpCust")]
    public string? Corp_Cust { get; set; }

    [CsiField("CreditHold")]
    public int CreditHold { get; set; }

    [CsiField("CreditHoldDescription")]
    public string? CreditHoldReasonDescription { get; set; }

    [CsiField("CreditHoldDate")]
    public DateTime? CreditHoldDate { get; set; }

    // This property is just for display purposes
    public string CreditHoldDateDisplay =>
        CreditHoldDate == null ? "" : CreditHoldDate.Value.ToString("d");

    [CsiField("CreditHoldReason")]
    public string? CreditHoldReason { get; set; }

    [CsiField("cusUf_PROG_BASIS")]
    public string? PricingCode { get; set; }




    public string? EUT { get; set; }



    [CsiField("Stat")]

    public string? Status { get; set; }



    public string? DisplayCustName => Cust_Name.Replace("&", "(and)");

    public string? CustNameWithNum => $"{Cust_Name} ({Cust_Num.Trim()})";



    public enum CustomerStatus
    {
        [Description("Active")]
        A,
        [Description("Inactive")]
        I,
        [Description("Restricted")]
        R
    }

    public string StatusDescription
    {
        get
        {
            if (Enum.TryParse<CustomerStatus>(Status, out var customerStatus))
            {
                return customerStatus.ToDescription();
            }
            return "Unknown";
        }
    }
    public string HoldStatusYN
    {
        get
        {
            if (CreditHold == 1)
            {
                return "Yes";
            }
            else
            {
                return "No";
            }
        }
    }

}


public static class CustomerStatusExtensions
{
    public static string? ToDescription(this Customer.CustomerStatus status)
    {
        return status switch
        {
            Customer.CustomerStatus.A => "Active",
            Customer.CustomerStatus.I => "Inactive",
            Customer.CustomerStatus.R => "Restricted",
            _ => "Unknown"
        };
    }
    public static string? GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = field.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
}

// Usage
/*
Customer.CustomerStatus status = Customer.CustomerStatus.A;
string? description = status.ToDescription();
Console.WriteLine(description); // Outputs: Active

or
// with descriptions in enum
CustomerStatus status = CustomerStatus.A;
string? description = status.GetDescription();
Console.WriteLine(description); // Outputs: Active


*/