using System.Windows.Forms;
using YtLight.Core.Abstractions;

namespace YtLight.App.Services;

public sealed class TrayService : ITrayService
{
    private readonly NotifyIcon _icon;

    public TrayService()
    {
        _icon = new NotifyIcon
        {
            Text = "YtLight",
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Göster", null, (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Oynat / Duraklat", null, (_, _) => PlayPauseRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Sonraki", null, (_, _) => NextRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Uyku modu (30 dk)", null, (_, _) => SleepTimerRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => ShowWindowRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ShowWindowRequested;
    public event EventHandler? PlayPauseRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? SleepTimerRequested;
    public event EventHandler? ExitRequested;

    public void Show() => _icon.Visible = true;

    public void HideBalloon() => _icon.Visible = true;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
