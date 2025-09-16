namespace RepPortal.Models;

public class PackingList
{
    public PackingListHeader Header { get; set; } = new PackingListHeader();
    public List<PackingListItem> Items { get; set; } = new List<PackingListItem>();

}
