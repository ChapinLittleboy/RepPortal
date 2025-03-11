namespace RepPortal.Models;

public class ItemCls
{
    public string SiteRef { get; }
    public string Item { get; }
    public string Description { get; }
    public string UM { get; }
    public string AbcCode { get; }
    public string ProductCode { get; }
    public string PMTCode { get; }
    public decimal UnitCost { get; }
    public string FamilyCode { get; }
    public decimal CurUCost { get; }
    public Guid RowPointer { get; }


    public ItemCls(
        string siteRef,
        string item,
        string description,
        string uM,
        string abcCode,
        string productCode,
        string pMTCode,
        decimal unitCost,
        string familyCode,
        decimal curUCost,
        Guid rowPointer)
    {
        SiteRef = siteRef;
        Item = item;
        Description = description;
        UM = uM;
        AbcCode = abcCode;
        ProductCode = productCode;
        PMTCode = pMTCode;
        UnitCost = unitCost;
        FamilyCode = familyCode;
        CurUCost = curUCost;
        RowPointer = rowPointer;
    }
}
