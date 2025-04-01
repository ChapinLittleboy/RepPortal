using System.ComponentModel;
using System.Reflection;

namespace RepPortal.Models;

public class Customer
{
    public string? Cust_Num { get; set; }
    public string? Cust_Name { get; set; }
    public string? RepCode { get; set; }

    public string? BuyingGroup { get; set; }
    public string? BillToName { get; set; }
    public string? BillToAddress1 { get; set; }
    public string? BillToAddress2 { get; set; }
    public string? BillToAddress3 { get; set; }
    public string? BillToAddress4 { get; set; }
    public string? BillToCity { get; set; }
    public string? BillToState { get; set; }
    public string? BillToZip { get; set; }
    public string? BillToCountry { get; set; }
    public string? BillToPhone { get; set; }
    public string? BillToBuyer { get; set; }
    public string? PaymentTerms { get; set; }
    public string? PricingMethod { get; set; }  // from field Uf_PROG_BASIS -- has full string? in it
    public string? FreightTerms { get; set; }
    public double FreightMinimums { get; set; }
    public string? SalesManager { get; set; }
    public string? SalesManagerName { get; set; }
    public string? SalesManagerEmail { get; set; }
    public string? PaymentTermsCode { get; set; }
    public string? PaymentTermsDescription { get; set; }
    public string? Corp_Cust { get; set; }
    public int CreditHold { get; set; }
    public DateTime CreditHoldDate { get; set; }
    public string? CreditHoldReason { get; set; }
    public string? PricingCode { get; set; }




    public string? EUT { get; set; }




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