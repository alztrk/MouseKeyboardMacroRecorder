using System.Drawing;
using Forms = System.Windows.Forms;

namespace MouseKeyboardMacroRecorder.Infrastructure.Windows;

/// <summary>
/// Shows short, actionable errors through the Windows notification area.
/// </summary>
internal sealed class WindowsNotificationService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Icon? _applicationIcon;
    private bool _disposed;

    public WindowsNotificationService()
    {
        var processPath = Environment.ProcessPath;
        _applicationIcon = processPath is null ? null : Icon.ExtractAssociatedIcon(processPath);
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _applicationIcon ?? SystemIcons.Application,
            Text = "Mouse Keyboard Macro Recorder",
            Visible = true
        };
    }

    public void ShowError(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var notificationText = message.Length > 240 ? message[..237] + "..." : message;
        _notifyIcon.ShowBalloonTip(
            5000,
            "Mouse Keyboard Macro Recorder",
            notificationText,
            Forms.ToolTipIcon.Error);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _applicationIcon?.Dispose();
    }
}
