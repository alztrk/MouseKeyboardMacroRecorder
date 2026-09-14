using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MouseKeyboardMacroRecorder.Core.Application;
using MouseKeyboardMacroRecorder.Core.Domain;
using MouseKeyboardMacroRecorder.Infrastructure.Windows;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace MouseKeyboardMacroRecorder;

public partial class HotkeyDialog : Window
{
    public HotkeyDialog(
        uint recordVirtualKey,
        HotkeyModifiers recordModifiers,
        uint playVirtualKey,
        HotkeyModifiers playModifiers,
        uint stopVirtualKey,
        HotkeyModifiers stopModifiers)
    {
        InitializeComponent();
        SetBinding(RecordHotkeyBox, new HotkeyBinding(HotkeyCommand.Record, recordModifiers, recordVirtualKey));
        SetBinding(PlayHotkeyBox, new HotkeyBinding(HotkeyCommand.Play, playModifiers, playVirtualKey));
        SetBinding(StopHotkeyBox, new HotkeyBinding(HotkeyCommand.Stop, stopModifiers, stopVirtualKey));
    }

    public uint RecordVirtualKey { get; private set; }

    public HotkeyModifiers RecordModifiers { get; private set; }

    public uint PlayVirtualKey { get; private set; }

    public HotkeyModifiers PlayModifiers { get; private set; }

    public uint StopVirtualKey { get; private set; }

    public HotkeyModifiers StopModifiers { get; private set; }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadBinding(RecordHotkeyBox, HotkeyCommand.Record, out var record)
            || !TryReadBinding(PlayHotkeyBox, HotkeyCommand.Play, out var play)
            || !TryReadBinding(StopHotkeyBox, HotkeyCommand.Stop, out var stop))
        {
            System.Windows.MessageBox.Show(this, "Choose a key for every command.", "Hotkeys", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (record.VirtualKey == play.VirtualKey && record.Modifiers == play.Modifiers
            || record.VirtualKey == stop.VirtualKey && record.Modifiers == stop.Modifiers
            || play.VirtualKey == stop.VirtualKey && play.Modifiers == stop.Modifiers)
        {
            System.Windows.MessageBox.Show(this, "Each command needs a different key.", "Hotkeys", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RecordVirtualKey = record.VirtualKey;
        PlayVirtualKey = play.VirtualKey;
        StopVirtualKey = stop.VirtualKey;
        RecordModifiers = record.Modifiers;
        PlayModifiers = play.Modifiers;
        StopModifiers = stop.Modifiers;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private static void SetBinding(WpfTextBox textBox, HotkeyBinding binding)
    {
        textBox.Tag = binding;
        textBox.Text = HotkeyFormatting.Describe(binding);
    }

    private void HotkeyBox_PreviewKeyDown(object sender, WpfKeyEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            return;
        }

        var command = textBox.Name switch
        {
            nameof(RecordHotkeyBox) => HotkeyCommand.Record,
            nameof(PlayHotkeyBox) => HotkeyCommand.Play,
            _ => HotkeyCommand.Stop
        };
        var binding = new HotkeyBinding(command, ToHotkeyModifiers(Keyboard.Modifiers), (uint)virtualKey);
        SetBinding(textBox, binding);
        e.Handled = true;
    }

    private static bool TryReadBinding(WpfTextBox textBox, HotkeyCommand command, out HotkeyBinding binding)
    {
        if (textBox.Tag is HotkeyBinding stored && stored.Command == command)
        {
            binding = stored;
            return true;
        }

        binding = new HotkeyBinding(command, HotkeyModifiers.None, 0);
        return false;
    }

    private static HotkeyModifiers ToHotkeyModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= HotkeyModifiers.Windows;
        return result;
    }
}
