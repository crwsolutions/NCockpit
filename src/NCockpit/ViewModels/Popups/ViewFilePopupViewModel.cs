using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui;
using NCockpit.Services;

namespace NCockpit.ViewModels.Popups;

/// <summary>
/// ViewModel for the View File popup. Shows the file as an HTML document built by
/// <see cref="IFilePreviewService"/>: Markdown is rendered, other text files are
/// shown verbatim. The popup receives the full file path and file name via shell
/// parameters.
/// </summary>
public partial class ViewFilePopupViewModel : ObservableObject, IQueryAttributable
{
    private readonly IFilePreviewService _filePreviewService;

    [ObservableProperty]
    public partial string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The HTML document the web view displays.
    /// </summary>
    [ObservableProperty]
    public partial string Html { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public ViewFilePopupViewModel(IFilePreviewService filePreviewService)
    {
        _filePreviewService = filePreviewService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("path", out var pathObject) && pathObject is string path)
        {
            FileName = System.IO.Path.GetFileName(path);
            _ = LoadFileAsync(path);
        }
    }

    /// <summary>
    /// Build the HTML document for the given file path.
    /// </summary>
    private async Task LoadFileAsync(string path)
    {
        try
        {
            ErrorMessage = null;
            Html = await _filePreviewService.BuildHtmlDocumentAsync(path);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error viewing file: {ex.Message}";
            Html = string.Empty;
        }
    }

    /// <summary>
    /// Close the popup.
    /// </summary>
    [RelayCommand]
    private async Task CloseAsync()
    {
        await Shell.Current.ClosePopupAsync();
    }
}
