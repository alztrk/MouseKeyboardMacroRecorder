using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HotkeyDefaults = MouseKeyboardMacroRecorder.Core.Domain.HotkeyDefaults;

namespace MouseKeyboardMacroRecorder;

public partial class HotkeyDialog : Window
{
    public HotkeyDialog(uint recordVirtualKey, uint playVirtualKey, uint stopVirtualKey)
    {
        InitializeComponent();
        Populate(RecordComboBox, HotkeyDefaults.RecordVirtualKeys);
        Populate(PlayComboBox, HotkeyDefaults.PlayVirtualKeys);
        Populate(StopComboBox, HotkeyDefaults.StopVirtualKeys);
        SelectByTag(RecordComboBox, recordVirtualKey);
        SelectByTag(PlayComboBox, playVirtualKey);
        SelectByTag(StopComboBox, stopVirtualKey);
    }

    public uint RecordVirtualKey { get; private set; }

    public uint PlayVirtualKey { get; private set; }

    public uint StopVirtualKey { get; private set; }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadKey(RecordComboBox, out var record)
            || !TryReadKey(PlayComboBox, out var play)
            || !TryReadKey(StopComboBox, out var stop))
        {
            MessageBox.Show(this, "Choose a key for every command.", "Hotkeys", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (record == play || record == stop || play == stop)
        {
            MessageBox.Show(this, "Each command needs a different key.", "Hotkeys", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        RecordVirtualKey = record;
        PlayVirtualKey = play;
        StopVirtualKey = stop;
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
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private static void SelectByTag(ComboBox comboBox, uint virtualKey)
    {
        var tag = virtualKey.ToString(CultureInfo.InvariantCulture);
        comboBox.SelectedItem = comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(item => Equals(item.Tag, tag))
            ?? comboBox.Items.OfType<ComboBoxItem>().FirstOrDefault();
    }

    private static void Populate(ComboBox comboBox, IReadOnlyList<uint> virtualKeys)
    {
        foreach (var virtualKey in virtualKeys)
        {
            comboBox.Items.Add(new ComboBoxItem
            {
                Content = HotkeyDefaults.GetDisplayName(virtualKey),
                Tag = virtualKey.ToString(CultureInfo.InvariantCulture)
            });
        }
    }

    private static bool TryReadKey(ComboBox comboBox, out uint virtualKey)
    {
        return uint.TryParse(
            (comboBox.SelectedItem as ComboBoxItem)?.Tag as string,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out virtualKey);
    }
}
