namespace RepPortal.Models;

public class ItemCls
{
    public string? SiteRef { get; }
    public string Item { get; }
    public string Description { get; }
    public string? UM { get; }
    public string? AbcCode { get; }
    public string? ProductCode { get; }
    public string? PMTCode { get; }
    public decimal? UnitCost { get; }
    public string? FamilyCode { get; }
    public decimal? CurUCost { get; }
    public string Stat { get; private set; }
    public Guid RowPointer { get; }

    public string DisplayText => $"{Item} – {Description}";

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
        string stat,
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
        Stat = stat;
        RowPointer = rowPointer;
    }
}
public class ItemDetail
{
    public string Item { get; set; }
    public string Description { get; set; }
    public decimal Price1 { get; set; }
    public decimal Price2 { get; set; }
    public decimal Price3 { get; set; }
    public string? ReplacementItem { get; set; }
    public string? ReplacementMessage { get; set; }

    public string ItemStatusDescription => ItemStatus switch
    {
        "A" => "Active",
        "O" => "Obsolete",
        "S" => "Slow Moving",
        _ => "Unknown"
    };
    public string ItemStatusExpandedDescription => ItemStatus switch
    {
        "A" => "Active — Available for purchase",
        "O" => "Obsolete — No stock, unavailable for purchase",
        "S" => "Slow Moving — Limited stock available",
        _ => "Unknown"
    };

    public string ItemStatus { get; set; }
}

public class ItemInfo
{
    public string Item { get; set; }
    public string Description { get; set; }
    // what shows up + gets filtered on in the combo
    public string DisplayText => $"{Item} – {Description}";
}