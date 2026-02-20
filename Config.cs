using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace SpiritsNotifications;

public sealed class AppConfig
{
  public string ProductId { get; set; } = "vEjlRlWp82033";
  public string UserId { get; set; } = "";
  public string ViewerUserId { get; set; } = "";
  public string ViewerName { get; set; } = "";
  public string ViewerEmail { get; set; } = "";
  public string ApiKey { get; set; } = "";
  public string ApiBaseUrl { get; set; } = "https://api.getbeamer.com/v0";
  public int MaxPosts { get; set; } = 25;
  public long RequestTimeoutMs { get; set; } = 10_000;
  public long RefreshMs { get; set; } = 60_000;
  public int WidgetWidth { get; set; } = 520;
  public bool AutoOpenOnLaunch { get; set; } = true;

  public long ManualCloseCooldownMs { get; set; } = 6_000;
  public bool PulseOnNewMessage { get; set; } = true;
  public long PulseMinIntervalMs { get; set; } = 15_000;

  public bool ForceForegroundOnUrgent { get; set; } = false;
  public int UrgentFocusUnreadDelta { get; set; } = 3;
  public long FocusStealCooldownMs { get; set; } = 45_000;
  public bool EnableViewTracking { get; set; } = true;
  public bool ArchiveMessages { get; set; } = true;

  public static readonly string FileName = "SpiritsNotifications.config.json";
  public static readonly string LegacyFileName = "Beamerviewer.config.json";

  public static (AppConfig Config, string Path) LoadOrCreate()
  {
    var preferred = Path.Combine(AppContext.BaseDirectory, FileName);
    if (TryLoad(preferred, out var cfg)) return (Normalize(cfg!), preferred);

    var legacyPreferred = Path.Combine(AppContext.BaseDirectory, LegacyFileName);
    if (TryLoadLegacy(preferred, legacyPreferred, out cfg, out var migratedPath))
      return (Normalize(cfg!), migratedPath);

    if (TryExtractEmbeddedTemplate(preferred) && TryLoad(preferred, out cfg))
      return (Normalize(cfg!), preferred);

    if (TryWrite(preferred, out cfg)) return (Normalize(cfg!), preferred);

    var fallbackDir = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "SpiritsNotifications"
    );
    Directory.CreateDirectory(fallbackDir);

    var fallback = Path.Combine(fallbackDir, FileName);
    if (TryLoad(fallback, out cfg)) return (Normalize(cfg!), fallback);

    var legacyFallback = Path.Combine(fallbackDir, LegacyFileName);
    if (TryLoadLegacy(fallback, legacyFallback, out cfg, out migratedPath))
      return (Normalize(cfg!), migratedPath);

    var legacyFallbackDir = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "Beamer_viewer"
    );
    Directory.CreateDirectory(legacyFallbackDir);

    var legacyOldAppPath = Path.Combine(legacyFallbackDir, LegacyFileName);
    if (TryLoadLegacy(fallback, legacyOldAppPath, out cfg, out migratedPath))
      return (Normalize(cfg!), migratedPath);

    if (TryExtractEmbeddedTemplate(fallback) && TryLoad(fallback, out cfg))
      return (Normalize(cfg!), fallback);

    TryWrite(fallback, out cfg);
    return (Normalize(cfg!), fallback);
  }

  private static AppConfig Normalize(AppConfig cfg)
  {
    if (string.IsNullOrWhiteSpace(cfg.ProductId)) cfg.ProductId = "vEjlRlWp82033";
    if (string.IsNullOrWhiteSpace(cfg.UserId)) cfg.UserId = $"local-{Guid.NewGuid()}";
    cfg.ViewerUserId = cfg.ViewerUserId?.Trim() ?? "";
    cfg.ViewerName = cfg.ViewerName?.Trim() ?? "";
    cfg.ViewerEmail = cfg.ViewerEmail?.Trim() ?? "";
    if (string.IsNullOrWhiteSpace(cfg.ApiBaseUrl)) cfg.ApiBaseUrl = "https://api.getbeamer.com/v0";
    cfg.ApiBaseUrl = cfg.ApiBaseUrl.Trim().TrimEnd('/');
    cfg.ApiKey = cfg.ApiKey?.Trim() ?? "";

    if (cfg.RefreshMs < 1_000) cfg.RefreshMs = 1_000;
    if (cfg.RequestTimeoutMs < 2_000) cfg.RequestTimeoutMs = 2_000;
    if (cfg.RequestTimeoutMs > 60_000) cfg.RequestTimeoutMs = 60_000;

    if (cfg.MaxPosts < 1) cfg.MaxPosts = 1;
    if (cfg.MaxPosts > 100) cfg.MaxPosts = 100;

    if (cfg.WidgetWidth < 320) cfg.WidgetWidth = 520;

    if (cfg.ManualCloseCooldownMs < 0) cfg.ManualCloseCooldownMs = 0;
    if (cfg.ManualCloseCooldownMs > 120_000) cfg.ManualCloseCooldownMs = 120_000;

    if (cfg.PulseMinIntervalMs < 0) cfg.PulseMinIntervalMs = 0;
    if (cfg.PulseMinIntervalMs > 600_000) cfg.PulseMinIntervalMs = 600_000;

    if (cfg.UrgentFocusUnreadDelta < 1) cfg.UrgentFocusUnreadDelta = 1;
    if (cfg.UrgentFocusUnreadDelta > 50) cfg.UrgentFocusUnreadDelta = 50;

    if (cfg.FocusStealCooldownMs < 0) cfg.FocusStealCooldownMs = 0;
    if (cfg.FocusStealCooldownMs > 600_000) cfg.FocusStealCooldownMs = 600_000;

    return cfg;
  }

  private static bool TryLoad(string path, out AppConfig? cfg)
  {
    cfg = null;
    try
    {
      if (!File.Exists(path)) return false;
      var raw = File.ReadAllText(path);
      cfg = JsonSerializer.Deserialize<AppConfig>(raw, new JsonSerializerOptions
      {
        PropertyNameCaseInsensitive = true
      });
      return cfg != null;
    }
    catch
    {
      return false;
    }
  }

  private static bool TryWrite(string path, out AppConfig? cfg)
  {
    cfg = null;
    try
    {
      cfg = Normalize(new AppConfig());
      return TrySave(path, cfg);
    }
    catch
    {
      cfg = Normalize(new AppConfig());
      return false;
    }
  }

  private static bool TryLoadLegacy(string targetPath, string legacyPath, out AppConfig? cfg, out string resolvedPath)
  {
    resolvedPath = targetPath;
    cfg = null;

    if (!TryLoad(legacyPath, out cfg)) return false;
    cfg = Normalize(cfg!);

    if (TrySave(targetPath, cfg))
    {
      resolvedPath = targetPath;
      return true;
    }

    resolvedPath = legacyPath;
    return true;
  }

  private static bool TrySave(string path, AppConfig cfg)
  {
    try
    {
      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrWhiteSpace(dir))
      {
        Directory.CreateDirectory(dir);
      }

      var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
      File.WriteAllText(path, json);
      return true;
    }
    catch
    {
      return false;
    }
  }

  private static bool TryExtractEmbeddedTemplate(string targetPath)
  {
    try
    {
      if (File.Exists(targetPath)) return true;

      var asm = Assembly.GetExecutingAssembly();
      var resName = asm.GetManifestResourceNames()
        .FirstOrDefault(n => n.EndsWith(FileName, StringComparison.OrdinalIgnoreCase));

      if (string.IsNullOrWhiteSpace(resName)) return false;

      using var s = asm.GetManifestResourceStream(resName);
      if (s is null) return false;

      using var fs = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
      s.CopyTo(fs);
      return true;
    }
    catch
    {
      return false;
    }
  }
}
