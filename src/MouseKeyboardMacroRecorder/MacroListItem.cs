using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using MouseKeyboardMacroRecorder.Core.Domain;

namespace MouseKeyboardMacroRecorder;

/// <summary>
/// Presentation model for an imported or recorded macro in the list surface.
/// </summary>
internal sealed class MacroListItem : INotifyPropertyChanged
{
    private MacroDocument _document;
    private string? _filePath;
    private bool _isSaved;

    public MacroListItem(MacroDocument document, string? filePath, bool isSaved)
    {
        _document = document;
        _filePath = filePath;
        _isSaved = isSaved;
        ActionItems = BuildActionItems(document);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MacroDocument Document => _document;

    public string? FilePath => _filePath;

    public bool IsSaved => _isSaved;

    public string Name => _document.Name;

    public string Meta => _isSaved ? "Saved" : "Not exported";

    public string ActionCountText => $"{_document.Actions.Count} actions";

    public ObservableCollection<MacroActionListItem> ActionItems { get; private set; }

    public void Update(MacroDocument document, string? filePath, bool isSaved)
    {
        _document = document;
        _filePath = filePath;
        _isSaved = isSaved;
        ActionItems = BuildActionItems(document);
        OnPropertyChanged(string.Empty);
    }

    public void MarkSaved(string path)
    {
        _filePath = path;
        _isSaved = true;
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(IsSaved));
        OnPropertyChanged(nameof(Meta));
    }

    public void MarkChanged(MacroDocument document)
    {
        _document = document;
        _isSaved = false;
        ActionItems = BuildActionItems(document);
        OnPropertyChanged(string.Empty);
    }

    private static ObservableCollection<MacroActionListItem> BuildActionItems(MacroDocument document)
    {
        return new ObservableCollection<MacroActionListItem>(
            document.Actions.Select((action, index) => new MacroActionListItem(action, index + 1)));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Presentation model for one macro action row.
/// </summary>
internal sealed class MacroActionListItem
{
    public MacroActionListItem(MacroAction action, int index)
    {
        Action = action;
        IndexText = index.ToString(CultureInfo.InvariantCulture);
        DelayText = action.AfterMilliseconds == 0 ? "now" : $"+{action.AfterMilliseconds} ms";
        Summary = Describe(action);
    }

    public MacroAction Action { get; }

    public string IndexText { get; }

    public string DelayText { get; }

    public string Summary { get; }

    private static string Describe(MacroAction action)
    {
        return action switch
        {
            MouseMoveAction move => $"Move to {move.X}, {move.Y}",
            MouseButtonAction button => $"{button.Button} button {button.State.ToString().ToLowerInvariant()} at {button.X}, {button.Y}",
            MouseWheelAction wheel => $"Scroll {wheel.Delta:+#;-#;0} at {wheel.X}, {wheel.Y}",
            KeyboardAction keyboard => $"Key {keyboard.Key} {keyboard.State.ToString().ToLowerInvariant()}",
            _ => "Unknown action"
        };
    }
}
