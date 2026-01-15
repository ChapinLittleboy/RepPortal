namespace RepPortal.Models;

public class PcfExpirationNotice
{

    public int PcfNum { get; set; }
    public string NoticeType { get; set; }
    public DateTime SentOnDate { get; set; }
    public string SentToRepEmail { get; set; }
    public string SentToMgrEmail { get; set; }


    public enum PcfNoticeType
    {
        Day30,
        Day15
    }


}
 