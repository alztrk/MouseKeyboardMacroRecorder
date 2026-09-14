using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Material.Icons;
using MouseKeyboardMacroRecorder.Core.Application;
using MouseKeyboardMacroRecorder.Core.Domain;
using MouseKeyboardMacroRecorder.Core.Infrastructure.Persistence;
using MouseKeyboardMacroRecorder.Infrastructure.Windows;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;

namespace MouseKeyboardMacroRecorder;

/// <summary>
/// Compact command surface for recording and replaying desktop input.
/// </summary>
public partial class MainWindow : Window, IDisposable
{
    private readonly ObservableCollection<MacroListItem> _macroItems = new();
    private readonly WindowsInputCapture _capture;
    private readonly WindowsInputInjector _injector;
    private readonly WindowsScreenInfoProvider _screenInfo;
    private readonly WindowsCursorPositionProvider _cursorPosition;
    private WindowsGlobalHotkeyService _hotkeys;
    private readonly MacroRecorderService _recorder;
    private readonly MacroPlaybackService _playback;
    private readonly AutoClickerService _autoClicker;
    private readonly AutomationCoordinator _coordinator;
    private readonly WindowsNotificationService _notifications;
    private UserPreferences _preferences;
    private IDisposable? _hotkeyRegistration;
    private readonly SemaphoreSlim _hotkeyExecutionGate = new(1, 1);
    private string? _startupWarning;
    private string? _statusMessage;
    private bool _captureErrorShown;
    private bool _isApplyingPreferences;
    private bool _closeRequested;
    private bool _isClosing;
    private bool _servicesDisposed;
    private bool _autoIntervalValid = true;

    /// <summary>
    /// Creates the main application window and wires the desktop automation services.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        LoadWindowIcon();

        _preferences = LoadPreferences();
        _capture = new WindowsInputCapture();
        _injector = new WindowsInputInjector();
        _screenInfo = new WindowsScreenInfoProvider();
        _cursorPosition = new WindowsCursorPositionProvider();
        _hotkeys = new WindowsGlobalHotkeyService();
        _recorder = new MacroRecorderService(_capture, _screenInfo);
        _playback = new MacroPlaybackService(_injector, new SystemAsyncDelay());
        _autoClicker = new AutoClickerService(_injector, _cursorPosition, new SystemAsyncDelay());
        _coordinator = new AutomationCoordinator(_recorder, _playback, _autoClicker, _injector);
        _notifications = new WindowsNotificationService();

        MacroItemsList.ItemsSource = _macroItems;
        _coordinator.StateChanged += Coordinator_StateChanged;
        _playback.ProgressChanged += Playback_ProgressChanged;
        _capture.Error += Capture_Error;
        _hotkeys.Pressed += Hotkeys_Pressed;

        ApplyPreferences();
        ApplyModeButtonState(macroIsActive: !string.Equals(_preferences.LastSurface, WorkspaceSurfaceNames.AutoClicker, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(_preferences.LastSurface, WorkspaceSurfaceNames.AutoClicker, StringComparison.OrdinalIgnoreCase))
        {
            MacroSurface.Visibility = Visibility.Collapsed;
            AutoClickerSurface.Visibility = Visibility.Visible;
        }

        UpdateThemeButton();
        UpdateShortcutHint();
        UpdateMacroListVisibility();
        UpdateUiForState(AutomationState.Idle);
    }

    private UserPreferences LoadPreferences()
    {
        try
        {
            var preferences = UserPreferencesStore.Load();
            UserPreferencesNormalizer.Normalize(preferences);
            return preferences;
        }
        catch (PreferencesPersistenceException exception)
        {
            _startupWarning = "Preferences could not be loaded: " + exception.Message;
            return new UserPreferences
            {
                LastSurface = AutomationDefaults.LastSurface
            };
        }
    }

    private void LoadWindowIcon()
    {
        var iconUri = new Uri("pack://application:,,,/Assets/favicon.ico", UriKind.Absolute);
        var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(
            iconUri,
            System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        var icon = decoder.Frames
            .OrderByDescending(frame => frame.PixelWidth)
            .First();
        icon.Freeze();
        Icon = icon;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateResponsiveLayout();
        Dispatcher.BeginInvoke(
            new Action(RegisterGlobalHotkeys),
            System.Windows.Threading.DispatcherPriority.ContextIdle);
        Dispatcher.BeginInvoke(
            new Action(LoadAutoSavedMacros),
            System.Windows.Threading.DispatcherPriority.Background);
        if (!string.IsNullOrWhiteSpace(_startupWarning))
        {
            SetStatus(_startupWarning, isError: true);
        }

        BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(180)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void RegisterGlobalHotkeys()
    {
        TryRegisterGlobalHotkeys();
    }

    private void LoadAutoSavedMacros()
    {
        var directory = ProductInfo.GetMacroDirectory();
        if (!Directory.Exists(directory))
        {
            return;
        }

        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*" + MacroFileStore.FileExtension)
                         .OrderByDescending(path => File.GetLastWriteTimeUtc(path)))
            {
                try
                {
                    AddOrUpdateMacro(MacroFileStore.Load(path), path, saved: true);
                }
                catch (MacroPersistenceException exception)
                {
                    _startupWarning = "One auto-saved macro could not be loaded: " + exception.Message;
                }
            }
        }
        catch (IOException exception)
        {
            _startupWarning = "Auto-saved macros could not be loaded: " + exception.Message;
        }
        catch (UnauthorizedAccessException exception)
        {
            _startupWarning = "Auto-saved macros could not be loaded because access was denied: " + exception.Message;
        }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        e.Cancel = true;
        if (_closeRequested)
        {
            return;
        }

        _closeRequested = true;
        try
        {
            await _coordinator.StopAsync();
        }
        catch (Exception exception)
        {
            _coordinator.RequestStop();
            _startupWarning = "Automation stopped with an error while closing: " + exception.Message;
        }

        var animation = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(140)));
        animation.Completed += (_, _) =>
        {
            _isClosing = true;
            Close();
        };
        BeginAnimation(UIElement.OpacityProperty, animation);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        Dispose();
    }

    public void Dispose()
    {
        if (_servicesDisposed)
        {
            return;
        }

        _servicesDisposed = true;
        _hotkeyRegistration?.Dispose();
        _hotkeys.Pressed -= Hotkeys_Pressed;
        _hotkeys.Dispose();
        _coordinator.StateChanged -= Coordinator_StateChanged;
        _playback.ProgressChanged -= Playback_ProgressChanged;
        _capture.Error -= Capture_Error;
        _coordinator.Dispose();
        _injector.Dispose();
        _notifications.Dispose();
        GC.SuppressFinalize(this);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout();
    }

    private void MacroSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout();
    }

    private void AutoClickerSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout();
    }

    private void UpdateResponsiveLayout()
    {
        if (ShortcutHint is null)
        {
            return;
        }

        ShortcutHint.Visibility = ActualWidth < UiMetrics.HideShortcutHintWidth ? Visibility.Collapsed : Visibility.Visible;

        var macroCompact = MacroSurface.ActualWidth < UiMetrics.CompactSurfaceWidth;
        if (macroCompact)
        {
            MacroSettingsColumn.Width = new GridLength(0);
            Grid.SetColumnSpan(MacroListPanel, 2);
            Grid.SetColumn(MacroDivider, 0);
            Grid.SetRow(MacroDivider, 1);
            MacroDivider.Padding = new Thickness(0, 16, 0, 0);
            MacroDivider.BorderThickness = new Thickness(0, 1, 0, 0);
            MacroListPanel.Margin = new Thickness(0, 0, 0, 16);
        }
        else
        {
            MacroSettingsColumn.Width = UiMetrics.MacroSettingsColumnWidth;
            Grid.SetColumnSpan(MacroListPanel, 1);
            Grid.SetColumn(MacroDivider, 1);
            Grid.SetRow(MacroDivider, 0);
            MacroDivider.Padding = new Thickness(22, 0, 0, 0);
            MacroDivider.BorderThickness = new Thickness(1, 0, 0, 0);
            MacroListPanel.Margin = new Thickness(0, 0, 22, 0);
        }

        var autoCompact = AutoClickerSurface.ActualWidth < UiMetrics.CompactSurfaceWidth;
        if (autoCompact)
        {
            AutoClickerSettingsColumn.Width = new GridLength(0);
            Grid.SetColumnSpan(AutoClickerMainPanel, 2);
            Grid.SetColumn(AutoClickerDivider, 0);
            Grid.SetRow(AutoClickerDivider, 1);
            AutoClickerDivider.Padding = new Thickness(0, 16, 0, 0);
            AutoClickerDivider.BorderThickness = new Thickness(0, 1, 0, 0);
            AutoClickerMainPanel.Margin = new Thickness(0, 0, 0, 16);
        }
        else
        {
            AutoClickerSettingsColumn.Width = UiMetrics.AutoClickerSettingsColumnWidth;
            Grid.SetColumnSpan(AutoClickerMainPanel, 1);
            Grid.SetColumn(AutoClickerDivider, 1);
            Grid.SetRow(AutoClickerDivider, 0);
            AutoClickerDivider.Padding = new Thickness(20, 0, 0, 0);
            AutoClickerDivider.BorderThickness = new Thickness(1, 0, 0, 0);
            AutoClickerMainPanel.Margin = new Thickness(0, 0, 20, 0);
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleTheme();
        UpdateThemeButton();
    }

    private void HotkeysButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator.State != AutomationState.Idle)
        {
            return;
        }

        var dialog = new HotkeyDialog(
            _preferences.RecordHotkeyVirtualKey,
            _preferences.RecordHotkeyModifiers,
            _preferences.PlayHotkeyVirtualKey,
            _preferences.PlayHotkeyModifiers,
            _preferences.StopHotkeyVirtualKey,
            _preferences.StopHotkeyModifiers)
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var previousRecord = _preferences.RecordHotkeyVirtualKey;
        var previousRecordModifiers = _preferences.RecordHotkeyModifiers;
        var previousPlay = _preferences.PlayHotkeyVirtualKey;
        var previousPlayModifiers = _preferences.PlayHotkeyModifiers;
        var previousStop = _preferences.StopHotkeyVirtualKey;
        var previousStopModifiers = _preferences.StopHotkeyModifiers;
        _preferences.RecordHotkeyVirtualKey = dialog.RecordVirtualKey;
        _preferences.RecordHotkeyModifiers = dialog.RecordModifiers;
        _preferences.PlayHotkeyVirtualKey = dialog.PlayVirtualKey;
        _preferences.PlayHotkeyModifiers = dialog.PlayModifiers;
        _preferences.StopHotkeyVirtualKey = dialog.StopVirtualKey;
        _preferences.StopHotkeyModifiers = dialog.StopModifiers;
        if (TryRegisterGlobalHotkeys())
        {
            PersistPreferences();
            UpdateShortcutHint();
            SetStatus("Hotkeys updated.");
            return;
        }

        _preferences.RecordHotkeyVirtualKey = previousRecord;
        _preferences.RecordHotkeyModifiers = previousRecordModifiers;
        _preferences.PlayHotkeyVirtualKey = previousPlay;
        _preferences.PlayHotkeyModifiers = previousPlayModifiers;
        _preferences.StopHotkeyVirtualKey = previousStop;
        _preferences.StopHotkeyModifiers = previousStopModifiers;
        TryRegisterGlobalHotkeys();
        UpdateShortcutHint();
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        MaximizeIcon.Kind = WindowState == WindowState.Maximized
            ? MaterialIconKind.WindowRestore
            : MaterialIconKind.WindowMaximize;
    }

    private void UpdateThemeButton()
    {
        ThemeButton.ToolTip = ThemeManager.IsDarkTheme ? "Use light theme" : "Use dark theme";
        ThemeIcon.Foreground = (WpfBrush)FindResource("InkBrush");
    }

    private void MacroModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator.State != AutomationState.Idle)
        {
            return;
        }

        SwitchSurface(MacroSurface, AutoClickerSurface);
        ApplyModeButtonState(macroIsActive: true);
        Dispatcher.BeginInvoke(new Action(UpdateResponsiveLayout), System.Windows.Threading.DispatcherPriority.Loaded);
        _preferences.LastSurface = WorkspaceSurfaceNames.Macros;
        PersistPreferences();
    }

    private void AutoClickerModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator.State != AutomationState.Idle)
        {
            return;
        }

        SwitchSurface(AutoClickerSurface, MacroSurface);
        ApplyModeButtonState(macroIsActive: false);
        Dispatcher.BeginInvoke(new Action(UpdateResponsiveLayout), System.Windows.Threading.DispatcherPriority.Loaded);
        _preferences.LastSurface = WorkspaceSurfaceNames.AutoClicker;
        PersistPreferences();
    }

    private void ApplyModeButtonState(bool macroIsActive)
    {
        MacroModeButton.Style = (Style)FindResource(macroIsActive ? "ActiveModeButtonStyle" : "ModeButtonStyle");
        AutoClickerModeButton.Style = (Style)FindResource(macroIsActive ? "ModeButtonStyle" : "ActiveModeButtonStyle");
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator.State == AutomationState.Recording)
        {
            StopRecordingAndStore();
            return;
        }

        if (_coordinator.State != AutomationState.Idle)
        {
            return;
        }

        try
        {
            var options = new RecordingOptions(
                CaptureMouseCheckBox.IsChecked == true,
                CaptureKeyboardCheckBox.IsChecked == true);
            options.EnsureValid();
            _preferences.CaptureMouse = options.CaptureMouse;
            _preferences.CaptureKeyboard = options.CaptureKeyboard;
            PersistPreferences();
            _coordinator.StartRecording(options);
            SetStatus("Recording input…");
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void StopRecordingAndStore()
    {
        MacroDocument? document = null;
        try
        {
            document = _coordinator.StopRecording("Recorded macro");

            var name = $"{ProductInfo.RecordedMacroBaseName} - {DateTime.Now:yyyy-MM-dd HH-mm-ss}";
            var path = GetUniqueAutoSavePath(name);
            document = document with { Name = name };
            MacroFileStore.Save(path, document);
            AddOrUpdateMacro(document, path, saved: true);
            SetStatus("Macro saved automatically.");
        }
        catch (Exception exception)
        {
            if (document is not null)
            {
                AddOrUpdateMacro(document, null, saved: false);
            }

            ShowError(exception);
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        _ = PlayAsync();
    }

    private async Task PlayAsync()
    {
        try
        {
            if (_coordinator.State == AutomationState.Playing)
            {
                _coordinator.PausePlayback();
                return;
            }

            if (_coordinator.State == AutomationState.Pausing)
            {
                _coordinator.ResumePlayback();
                return;
            }

            if (_coordinator.State != AutomationState.Idle || MacroItemsList.SelectedItem is not MacroListItem selected)
            {
                return;
            }

            var options = BuildPlaybackOptions();
            await _coordinator.PlayAsync(selected.Document, options);
            SetStatus("Ready");
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _ = StopAsync();
    }

    private async Task StopAsync()
    {
        try
        {
            await _coordinator.StopAsync();
            SetStatus("Ready");
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void AutoClickerStartButton_Click(object sender, RoutedEventArgs e)
    {
        _ = AutoClickerAsync();
    }

    private async Task AutoClickerAsync()
    {
        if (_coordinator.State == AutomationState.AutoClicking)
        {
            await StopAutomationAsync();
            return;
        }

        if (_coordinator.State != AutomationState.Idle)
        {
            return;
        }

        try
        {
            var options = BuildAutoClickerOptions();
            await _coordinator.RunAutoClickerAsync(options);
            SetStatus("Ready");
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private async Task StopAutomationAsync()
    {
        try
        {
            await _coordinator.StopAsync();
            SetStatus("Ready");
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator.State != AutomationState.Idle)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = ProductInfo.MacroFileDialogFilter,
            Multiselect = true,
            Title = "Import macros"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var failures = new List<string>();
        foreach (var path in dialog.FileNames)
        {
            try
            {
                AddOrUpdateMacro(MacroFileStore.Load(path), path, saved: true);
            }
            catch (Exception exception)
            {
                failures.Add($"{Path.GetFileName(path)}: {exception.Message}");
            }
        }

        if (failures.Count > 0)
        {
            ShowError(new InvalidOperationException(string.Join(Environment.NewLine, failures)));
        }
        else
        {
            SetStatus("Macro imported.");
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator.State != AutomationState.Idle || MacroItemsList.SelectedItem is not MacroListItem selected)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = MacroFileStore.FileExtension.TrimStart('.'),
            Filter = ProductInfo.MacroFileDialogFilter,
            FileName = selected.Name + MacroFileStore.FileExtension,
            OverwritePrompt = true,
            Title = "Export macro"
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var path = EnsureMacroExtension(dialog.FileName);
        try
        {
            MacroFileStore.Save(path, selected.Document);
            selected.MarkSaved(path);
            SetStatus("Macro exported.");
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
    }

    private void MacroItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MacroItemsList.SelectedItem is MacroListItem selected)
        {
            SelectedMacroTitle.Text = selected.Name;
            SelectedMacroMeta.Text = selected.Meta;
            MacroActionsList.ItemsSource = selected.ActionItems;
        }
        else
        {
            SelectedMacroTitle.Text = string.Empty;
            SelectedMacroMeta.Text = string.Empty;
            MacroActionsList.ItemsSource = null;
        }

        UpdateActionButtons();
        UpdateMacroListVisibility();
    }

    private void MacroActionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();
    }

    private void RemoveMacroButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator.State != AutomationState.Idle || (sender as WpfButton)?.Tag is not MacroListItem item)
        {
            return;
        }

        _macroItems.Remove(item);
        if (_macroItems.Count > 0)
        {
            MacroItemsList.SelectedIndex = Math.Min(MacroItemsList.SelectedIndex, _macroItems.Count - 1);
        }

        UpdateMacroListVisibility();
        SetStatus("Macro removed from the list.");
    }

    private void MoveActionUpButton_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedAction(-1);
    }

    private void MoveActionDownButton_Click(object sender, RoutedEventArgs e)
    {
        MoveSelectedAction(1);
    }

    private void RemoveActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator.State != AutomationState.Idle
            || MacroItemsList.SelectedItem is not MacroListItem macro
            || MacroActionsList.SelectedIndex < 0)
        {
            return;
        }

        var actions = macro.Document.Actions.ToList();
        var selectedIndex = MacroActionsList.SelectedIndex;
        actions.RemoveAt(selectedIndex);
        if (TryUpdateMacroActions(macro, actions))
        {
            MacroActionsList.SelectedIndex = Math.Min(selectedIndex, actions.Count - 1);
        }
    }

    private void MoveSelectedAction(int offset)
    {
        if (_coordinator.State != AutomationState.Idle
            || MacroItemsList.SelectedItem is not MacroListItem macro)
        {
            return;
        }

        var selectedIndex = MacroActionsList.SelectedIndex;
        var targetIndex = selectedIndex + offset;
        if (selectedIndex < 0 || targetIndex < 0 || targetIndex >= macro.Document.Actions.Count)
        {
            return;
        }

        var actions = macro.Document.Actions.ToList();
        (actions[selectedIndex], actions[targetIndex]) = (actions[targetIndex], actions[selectedIndex]);
        if (TryUpdateMacroActions(macro, actions))
        {
            MacroActionsList.SelectedIndex = targetIndex;
        }
    }

    private bool TryUpdateMacroActions(MacroListItem macro, IReadOnlyList<MacroAction> actions)
    {
        try
        {
            var updated = macro.Document with { Actions = actions.ToArray() };
            MacroValidator.EnsureValid(updated);
            macro.MarkChanged(updated);
            MacroItemsList.SelectedItem = macro;
            MacroActionsList.ItemsSource = macro.ActionItems;
            SetStatus("Macro changes are ready to export.");
            return true;
        }
        catch (Exception exception)
        {
            ShowError(exception);
            return false;
        }
    }

    private void UpdateMacroListVisibility()
    {
        var hasMacros = _macroItems.Count > 0;
        EmptyMacroState.Visibility = hasMacros ? Visibility.Collapsed : Visibility.Visible;
        MacroDataPanel.Visibility = hasMacros ? Visibility.Visible : Visibility.Collapsed;
        ExportButton.IsEnabled = hasMacros && MacroItemsList.SelectedItem is not null && _coordinator.State == AutomationState.Idle;
        if (!hasMacros)
        {
            MacroActionsList.ItemsSource = null;
        }
    }

    private void UpdateActionButtons()
    {
        var isIdle = _coordinator.State == AutomationState.Idle;
        var index = MacroActionsList.SelectedIndex;
        var hasSelection = isIdle
            && MacroItemsList.SelectedItem is MacroListItem macro
            && index >= 0
            && index < macro.Document.Actions.Count;
        MoveActionUpButton.IsEnabled = hasSelection && index > 0;
        MoveActionDownButton.IsEnabled = hasSelection && index < ((MacroListItem)MacroItemsList.SelectedItem!).Document.Actions.Count - 1;
        RemoveActionButton.IsEnabled = hasSelection;
    }

    private void Coordinator_StateChanged(object? sender, AutomationStateChangedEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateUiForState(e.Current)));
            return;
        }

        UpdateUiForState(e.Current);
    }

    private void Playback_ProgressChanged(object? sender, PlaybackProgressEventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(() => Playback_ProgressChanged(sender, e)));
            return;
        }

        SetStatus($"Playing · loop {e.Loop} · action {e.Action}/{e.TotalActions}");
    }

    private void Capture_Error(object? sender, InputCaptureErrorEventArgs e)
    {
        if (_captureErrorShown)
        {
            return;
        }

        _captureErrorShown = true;
        var action = new Action(() => ShowError(e.Exception));
        if (Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.BeginInvoke(action);
        }
    }

    private void Hotkeys_Pressed(object? sender, HotkeyPressedEventArgs e)
    {
        if (_isClosing || _servicesDisposed || Dispatcher.HasShutdownStarted)
        {
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_isClosing || _servicesDisposed)
                {
                    return;
                }

                if (e.Command == HotkeyCommand.Stop)
                {
                    _ = ExecuteStopHotkeyAsync();
                    return;
                }

                _ = ExecuteHotkeyAsync(e.Command);
            }));
        }
        catch (Exception exception) when (exception is InvalidOperationException or TaskCanceledException)
        {
            // The dispatcher may be shutting down while the hotkey thread is still unwinding.
            Debug.WriteLine(exception);
        }
    }

    private async Task ExecuteStopHotkeyAsync()
    {
        if (_coordinator.State == AutomationState.Recording)
        {
            StopRecordingAndStore();
            return;
        }

        await StopAsync();
    }

    private async Task ExecuteHotkeyAsync(HotkeyCommand command)
    {
        var acquired = false;
        try
        {
            acquired = await _hotkeyExecutionGate.WaitAsync(0);
            if (!acquired)
            {
                return;
            }

            switch (command)
            {
                case HotkeyCommand.Record:
                    if (string.Equals(_preferences.LastSurface, WorkspaceSurfaceNames.AutoClicker, StringComparison.OrdinalIgnoreCase))
                    {
                        await AutoClickerAsync();
                    }
                    else
                    {
                        RecordButton_Click(this, new RoutedEventArgs());
                    }

                    break;
                case HotkeyCommand.Play:
                    await PlayAsync();
                    break;
                case HotkeyCommand.Stop:
                    if (_coordinator.State == AutomationState.Recording)
                    {
                        RecordButton_Click(this, new RoutedEventArgs());
                    }
                    else
                    {
                        await StopAsync();
                    }

                    break;
            }
        }
        catch (Exception exception)
        {
            ShowError(exception);
        }
        finally
        {
            if (acquired)
            {
                _hotkeyExecutionGate.Release();
            }
        }
    }

    private void UpdateUiForState(AutomationState state)
    {
        var isIdle = state == AutomationState.Idle;
        var isRecording = state == AutomationState.Recording;
        var isPlaying = state is AutomationState.Playing or AutomationState.Pausing;
        var isAutoClicking = state == AutomationState.AutoClicking;

        RecordButton.IsEnabled = isIdle || isRecording;
        RecordNewMacroButton.IsEnabled = isIdle;
        PlayButton.IsEnabled = isIdle && MacroItemsList.SelectedItem is not null || isPlaying;
        StopButton.IsEnabled = !isIdle;
        ImportButton.IsEnabled = isIdle;
        ExportButton.IsEnabled = isIdle && MacroItemsList.SelectedItem is not null;
        MacroModeButton.IsEnabled = isIdle;
        AutoClickerModeButton.IsEnabled = isIdle;
        AutoClickerStartButton.IsEnabled = (isIdle || isAutoClicking) && (_autoIntervalValid || isAutoClicking);
        CaptureMouseCheckBox.IsEnabled = isIdle;
        CaptureKeyboardCheckBox.IsEnabled = isIdle;
        PlaybackRepeatComboBox.IsEnabled = isIdle;
        PlaybackSpeedComboBox.IsEnabled = isIdle;
        PlaybackInterLoopDelayTextBox.IsEnabled = isIdle;
        AutoButtonComboBox.IsEnabled = isIdle;
        AutoIntervalTextBox.IsEnabled = isIdle;
        AutoRepeatComboBox.IsEnabled = isIdle;
        AutoPositionComboBox.IsEnabled = isIdle;
        AutoXTextBox.IsEnabled = isIdle;
        AutoYTextBox.IsEnabled = isIdle;
        HotkeysButton.IsEnabled = isIdle;

        if (isRecording)
        {
            RecordButtonText.Text = "Stop recording";
            RecordButtonIcon.Kind = MaterialIconKind.Stop;
            RecordButton.ToolTip = "Stop recording and save the macro";
            SetStatus($"Recording · {_coordinator.RecordedActionCount} actions");
        }
        else
        {
            RecordButtonText.Text = "Record";
            RecordButtonIcon.Kind = MaterialIconKind.RecordCircleOutline;
            RecordButton.ToolTip = $"Start recording ({DescribeRecordHotkey()})";
        }

        if (state == AutomationState.Playing)
        {
            PlayButtonText.Text = "Pause";
            PlayButtonIcon.Kind = MaterialIconKind.Pause;
        }
        else if (state == AutomationState.Pausing)
        {
            PlayButtonText.Text = "Resume";
            PlayButtonIcon.Kind = MaterialIconKind.Play;
        }
        else
        {
            PlayButtonText.Text = "Play";
            PlayButtonIcon.Kind = MaterialIconKind.Play;
        }

        if (isAutoClicking)
        {
            AutoClickerStartText.Text = "Stop auto clicker";
            AutoClickerStartIcon.Kind = MaterialIconKind.Stop;
            AutoClickerStatusText.Text = "Clicking…";
        }
        else
        {
            AutoClickerStartText.Text = "Start auto clicker";
            AutoClickerStartIcon.Kind = MaterialIconKind.Mouse;
            AutoClickerStatusText.Text = "Ready";
        }

        if (isIdle && state != AutomationState.Error)
        {
            if (_statusMessage is null || _statusMessage.StartsWith("Playing", StringComparison.Ordinal))
            {
                SetStatus(null);
            }
        }

        UpdateMacroListVisibility();
        UpdateActionButtons();
    }

    private void SetStatus(string? message, bool isError = false)
    {
        _statusMessage = message;
        if (string.IsNullOrWhiteSpace(message))
        {
            UpdateShortcutHint();
            return;
        }

        ShortcutHint.Text = message;
        ShortcutHint.Foreground = (WpfBrush)FindResource(isError ? "ErrorBrush" : "SubtleInkBrush");
        ShortcutHint.ToolTip = message;
    }

    private void ShowError(Exception exception)
    {
        var message = GetUserErrorMessage(exception);
        SetStatus("Action could not be completed.", isError: true);
        _notifications.ShowError(message);
        var dialog = new ErrorDialog(message)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private static string GetUserErrorMessage(Exception exception)
    {
        var message = string.IsNullOrWhiteSpace(exception.Message)
            ? "The requested operation could not be completed."
            : exception.Message;
        return message
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)[0]
            .Trim();
    }

    private PlaybackOptions BuildPlaybackOptions()
    {
        var (repeatMode, repeatCount) = ParseRepeat(PlaybackRepeatComboBox, RepeatMode.Once, 1);
        var speedTag = GetSelectedTag(PlaybackSpeedComboBox);
        if (speedTag is null || !double.TryParse(speedTag, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
        {
            throw new InvalidOperationException("Playback speed is not valid.");
        }

        if (!int.TryParse(PlaybackInterLoopDelayTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var interLoopDelay))
        {
            throw new InvalidOperationException("The inter-loop delay must be a whole number.");
        }

        return new PlaybackOptions(speed, repeatMode, repeatCount, interLoopDelay);
    }

    private AutoClickerOptions BuildAutoClickerOptions()
    {
        var button = GetSelectedTag(AutoButtonComboBox) switch
        {
            "left" => MouseButtonKind.Left,
            "right" => MouseButtonKind.Right,
            "middle" => MouseButtonKind.Middle,
            _ => throw new InvalidOperationException("The auto clicker button is not valid.")
        };
        if (!int.TryParse(AutoIntervalTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval))
        {
            throw new InvalidOperationException("The click interval must be a whole number.");
        }

        var (repeatMode, repeatCount) = ParseRepeat(AutoRepeatComboBox, RepeatMode.UntilStopped, 1);
        var positionMode = GetSelectedTag(AutoPositionComboBox) switch
        {
            "current" => ClickPositionMode.CurrentCursor,
            "fixed" => ClickPositionMode.FixedPosition,
            _ => throw new InvalidOperationException("The click position is not valid.")
        };
        var x = 0;
        var y = 0;
        if (positionMode == ClickPositionMode.FixedPosition)
        {
            if (!int.TryParse(AutoXTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                || !int.TryParse(AutoYTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
            {
                throw new InvalidOperationException("Fixed X and Y positions must be whole numbers.");
            }
        }

        return new AutoClickerOptions(button, interval, repeatMode, repeatCount, positionMode, x, y);
    }

    private static (RepeatMode Mode, int Count) ParseRepeat(WpfComboBox comboBox, RepeatMode defaultMode, int defaultCount)
    {
        var tag = GetSelectedTag(comboBox);
        if (tag == "once")
        {
            return (RepeatMode.Once, 1);
        }

        if (tag == "until_stopped")
        {
            return (RepeatMode.UntilStopped, 1);
        }

        if (tag?.StartsWith("count:", StringComparison.Ordinal) == true
            && int.TryParse(tag["count:".Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            return (RepeatMode.FixedCount, count);
        }

        return (defaultMode, defaultCount);
    }

    private void ApplyPreferences()
    {
        _isApplyingPreferences = true;
        try
        {
            CaptureMouseCheckBox.IsChecked = _preferences.CaptureMouse;
            CaptureKeyboardCheckBox.IsChecked = _preferences.CaptureKeyboard;
            SelectByTag(PlaybackRepeatComboBox, RepeatTag(_preferences.PlaybackRepeatMode, _preferences.PlaybackRepeatCount));
            SelectByTag(PlaybackSpeedComboBox, _preferences.PlaybackSpeed.ToString("0.##", CultureInfo.InvariantCulture));
            PlaybackInterLoopDelayTextBox.Text = _preferences.PlaybackInterLoopDelayMilliseconds.ToString(CultureInfo.InvariantCulture);
            SelectByTag(AutoButtonComboBox, _preferences.AutoClickButton switch
            {
                MouseButtonKind.Left => "left",
                MouseButtonKind.Right => "right",
                _ => "middle"
            });
            AutoIntervalTextBox.Text = _preferences.AutoClickIntervalMilliseconds.ToString(CultureInfo.InvariantCulture);
            SelectByTag(AutoRepeatComboBox, RepeatTag(_preferences.AutoClickRepeatMode, _preferences.AutoClickRepeatCount));
            SelectByTag(AutoPositionComboBox, _preferences.AutoClickPositionMode == ClickPositionMode.FixedPosition ? "fixed" : "current");
            AutoXTextBox.Text = _preferences.AutoClickFixedX.ToString(CultureInfo.InvariantCulture);
            AutoYTextBox.Text = _preferences.AutoClickFixedY.ToString(CultureInfo.InvariantCulture);
            FixedPositionPanel.Visibility = _preferences.AutoClickPositionMode == ClickPositionMode.FixedPosition
                ? Visibility.Visible
                : Visibility.Collapsed;
            UpdateAutoIntervalValidation();
        }
        finally
        {
            _isApplyingPreferences = false;
        }
    }

    private void PlaybackRepeatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        var (mode, count) = ParseRepeat(PlaybackRepeatComboBox, RepeatMode.Once, 1);
        _preferences.PlaybackRepeatMode = mode;
        _preferences.PlaybackRepeatCount = count;
        PersistPreferences();
    }

    private void PlaybackSpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences || GetSelectedTag(PlaybackSpeedComboBox) is not string tag)
        {
            return;
        }

        if (double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
        {
            _preferences.PlaybackSpeed = speed;
            PersistPreferences();
        }
    }

    private void RecordingOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        _preferences.CaptureMouse = CaptureMouseCheckBox.IsChecked == true;
        _preferences.CaptureKeyboard = CaptureKeyboardCheckBox.IsChecked == true;
        PersistPreferences();
    }

    private void PlaybackInterLoopDelayTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsInitialized
            || _isApplyingPreferences
            || !int.TryParse(PlaybackInterLoopDelayTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delay))
        {
            return;
        }

        _preferences.PlaybackInterLoopDelayMilliseconds = delay;
        PersistPreferences();
    }

    private void AutoOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_isApplyingPreferences || !IsInitialized)
        {
            return;
        }

        if (GetSelectedTag(AutoButtonComboBox) is string button)
        {
            _preferences.AutoClickButton = button switch
            {
                "right" => MouseButtonKind.Right,
                "middle" => MouseButtonKind.Middle,
                _ => MouseButtonKind.Left
            };
        }

        var intervalValid = UpdateAutoIntervalValidation();
        if (intervalValid && int.TryParse(AutoIntervalTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval))
        {
            _preferences.AutoClickIntervalMilliseconds = interval;
        }

        var (repeatMode, repeatCount) = ParseRepeat(AutoRepeatComboBox, RepeatMode.UntilStopped, 1);
        _preferences.AutoClickRepeatMode = repeatMode;
        _preferences.AutoClickRepeatCount = repeatCount;
        if (int.TryParse(AutoXTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x))
        {
            _preferences.AutoClickFixedX = x;
        }

        if (int.TryParse(AutoYTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
        {
            _preferences.AutoClickFixedY = y;
        }

        PersistPreferences();
    }

    private bool UpdateAutoIntervalValidation()
    {
        if (!int.TryParse(AutoIntervalTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var interval))
        {
            SetAutoIntervalValidation("Enter a whole number between 0 ms and 24 hours.");
            return false;
        }

        if (interval < AutomationLimits.MinimumAutoClickIntervalMilliseconds
            || interval > AutomationLimits.MaximumAutoClickIntervalMilliseconds)
        {
            SetAutoIntervalValidation("Use a value between 0 ms and 24 hours.");
            return false;
        }

        SetAutoIntervalValidation(null);
        return true;
    }

    private void SetAutoIntervalValidation(string? message)
    {
        _autoIntervalValid = message is null;
        AutoIntervalErrorText.Text = message ?? string.Empty;
        AutoIntervalErrorPanel.Visibility = message is null ? Visibility.Collapsed : Visibility.Visible;
        AutoIntervalTextBox.ToolTip = message;

        if (IsInitialized)
        {
            var isAutoClicking = _coordinator.State == AutomationState.AutoClicking;
            AutoClickerStartButton.IsEnabled = (_coordinator.State == AutomationState.Idle || isAutoClicking)
                && (_autoIntervalValid || isAutoClicking);
        }
    }

    private void AutoPositionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingPreferences)
        {
            return;
        }

        var isFixed = GetSelectedTag(AutoPositionComboBox) == "fixed";
        FixedPositionPanel.Visibility = isFixed ? Visibility.Visible : Visibility.Collapsed;
        _preferences.AutoClickPositionMode = isFixed ? ClickPositionMode.FixedPosition : ClickPositionMode.CurrentCursor;
        PersistPreferences();
    }

    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(character => !char.IsDigit(character) && character != '-');
    }

    private void PersistPreferences()
    {
        UserPreferencesNormalizer.Normalize(_preferences);
        try
        {
            UserPreferencesStore.Save(_preferences);
        }
        catch (PreferencesPersistenceException exception)
        {
            _startupWarning = "Preferences could not be saved: " + exception.Message;
            SetStatus(_startupWarning, isError: true);
        }
    }

    private bool TryRegisterGlobalHotkeys()
    {
        var previousRegistration = _hotkeyRegistration;
        var previousHotkeys = _hotkeys;
        _hotkeyRegistration = null;
        previousRegistration?.Dispose();
        previousHotkeys.Pressed -= Hotkeys_Pressed;
        previousHotkeys.Dispose();

        var candidate = new WindowsGlobalHotkeyService();
        candidate.Pressed += Hotkeys_Pressed;
        try
        {
            var registration = candidate.Register(
                new[]
                {
                    new HotkeyBinding(HotkeyCommand.Record, _preferences.RecordHotkeyModifiers, _preferences.RecordHotkeyVirtualKey),
                    new HotkeyBinding(HotkeyCommand.Play, _preferences.PlayHotkeyModifiers, _preferences.PlayHotkeyVirtualKey),
                    new HotkeyBinding(HotkeyCommand.Stop, _preferences.StopHotkeyModifiers, _preferences.StopHotkeyVirtualKey)
                });
            _hotkeyRegistration = registration;
            _hotkeys = candidate;
            return true;
        }
        catch (GlobalHotkeyException exception)
        {
            candidate.Pressed -= Hotkeys_Pressed;
            candidate.Dispose();
            _hotkeys = new WindowsGlobalHotkeyService();
            _hotkeys.Pressed += Hotkeys_Pressed;
            SetStatus($"Global hotkeys unavailable: {exception.Message}", isError: true);
            return false;
        }
    }

    private void UpdateShortcutHint()
    {
        var recordHotkey = HotkeyFormatting.Describe(new HotkeyBinding(HotkeyCommand.Record, _preferences.RecordHotkeyModifiers, _preferences.RecordHotkeyVirtualKey));
        var playHotkey = HotkeyFormatting.Describe(new HotkeyBinding(HotkeyCommand.Play, _preferences.PlayHotkeyModifiers, _preferences.PlayHotkeyVirtualKey));
        var stopHotkey = HotkeyFormatting.Describe(new HotkeyBinding(HotkeyCommand.Stop, _preferences.StopHotkeyModifiers, _preferences.StopHotkeyVirtualKey));
        ShortcutHint.Text = $"{recordHotkey} record  ·  {playHotkey} play  ·  {stopHotkey} stop";
        ShortcutHint.Foreground = (WpfBrush)FindResource("SubtleInkBrush");
        ShortcutHint.ToolTip = null;
        RecordButton.ToolTip = $"Start recording ({recordHotkey})";
        RecordNewMacroButton.ToolTip = $"Start recording ({recordHotkey})";
        PlayButton.ToolTip = $"Play selected macro ({playHotkey})";
        StopButton.ToolTip = $"Stop active automation ({stopHotkey})";
    }

    private static string RepeatTag(RepeatMode mode, int count)
    {
        return mode switch
        {
            RepeatMode.UntilStopped => "until_stopped",
            RepeatMode.FixedCount when count is 2 or 5 or 10 or 100 => $"count:{count}",
            _ => "once"
        };
    }

    private string DescribeRecordHotkey()
    {
        return HotkeyFormatting.Describe(new HotkeyBinding(
            HotkeyCommand.Record,
            _preferences.RecordHotkeyModifiers,
            _preferences.RecordHotkeyVirtualKey));
    }

    private static void SelectByTag(WpfComboBox comboBox, string tag)
    {
        var item = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(candidate => Equals(candidate.Tag, tag));
        comboBox.SelectedItem = item ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    private static string? GetSelectedTag(WpfComboBox comboBox)
    {
        return (comboBox.SelectedItem as ComboBoxItem)?.Tag as string;
    }

    private void AddOrUpdateMacro(MacroDocument document, string? path, bool saved)
    {
        var existing = path is null
            ? null
            : _macroItems.FirstOrDefault(item => item.FilePath is not null
                && string.Equals(item.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Update(document, path, saved);
            MacroItemsList.SelectedItem = existing;
        }
        else
        {
            var item = new MacroListItem(document, path, saved);
            _macroItems.Add(item);
            MacroItemsList.SelectedItem = item;
        }

        UpdateMacroListVisibility();
    }

    private static string EnsureMacroExtension(string path)
    {
        return path.EndsWith(MacroFileStore.FileExtension, StringComparison.OrdinalIgnoreCase)
            ? path
            : path + MacroFileStore.FileExtension;
    }

    private static string GetUniqueAutoSavePath(string name)
    {
        var directory = ProductInfo.GetMacroDirectory();
        var basePath = Path.Combine(directory, name + MacroFileStore.FileExtension);
        if (!File.Exists(basePath))
        {
            return basePath;
        }

        for (var index = 2; index <= 1000; index++)
        {
            var path = Path.Combine(directory, $"{name} ({index}){MacroFileStore.FileExtension}");
            if (!File.Exists(path))
            {
                return path;
            }
        }

        throw new IOException("A unique automatic macro file name could not be created.");
    }

    private static string GetMacroNameFromPath(string path)
    {
        var fileName = Path.GetFileName(path);
        if (fileName.EndsWith(MacroFileStore.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^MacroFileStore.FileExtension.Length];
        }

        return string.IsNullOrWhiteSpace(fileName) ? ProductInfo.RecordedMacroBaseName : fileName.Trim();
    }

    private static void SwitchSurface(UIElement visibleSurface, UIElement hiddenSurface)
    {
        hiddenSurface.Visibility = Visibility.Collapsed;
        visibleSurface.Visibility = Visibility.Visible;
        visibleSurface.Opacity = 0;
        visibleSurface.RenderTransform = new TranslateTransform(0, 8);

        var opacity = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(180)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var translate = new DoubleAnimation(8, 0, new Duration(TimeSpan.FromMilliseconds(180)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        visibleSurface.BeginAnimation(UIElement.OpacityProperty, opacity);
        ((TranslateTransform)visibleSurface.RenderTransform).BeginAnimation(TranslateTransform.YProperty, translate);
    }
}
