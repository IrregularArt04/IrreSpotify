using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace IrreSpotify.Services;

public class LibrespotService : IDisposable
{
    private Process? _process;
    public bool IsRunning => _process != null && !_process.HasExited;
    public string DeviceName { get; set; } = "IrreSpotify Lite";
    public event Action<string>? LogReceived;
    public event Action<bool>? StatusChanged;

    public LibrespotService()
    {
        AppDomain.CurrentDomain.ProcessExit += (s, e) => Stop();
    }

    public bool Start(string? username = null, string? password = null, int bitrate = 320)
    {
        if (IsRunning) return true;

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string exePath = Path.Combine(baseDir, "Utils", "librespot.exe");

        if (!File.Exists(exePath))
        {
            // Fallback check relative to cwd
            exePath = Path.GetFullPath(Path.Combine("Utils", "librespot.exe"));
            if (!File.Exists(exePath))
            {
                LogReceived?.Invoke($"[ERROR] librespot.exe not found at '{exePath}'");
                return false;
            }
        }

        string cachePath = Path.Combine(baseDir, "cache");
        Directory.CreateDirectory(cachePath);

        string arguments = $"--name \"{DeviceName}\" --bitrate {bitrate} --cache \"{cachePath}\"";

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            arguments += $" --username \"{username}\" --password \"{password}\"";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            _process = new Process { StartInfo = startInfo };
            _process.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    LogReceived?.Invoke($"[librespot] {e.Data}");
                }
            };
            _process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    LogReceived?.Invoke($"[librespot err] {e.Data}");
                }
            };

            bool started = _process.Start();
            if (started)
            {
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                LogReceived?.Invoke($"[INFO] librespot started successfully as '{DeviceName}'");
                StatusChanged?.Invoke(true);
                return true;
            }
        }
        catch (Exception ex)
        {
            LogReceived?.Invoke($"[ERROR] Failed to start librespot: {ex.Message}");
        }

        StatusChanged?.Invoke(false);
        return false;
    }

    public void Stop()
    {
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(true);
                _process.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[ERROR] Error stopping librespot: {ex.Message}");
            }
            finally
            {
                _process?.Dispose();
                _process = null;
                StatusChanged?.Invoke(false);
                LogReceived?.Invoke("[INFO] librespot stopped");
            }
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
