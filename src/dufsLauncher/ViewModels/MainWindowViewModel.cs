using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using dufsLauncher.Services;

namespace dufsLauncher.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly DufsService _dufsService = new();

    private static readonly IBrush RunningDot = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush StoppedDot = new SolidColorBrush(Color.Parse("#95A5A6"));
    private static readonly IBrush RunningBarBg = new SolidColorBrush(Color.Parse("#27AE60"));
    private static readonly IBrush StoppedBarBg = new SolidColorBrush(Color.Parse("#F0F0F0"));
    private static readonly IBrush RunningBarFg = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IBrush StoppedBarFg = new SolidColorBrush(Color.Parse("#666666"));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ServePathError))]
    [NotifyPropertyChangedFor(nameof(HasServePathError))]
    private string _servePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PortError))]
    [NotifyPropertyChangedFor(nameof(HasPortError))]
    private decimal _port = 5000;

    [ObservableProperty]
    private bool _isAllPermissions = true;

    public string? ServePathError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ServePath))
                return null;
            if (!Directory.Exists(ServePath))
                return "路径不存在或无法访问";
            return null;
        }
    }

    public bool HasServePathError => ServePathError is not null;

    public string? PortError
    {
        get
        {
            var port = (int)Port;
            if (port < 1 || port > 65535)
                return "端口范围: 1 ~ 65535";
            if (port < 1024)
                return "低于 1024 的端口可能需要管理员权限";
            return null;
        }
    }

    public bool HasPortError => PortError is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusDetail))]
    [NotifyPropertyChangedFor(nameof(StatusBrush))]
    [NotifyPropertyChangedFor(nameof(StatusBarBackground))]
    [NotifyPropertyChangedFor(nameof(StatusBarForeground))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning;

    public string StatusText => IsRunning ? "● 服务运行中" : "○ 服务已停止";

    public string StatusDetail => IsRunning
        ? $"端口 {(int)Port}  |  {(IsAllPermissions ? "所有权限" : "只读")}  |  {ServePath}"
        : "就绪";

    public IBrush StatusBrush => IsRunning ? RunningDot : StoppedDot;
    public IBrush StatusBarBackground => IsRunning ? RunningBarBg : StoppedBarBg;
    public IBrush StatusBarForeground => IsRunning ? RunningBarFg : StoppedBarFg;

    public Func<Task<string?>>? BrowseFolderInteraction { get; set; }
    public Func<string, string, Task>? ShowErrorInteraction { get; set; }

    public MainWindowViewModel()
    {
        _dufsService.Exited += (_, _) =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsRunning = false);

        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = SettingsService.Load();

        ServePath = settings.ServePath;
        Port = settings.Port;
        IsAllPermissions = settings.IsAllPermissions;

        OnPropertyChanged(nameof(ServePath));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(IsAllPermissions));
    }

    public void SaveSettings()
    {
        SettingsService.Save(new AppSettings
        {
            ServePath = ServePath,
            Port = (int)Port,
            IsAllPermissions = IsAllPermissions
        });
    }

    partial void OnServePathChanged(string value) => SaveSettings();
    partial void OnPortChanged(decimal value) => SaveSettings();
    partial void OnIsAllPermissionsChanged(bool value) => SaveSettings();

    private bool CanStart() => !IsRunning;
    private bool CanStop() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task Start()
    {
        if (string.IsNullOrWhiteSpace(ServePath))
        {
            await ShowError("启动失败", "请先选择服务路径");
            return;
        }

        if (!Directory.Exists(ServePath))
        {
            await ShowError("启动失败", $"路径不存在或无法访问:\n{ServePath}");
            return;
        }

        var port = (int)Port;
        if (port < 1 || port > 65535)
        {
            await ShowError("启动失败", "端口号必须在 1 ~ 65535 之间");
            return;
        }

        if (IsPortInUse(port))
        {
            await ShowError("启动失败", $"端口 {port} 已被其他程序占用");
            return;
        }

        try
        {
            await _dufsService.StartAsync(ServePath, port, IsAllPermissions);
            IsRunning = true;
        }
        catch (Exception ex)
        {
            await ShowError("启动失败", ex.Message);
        }
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task Stop()
    {
        try
        {
            _dufsService.Stop();
            IsRunning = false;
        }
        catch (Exception ex)
        {
            await ShowError("终止失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task BrowseFolder()
    {
        if (BrowseFolderInteraction is { } interact)
        {
            var path = await interact();
            if (path is not null)
                ServePath = path;
        }
    }

    public void Cleanup() => _dufsService.Dispose();

    private async Task ShowError(string title, string message)
    {
        if (ShowErrorInteraction is { } interact)
            await interact(title, message);
    }
}
