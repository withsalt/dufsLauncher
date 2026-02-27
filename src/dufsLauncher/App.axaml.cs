using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using dufsLauncher.ViewModels;
using dufsLauncher.Views;

namespace dufsLauncher;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _viewModel;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var icon = CreateAppIcon();
            _viewModel = new MainWindowViewModel();
            _mainWindow = new MainWindow
            {
                DataContext = _viewModel,
                Icon = icon
            };

            SetupTrayIcon(icon);

            desktop.MainWindow = _mainWindow;
            desktop.ShutdownRequested += OnShutdownRequested;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(WindowIcon icon)
    {
        var startItem = new NativeMenuItem("启动服务") { Command = _viewModel!.StartCommand };
        var stopItem = new NativeMenuItem("终止服务") { Command = _viewModel!.StopCommand };
        var exitItem = new NativeMenuItem("退出");

        exitItem.Click += (_, _) =>
        {
            _viewModel?.SaveSettings();
            _viewModel?.Cleanup();
            _trayIcon?.Dispose();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(startItem);
        menu.Items.Add(stopItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "dufs Launcher",
            Icon = icon,
            Menu = menu,
            IsVisible = true
        };

        _trayIcon.Clicked += (_, _) =>
        {
            if (_mainWindow is not null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        };
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _viewModel?.SaveSettings();
        _viewModel?.Cleanup();
        _trayIcon?.Dispose();
    }

    private static WindowIcon CreateAppIcon()
    {
        var uri = new Uri("avares://dufsLauncher/Assets/logo.ico");
        var stream = AssetLoader.Open(uri);
        return new WindowIcon(stream);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "DataAnnotationsValidationPlugin is being removed, not used.")]
    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
