namespace RepPortal.Services;

public interface IRepCodeContext
{
    string CurrentRepCode { get; }
    event Action OnRepCodeChanged;
    void OverrideRepCode(string newCode);
    void ResetRepCode();
    string CurrentLastName { get; }
}


public class RepCodeContext : IRepCodeContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string _overriddenCode;

    public RepCodeContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string CurrentRepCode
    {
        get
        {
            if (!string.IsNullOrEmpty(_overriddenCode))
                return _overriddenCode;

            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst("RepCode")?.Value ?? string.Empty;
        }
    }

    public string CurrentLastName
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst("LastName")?.Value ?? string.Empty;
        }
    }
    public event Action OnRepCodeChanged;

    public void OverrideRepCode(string newCode)
    {
        _overriddenCode = newCode;
        OnRepCodeChanged?.Invoke();
    }

    public void ResetRepCode()
    {
        _overriddenCode = null;
        OnRepCodeChanged?.Invoke();
    }
}
