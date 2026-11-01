using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using WinBgScheduler.App.Services;

namespace WinBgScheduler.App.Tray;

/// <summary>The notification-area icon. Right-click: Configurações / Fechar; double-click opens the window.</summary>
public sealed class TrayIconHost(SchedulerService scheduler) : IDisposable
{
    private TaskbarIcon? _icon;

    public void Create(Action openSettings, Action requestClose)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("Configurações", openSettings));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Fechar", requestClose));

        var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico")).Stream;
        var size = (int)Math.Round(16 * GetDpiForSystem() / 96.0);
        _icon = new TaskbarIcon
        {
            Icon = new System.Drawing.Icon(stream, size, size),
            ToolTipText = "Win BG Scheduler",
            ContextMenu = menu,
            MenuActivation = H.NotifyIcon.Core.PopupActivationMode.RightClick,
            DoubleClickCommand = new RelayCommand(openSettings),
        };
        _icon.ForceCreate(enablesEfficiencyMode: false);

        scheduler.Evaluated += (_, _) => RefreshTooltip();
        RefreshTooltip();
    }

    private void RefreshTooltip()
    {
        if (_icon is null)
            return;
        // The tray tooltip is limited to 127 characters.
        var text = $"Win BG Scheduler\n{scheduler.CurrentText}\n{scheduler.NextText}";
        _icon.ToolTipText = text.Length > 127 ? text[..124] + "..." : text;
    }

    private static MenuItem Item(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    public void Dispose() => _icon?.Dispose();
}
