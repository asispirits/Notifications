using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;

namespace Notifications;

public partial class App : Application
{
  private const string SingleInstanceMutexName = "Global\\NotificationsWidget.SingleInstance";
  private const int SwRestore = 9;
  private const string RegisterWatchdogArg = "--register-watchdog";
  private const string SegmentationBootstrapScriptName = "Configure-EndpointSegmentation.bat";

  private Mutex? _singleInstanceMutex;
  private bool _ownsMutex;

  [DllImport("user32.dll")]
  private static extern bool SetForegroundWindow(IntPtr hWnd);

  [DllImport("user32.dll")]
  private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

  [DllImport("user32.dll")]
  private static extern bool IsIconic(IntPtr hWnd);

  protected override void OnStartup(StartupEventArgs e)
  {
    if (e.Args.Any(arg => string.Equals(arg, RegisterWatchdogArg, StringComparison.OrdinalIgnoreCase)))
    {
      TryEnsureWatchdogTask(requestElevationIfNeeded: false, forceCreate: true);
      Shutdown();
      return;
    }

    _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);

    if (!createdNew)
    {
      TryActivateExistingInstance();
      Shutdown();
      return;
    }

    _ownsMutex = true;

    TryInitializeSegmentationOnFirstRun();

    TryEnsureWatchdogTask(requestElevationIfNeeded: true, forceCreate: false);

    base.OnStartup(e);

    MainWindow = new MainWindow();
    MainWindow.Show();
  }

  protected override void OnExit(ExitEventArgs e)
  {
    try
    {
      if (_ownsMutex)
      {
        _singleInstanceMutex?.ReleaseMutex();
      }
    }
    catch
    {
    }
    finally
    {
      _singleInstanceMutex?.Dispose();
      _singleInstanceMutex = null;
      _ownsMutex = false;
    }

    base.OnExit(e);
  }

  private static void TryInitializeSegmentationOnFirstRun()
  {
    try
    {
      var (cfg, cfgPath) = AppConfig.LoadOrCreate();
      if (cfg.SegmentationInitialized) return;

      var userId = ResolveBootstrapUserId(cfg);
      var scriptApplied = false;

      if (string.IsNullOrWhiteSpace(cfg.SegmentFilters))
      {
        scriptApplied = TryRunSegmentationBootstrapScript(cfgPath, userId);
      }

      (cfg, cfgPath) = AppConfig.LoadOrCreate();

      var changed = false;

      if (string.IsNullOrWhiteSpace(cfg.UserId))
      {
        cfg.UserId = userId;
        changed = true;
      }

      if (string.IsNullOrWhiteSpace(cfg.SegmentFilters))
      {
        cfg.SegmentFilters = BuildDefaultSegmentFilters(cfg.UserId);
        changed = true;
      }

      if (!cfg.SegmentMultiUser)
      {
        cfg.SegmentMultiUser = true;
        changed = true;
      }

      if (!cfg.SegmentationInitialized)
      {
        cfg.SegmentationInitialized = true;
        changed = true;
      }

      if (changed)
      {
        AppConfig.Save(cfgPath, cfg);
      }

      if (scriptApplied)
      {
        Debug.WriteLine("First-run segmentation initialized via bootstrap script.");
      }
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"First-run segmentation initialization failed: {ex}");
    }
  }

  private static bool TryRunSegmentationBootstrapScript(string cfgPath, string userId)
  {
    try
    {
      var scriptPath = Path.Combine(AppContext.BaseDirectory, "scripts", SegmentationBootstrapScriptName);
      if (!File.Exists(scriptPath)) return false;

      using var proc = new Process();
      proc.StartInfo.FileName = "cmd.exe";
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.CreateNoWindow = true;
      proc.StartInfo.RedirectStandardOutput = true;
      proc.StartInfo.RedirectStandardError = true;

      proc.StartInfo.ArgumentList.Add("/c");
      proc.StartInfo.ArgumentList.Add(scriptPath);
      proc.StartInfo.ArgumentList.Add(cfgPath);
      proc.StartInfo.ArgumentList.Add(userId);
      proc.StartInfo.ArgumentList.Add("default");
      proc.StartInfo.ArgumentList.Add("default");
      proc.StartInfo.ArgumentList.Add("default");
      proc.StartInfo.ArgumentList.Add(userId);
      proc.StartInfo.ArgumentList.Add("endpoint");
      proc.StartInfo.ArgumentList.Add("prod");

      proc.Start();

      var stdout = proc.StandardOutput.ReadToEnd();
      var stderr = proc.StandardError.ReadToEnd();
      proc.WaitForExit();

      if (proc.ExitCode == 0) return true;

      Debug.WriteLine($"Segmentation bootstrap script failed ({proc.ExitCode}). stdout={stdout}; stderr={stderr}");
      return false;
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Unable to run segmentation bootstrap script: {ex}");
      return false;
    }
  }

  private static string ResolveBootstrapUserId(AppConfig cfg)
  {
    var seed = string.IsNullOrWhiteSpace(cfg.UserId)
      ? (string.IsNullOrWhiteSpace(cfg.ViewerUserId) ? Environment.UserName : cfg.ViewerUserId)
      : cfg.UserId;

    return NormalizeSegmentToken(seed, "local-user");
  }

  private static string BuildDefaultSegmentFilters(string userId)
  {
    var normalizedUserId = NormalizeSegmentToken(userId, "local-user");
    return $"env:prod;device:{normalizedUserId}";
  }

  private static string NormalizeSegmentToken(string? value, string fallback)
  {
    var v = (value ?? "").Trim().ToLowerInvariant();
    if (string.IsNullOrWhiteSpace(v)) v = fallback;

    v = v.Replace(' ', '-');
    var cleaned = new string(v.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_').ToArray());
    if (string.IsNullOrWhiteSpace(cleaned)) cleaned = fallback;

    if (cleaned.Length > 80) cleaned = cleaned[..80];
    return cleaned;
  }

  private static void TryEnsureWatchdogTask(bool requestElevationIfNeeded, bool forceCreate)
  {
    try
    {
      var (cfg, _) = AppConfig.LoadOrCreate();
      if (!cfg.EnableLaunchWatchdogTask) return;

      var taskName = string.IsNullOrWhiteSpace(cfg.LaunchWatchdogTaskName)
        ? "Notifications Widget Watchdog"
        : cfg.LaunchWatchdogTaskName.Trim();

      var intervalMinutes = Math.Clamp(cfg.LaunchWatchdogIntervalMinutes, 5, 240);

      var exePath = Process.GetCurrentProcess().MainModule?.FileName;
      if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return;

      if (!forceCreate && TaskExists(taskName)) return;

      if (IsCurrentProcessElevated())
      {
        CreateOrUpdateWatchdogTask(taskName, exePath, intervalMinutes);
        return;
      }

      if (!requestElevationIfNeeded) return;

      TryLaunchElevatedWatchdogRegistration(exePath);
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Watchdog task setup failed: {ex}");
    }
  }

  private static void TryLaunchElevatedWatchdogRegistration(string exePath)
  {
    try
    {
      var psi = new ProcessStartInfo
      {
        FileName = exePath,
        Arguments = RegisterWatchdogArg,
        UseShellExecute = true,
        Verb = "runas",
        WindowStyle = ProcessWindowStyle.Hidden
      };

      Process.Start(psi);
    }
    catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
    {
      Debug.WriteLine("Watchdog elevation prompt was canceled by user.");
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Failed to launch elevated watchdog registration: {ex}");
    }
  }

  private static bool TaskExists(string taskName)
  {
    try
    {
      using var proc = new Process();
      proc.StartInfo.FileName = "schtasks.exe";
      proc.StartInfo.UseShellExecute = false;
      proc.StartInfo.CreateNoWindow = true;
      proc.StartInfo.RedirectStandardOutput = true;
      proc.StartInfo.RedirectStandardError = true;

      proc.StartInfo.ArgumentList.Add("/Query");
      proc.StartInfo.ArgumentList.Add("/TN");
      proc.StartInfo.ArgumentList.Add(taskName);

      proc.Start();
      proc.WaitForExit();
      return proc.ExitCode == 0;
    }
    catch
    {
      return false;
    }
  }

  private static void CreateOrUpdateWatchdogTask(string taskName, string exePath, int intervalMinutes)
  {
    var processName = Path.GetFileNameWithoutExtension(exePath);
    var escapedPathForPs = exePath.Replace("'", "''");

    var action =
      $"powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"if (-not (Get-Process -Name '{processName}' -ErrorAction SilentlyContinue)) {{ Start-Process -FilePath '{escapedPathForPs}' }}\"";

    using var proc = new Process();
    proc.StartInfo.FileName = "schtasks.exe";
    proc.StartInfo.UseShellExecute = false;
    proc.StartInfo.CreateNoWindow = true;
    proc.StartInfo.RedirectStandardOutput = true;
    proc.StartInfo.RedirectStandardError = true;

    proc.StartInfo.ArgumentList.Add("/Create");
    proc.StartInfo.ArgumentList.Add("/F");
    proc.StartInfo.ArgumentList.Add("/TN");
    proc.StartInfo.ArgumentList.Add(taskName);
    proc.StartInfo.ArgumentList.Add("/TR");
    proc.StartInfo.ArgumentList.Add(action);
    proc.StartInfo.ArgumentList.Add("/SC");
    proc.StartInfo.ArgumentList.Add("MINUTE");
    proc.StartInfo.ArgumentList.Add("/MO");
    proc.StartInfo.ArgumentList.Add(intervalMinutes.ToString(CultureInfo.InvariantCulture));
    proc.StartInfo.ArgumentList.Add("/RL");
    proc.StartInfo.ArgumentList.Add("HIGHEST");

    var currentUser = WindowsIdentity.GetCurrent()?.Name;
    if (!string.IsNullOrWhiteSpace(currentUser))
    {
      proc.StartInfo.ArgumentList.Add("/RU");
      proc.StartInfo.ArgumentList.Add(currentUser);
    }

    proc.Start();

    var stdout = proc.StandardOutput.ReadToEnd();
    var stderr = proc.StandardError.ReadToEnd();
    proc.WaitForExit();

    if (proc.ExitCode != 0)
    {
      Debug.WriteLine($"schtasks /Create failed ({proc.ExitCode}). stdout={stdout}; stderr={stderr}");
    }
    else
    {
      Debug.WriteLine($"Watchdog task '{taskName}' created/updated. Interval={intervalMinutes}m");
    }
  }

  private static bool IsCurrentProcessElevated()
  {
    try
    {
      using var identity = WindowsIdentity.GetCurrent();
      var principal = new WindowsPrincipal(identity);
      return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch
    {
      return false;
    }
  }

  private static void TryActivateExistingInstance()
  {
    try
    {
      var current = Process.GetCurrentProcess();
      var peer = Process
        .GetProcessesByName(current.ProcessName)
        .FirstOrDefault(p => p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero);

      if (peer is null) return;

      var hwnd = peer.MainWindowHandle;
      if (hwnd == IntPtr.Zero) return;

      if (IsIconic(hwnd))
      {
        ShowWindowAsync(hwnd, SwRestore);
      }

      SetForegroundWindow(hwnd);
    }
    catch
    {
    }
  }
}
