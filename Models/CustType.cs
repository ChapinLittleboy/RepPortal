namespace RepPortal.Models;

public class CustType
{
    public string CustomerType { get; set; }
    public string CustTypeName { get; set; }
    public string DisplayText => $"{CustomerType} - {CustTypeName}";

}
