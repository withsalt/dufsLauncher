using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace dufsLauncher.Services;

public class DufsService : IDisposable
{
    private Process? _process;
    private readonly StringBuilder _errorOutput = new();

    public bool IsRunning => _process is { HasExited: false };

    public event EventHandler? Exited;

    public static string GetDufsPath()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var rid = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows), RuntimeInformation.OSArchitecture) switch
        {
            (true, Architecture.X64) => "win-x64",
            (true, Architecture.Arm64) => "win-arm64",
            (_, Architecture.X64) when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "linux-x64",
            (_, Architecture.Arm64) when RuntimeInformation.IsOSPlatform(OSPlatform.Linux) => "linux-arm64",
            (_, Architecture.X64) when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "osx-x64",
            (_, Architecture.Arm64) when RuntimeInformation.IsOSPlatform(OSPlatform.OSX) => "osx-arm64",
            _ => throw new PlatformNotSupportedException("不支持当前平台")
        };

        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dufs.exe" : "dufs";
        string currentDufsPath = Path.Combine(basePath, exeName);
        if (File.Exists(currentDufsPath))
        {
            return currentDufsPath;
        }
        return Path.Combine(basePath, "runtimes", rid, "bin", exeName);
    }

    public async Task StartAsync(string servePath, int port, bool allPermissions)
    {
        if (IsRunning)
            throw new InvalidOperationException("服务已在运行中");

        var dufsPath = GetDufsPath();
        if (!File.Exists(dufsPath))
            throw new FileNotFoundException($"未找到 dufs 程序: {dufsPath}");

        var args = $"--port {port}";
        if (allPermissions)
            args += " --allow-all";
        args += $" \"{servePath.TrimEnd('\\', '/')}\"";

        _errorOutput.Clear();

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = dufsPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
            EnableRaisingEvents = true
        };

        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                _errorOutput.AppendLine(e.Data);
        };
        _process.Exited += (_, _) => Exited?.Invoke(this, EventArgs.Empty);

        if (!_process.Start())
            throw new InvalidOperationException("启动 dufs 进程失败");

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await Task.Delay(500);

        if (_process.HasExited)
        {
            var error = _errorOutput.ToString().Trim();
            var exitCode = _process.ExitCode;
            _process.Dispose();
            _process = null;

            var message = !string.IsNullOrEmpty(error)
                ? error
                : $"dufs 启动后立即退出 (退出码: {exitCode})";
            throw new InvalidOperationException(message);
        }
    }

    public void Stop()
    {
        if (_process is null || _process.HasExited)
            return;

        try
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"终止服务失败: {ex.Message}", ex);
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public void Dispose()
    {
        if (IsRunning)
        {
            try { _process!.Kill(entireProcessTree: true); } catch { }
        }
        _process?.Dispose();
        GC.SuppressFinalize(this);
    }
}
