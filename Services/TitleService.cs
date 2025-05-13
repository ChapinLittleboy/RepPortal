using RepPortal.Models;
namespace RepPortal.Services;


public class TitleService
{
    private readonly HelpContentService _helpContentService;

    public TitleService(HelpContentService helpContentService)
    {
        _helpContentService = helpContentService;
    }

    public event Action OnTitleChanged;

    private string _pageSubtitle = string.Empty;
    public string PageSubtitle
    {
        get => _pageSubtitle;
        set
        {
            if (_pageSubtitle != value)
            {
                _pageSubtitle = value;
                OnTitleChanged?.Invoke();
            }
        }
    }

    public string? PageHelpContent { get; private set; }

    public async Task LoadPageHelpContentAsync(string pageKey)
    {
        var help = await _helpContentService.GetHelpContentAsync(pageKey);

        PageHelpContent = help?.HtmlContent ?? string.Empty;

        // Notify the UI to update if needed
        OnTitleChanged?.Invoke();
    }
}
