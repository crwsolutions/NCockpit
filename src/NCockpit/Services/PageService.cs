namespace NCockpit.Services;

/// <summary>
/// Abstraction over the active MAUI page so ViewModels can call page-level
/// APIs (e.g. confirmation dialogs) without reaching into
/// <c>Application.Current.Windows[0].Page</c>.
/// </summary>
public interface IPageService
{
    /// <summary>
    /// Show an OK/Cancel (or custom two-button) confirmation dialog and return
    /// the user's choice.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}

/// <summary>
/// Default implementation that delegates to the top page of the first window.
/// </summary>
public class PageService : IPageService
{
    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        var page = Application.Current?.Windows[0]?.Page
            ?? throw new InvalidOperationException("No active page to show the confirmation on");
        return page.DisplayAlertAsync(title, message, accept, cancel);
    }
}
