namespace RepPortal.Models;

public class CustType
{
    [CsiField("CustType")] 
    public string CustomerType { get; set; }
    [CsiField("Description")] 
    public string CustTypeName { get; set; }
    public string DisplayText => $"{CustomerType} - {CustTypeName}";

}
