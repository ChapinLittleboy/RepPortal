using RepPortal.Services;

namespace RepPortal.Tests.Support;

internal sealed class FakeRepCodeContext : IRepCodeContext
{
    private readonly string _originalRepCode;
    private readonly List<string> _originalRegions;
    private string _currentRepCode;

    public FakeRepCodeContext(string repCode = "REP1", IEnumerable<string>? regions = null)
    {
        _originalRepCode = repCode;
        _originalRegions = regions?.ToList() ?? new List<string>();
        _currentRepCode = repCode;
        CurrentRegions = _originalRegions.ToList();
    }

    public string CurrentRepCode => _currentRepCode;
    public event Action? OnRepCodeChanged;
    public string CurrentLastName { get; set; } = "";
    public string CurrentFirstName { get; set; } = "";
    public string RepRegion { get; set; } = "";
    public List<string> CurrentRegions { get; set; }
    public string AssignedRegion { get; set; } = "";
    public bool IsAdministrator { get; set; }
    public string UserName { get; set; } = "";
    public int OverrideCalls { get; private set; }
    public int ResetCalls { get; private set; }

    public void OverrideRepCode(string newCode)
    {
        _currentRepCode = newCode;
        CurrentRegions = new List<string>();
        OverrideCalls++;
        OnRepCodeChanged?.Invoke();
    }

    public void OverrideRepCode(string newCode, List<string> regions)
    {
        _currentRepCode = newCode;
        CurrentRegions = regions ?? new List<string>();
        OverrideCalls++;
        OnRepCodeChanged?.Invoke();
    }

    public void ResetRepCode()
    {
        _currentRepCode = _originalRepCode;
        CurrentRegions = _originalRegions.ToList();
        ResetCalls++;
        OnRepCodeChanged?.Invoke();
    }
}
