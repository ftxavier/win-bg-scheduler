using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Toolkit.Uwp.Notifications;
using WinBgScheduler.App.Services;
using WinBgScheduler.App.Tray;
using WinBgScheduler.App.ViewModels;
using WinBgScheduler.App.Views;
using WinBgScheduler.Core.Scheduling;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.App;

public partial class App : Application
{
    private readonly SingleInstance _instance = new();
    private ServiceProvider? _services;
    private MainWindow? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        if (!_instance.TryAcquire())
        {
            SingleInstance.SignalExisting();
            Shutdown();
            return;
        }

        // The UI is in Portuguese; keep dates and day names consistent with it.
        var culture = new CultureInfo("pt-BR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Win BG Scheduler", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _services = ConfigureServices();
        var config = _services.GetRequiredService<ConfigService>();
        ThemeHelper.Apply(config.Config.Settings.Theme);
        _ = StartAsync(e.Args, config);
    }

    private async Task StartAsync(string[] args, ConfigService config)
    {
        var firstRun = FirstRunResult.NotFirstRun;
        try
        {
            if (config.IsFirstRun)
                firstRun = await FirstRunAsync(config);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Win BG Scheduler", ex.Message);
        }
        if (firstRun == FirstRunResult.HandedOver)
            return;

        var services = _services!;
        var notifications = services.GetRequiredService<NotificationService>();
        var scheduler = services.GetRequiredService<SchedulerService>();
        notifications.Initialize();
        notifications.Activated += toast => Dispatcher.BeginInvoke(() =>
        {
            scheduler.HandleToast(toast);
            if (toast.TryGetValue("action", out string action) && action == "open")
                ShowMainWindow();
        });
        scheduler.Start();

        services.GetRequiredService<TrayIconHost>().Create(ShowMainWindow, RequestClose);
        _instance.Listen(() => Dispatcher.BeginInvoke(ShowMainWindow));

        var launchedAtLogin = args.Contains(StartupService.TrayArgument);
        var launchedFromToast = ToastNotificationManagerCompat.WasCurrentProcessToastActivated();
        if (firstRun == FirstRunResult.Installed)
            notifications.ShowInfo("Win BG Scheduler está na bandeja", "Clique com o botão direito no ícone para abrir as configurações.");
        else if (!launchedAtLogin || !config.Config.Settings.StartMinimized || launchedFromToast)
            ShowMainWindow();
    }

    private enum FirstRunResult
    {
        NotFirstRun,
        NotInstalled,
        Installed,
        HandedOver,
    }

    /// <summary>Adopts the current wallpaper as the default, then offers to install.</summary>
    private async Task<FirstRunResult> FirstRunAsync(ConfigService config)
    {
        var wallpaper = _services!.GetRequiredService<WallpaperService>();
        try
        {
            config.Config.Defaults = SetupService.CaptureCurrentWallpaper(wallpaper, wallpaper.GetMonitors());
        }
        catch (Exception ex)
        {
            // Not fatal: a default can be picked later.
            await ShowMessageAsync("Papel de parede atual", $"Não foi possível ler o papel de parede atual: {ex.Message}");
        }
        config.Save();

        if (!await AskInstallAsync(config.Config.Defaults.Count > 0))
            return FirstRunResult.NotInstalled;

        string exe;
        try
        {
            exe = SetupService.Install();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Não foi possível instalar", ex.Message);
            return FirstRunResult.NotInstalled;
        }
        config.Config.Settings.StartWithWindows = true;
        config.Save();

        if (string.Equals(exe, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            return FirstRunResult.Installed;

        HandOverTo(exe);
        return FirstRunResult.HandedOver;
    }

    /// <summary>Exits and lets the installed copy take over, starting in the tray.</summary>
    public void HandOverTo(string exe)
    {
        _services?.GetService<SchedulerService>()?.Stop();
        _instance.Dispose();
        System.Diagnostics.Process.Start(exe, StartupService.TrayArgument);
        ExitApp();
    }

    private static async Task<bool> AskInstallAsync(bool adoptedWallpaper)
    {
        var target = SetupService.IsSingleFile ? "\n• Copia o app para a pasta de programas do seu usuário" : "";
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Bem-vindo ao Win BG Scheduler",
            Content = Text(
                (adoptedWallpaper ? "Seu papel de parede atual foi definido como papel padrão.\n\n" : "") +
                $"Deseja instalar o Win BG Scheduler?{target}\n• Cria um atalho no menu Iniciar\n" +
                "• Inicia junto com o Windows\n• Fica rodando como ícone na bandeja do sistema"),
            PrimaryButtonText = "Instalar",
            CloseButtonText = "Agora não",
            MinWidth = 460,
        };
        return await box.ShowDialogAsync() == Wpf.Ui.Controls.MessageBoxResult.Primary;
    }

    private static Task ShowMessageAsync(string title, string text) =>
        new Wpf.Ui.Controls.MessageBox { Title = title, Content = Text(text), CloseButtonText = "OK" }.ShowDialogAsync();

    // A plain string is rendered justified by WPF-UI's MessageBox.
    private static System.Windows.Controls.TextBlock Text(string text) =>
        new() { Text = text, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Left };

    private bool _closing;

    /// <summary>Tray "Fechar": confirm, put the default wallpaper back, stop scheduling and exit.</summary>
    private async void RequestClose()
    {
        if (_closing || _services is null)
            return;
        _closing = true;
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Fechar o Win BG Scheduler",
            Content = Text("Tem certeza que deseja encerrar?\n\nO papel de parede padrão será aplicado e nenhum agendamento " +
                           "será executado até o app ser aberto novamente."),
            PrimaryButtonText = "Encerrar",
            CloseButtonText = "Cancelar",
            MinWidth = 420,
        };
        if (await box.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
        {
            _closing = false;
            return;
        }

        try
        {
            _services.GetRequiredService<SchedulerService>().StopAndApplyDefault();
        }
        finally
        {
            ExitApp();
        }
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ConfigService>();
        services.AddSingleton<ContentFiles>();
        services.AddSingleton(sp => new WallpaperPlanner(sp.GetRequiredService<ContentFiles>().Resolve));
        services.AddSingleton<ImageRenderService>();
        services.AddSingleton<WallpaperService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<SchedulerService>();
        services.AddSingleton<TrayIconHost>();
        services.AddSingleton<Navigator>();
        services.AddSingleton<SchedulesViewModel>();
        services.AddSingleton<UpcomingViewModel>();
        services.AddSingleton<DefaultsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();
        return services.BuildServiceProvider();
    }

    private void ShowMainWindow()
    {
        if (_services is null)
            return;
        _window ??= new MainWindow(
            _services.GetRequiredService<MainViewModel>(),
            _services.GetRequiredService<ConfigService>().Config.Settings.Theme == "System");
        _window.Show();
        if (_window.WindowState == WindowState.Minimized)
            _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void ExitApp()
    {
        if (_window is not null)
        {
            _window.AllowClose = true;
            _window.Close();
        }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.GetService<SchedulerService>()?.Stop();
        _services?.GetService<TrayIconHost>()?.Dispose();
        _services?.Dispose();
        _instance.Dispose();
        base.OnExit(e);
    }
}
