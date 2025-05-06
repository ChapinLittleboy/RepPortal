using MimeKit.Cryptography;

namespace RepPortal.Services;

public interface IRepCodeContext
{
    string CurrentRepCode { get; }
    event Action OnRepCodeChanged;
    void OverrideRepCode(string newCode);
    void OverrideRepCode(string newCode, List<string> regions);
    void ResetRepCode();
    string CurrentLastName { get; }
    string CurrentFirstName { get; }
    string RepRegion { get; }
    public List<string> CurrentRegions { get; set; }

}


public class RepCodeContext : IRepCodeContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string _overriddenCode;
    private List<string> _overriddenRegions = new();


    public RepCodeContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string CurrentRepCode => !string.IsNullOrEmpty(_overriddenCode)
        ? _overriddenCode
        : _httpContextAccessor.HttpContext?.User?.FindFirst("RepCode")?.Value ?? string.Empty;

    public List<string> CurrentRegions => _overriddenCode != null
        ? _overriddenRegions
        : _httpContextAccessor.HttpContext?.User?.FindAll("Region")?.Select(r => r.Value).ToList() ?? new();

    public string CurrentLastName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst("LastName")?.Value ?? string.Empty;
        }
    }
    public string CurrentFirstName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst("FirstName")?.Value ?? string.Empty;
        }
    }
    public string RepRegion
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst("Region")?.Value ?? string.Empty;
        }
    }

    List<string> IRepCodeContext.CurrentRegions { get => CurrentRegions; set => throw new NotImplementedException(); }

    public event Action OnRepCodeChanged;


    public void OverrideRepCode(string newCode)
    {
        _overriddenCode = newCode;
        _overriddenRegions = new(); // clear region overrides
        OnRepCodeChanged?.Invoke();
    }

    public void OverrideRepCode(string newCode, List<string> regions)
    {
        _overriddenCode = newCode;
        _overriddenRegions = regions ?? new();
        OnRepCodeChanged?.Invoke();
    }

    public void ResetRepCode()
    {
        _overriddenCode = null;
        _overriddenRegions.Clear();
        OnRepCodeChanged?.Invoke();
    }
}
