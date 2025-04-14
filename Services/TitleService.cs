namespace RepPortal.Services;

public class TitleService    // This is where the page title is set that will be displayed in the AppBar.
{
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
}
