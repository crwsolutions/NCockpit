using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui;

namespace NCockpit.ViewModels.Popups;

/// <summary>
/// ViewModel for the View File popup. Displays the contents of a text file in a
/// read-only editor. The popup receives the full file path and file name via
/// shell parameters.
/// </summary>
public partial class ViewFilePopupViewModel : ObservableObject, IQueryAttributable
{
    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private string content = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("path", out var pathObject) && pathObject is string path)
        {
            FileName = System.IO.Path.GetFileName(path);
            _ = LoadFileAsync(path);
        }
    }

    /// <summary>
    /// Load the file contents from the provided path.
    /// </summary>
    private async Task LoadFileAsync(string path)
    {
        try
        {
            Content = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error reading file: {ex.Message}";
            Content = string.Empty;
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
