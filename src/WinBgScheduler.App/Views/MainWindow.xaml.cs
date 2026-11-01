using System.ComponentModel;
using WinBgScheduler.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WinBgScheduler.App.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel, bool followSystemTheme)
    {
        DataContext = viewModel;
        InitializeComponent();
        if (followSystemTheme)
            Loaded += (_, _) => SystemThemeWatcher.Watch(this);
    }

    /// <summary>Closing hides to the tray; the app keeps running.</summary>
    public bool AllowClose { get; set; }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
