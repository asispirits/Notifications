using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Notifications;

public sealed class UiConfig
{
  public bool EnableSecretMenu { get; set; } = false;
  public string AppTitle { get; set; } = "Notifications";
  public string HeaderTitle { get; set; } = "Notifications";
  public string ThemePageBackground { get; set; } = "#ffffff";
  public string ThemeHeaderStart { get; set; } = "#ffffff";
  public string ThemeHeaderEnd { get; set; } = "#f5faff";
  public string ThemeTextMain { get; set; } = "#1a4e86";
  public string ThemeTextMuted { get; set; } = "#4b6f95";
  public string ThemeAccent { get; set; } = "#0000ff";
  public string ThemeAccentSoft { get; set; } = "#80ffff";
  public string ThemeUnreadStart { get; set; } = "#6bbbf6";
  public string ThemeUnreadEnd { get; set; } = "#519fdd";
  public string ThemeReadStart { get; set; } = "#758499";
  public string ThemeReadEnd { get; set; } = "#657486";
}

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

  public UiConfig Ui { get; set; } = new();

  [JsonIgnore]
  public bool EnableSecretMenu
  {
    get => Ui.EnableSecretMenu;
    set => Ui.EnableSecretMenu = value;
  }

  [JsonIgnore]
  public string UiAppTitle
  {
    get => Ui.AppTitle;
    set => Ui.AppTitle = value;
  }

  [JsonIgnore]
  public string UiHeaderTitle
  {
    get => Ui.HeaderTitle;
    set => Ui.HeaderTitle = value;
  }

  [JsonIgnore]
  public string ThemePageBackground
  {
    get => Ui.ThemePageBackground;
    set => Ui.ThemePageBackground = value;
  }

  [JsonIgnore]
  public string ThemeHeaderStart
  {
    get => Ui.ThemeHeaderStart;
    set => Ui.ThemeHeaderStart = value;
  }

  [JsonIgnore]
  public string ThemeHeaderEnd
  {
    get => Ui.ThemeHeaderEnd;
    set => Ui.ThemeHeaderEnd = value;
  }

  [JsonIgnore]
  public string ThemeTextMain
  {
    get => Ui.ThemeTextMain;
    set => Ui.ThemeTextMain = value;
  }

  [JsonIgnore]
  public string ThemeTextMuted
  {
    get => Ui.ThemeTextMuted;
    set => Ui.ThemeTextMuted = value;
  }

  [JsonIgnore]
  public string ThemeAccent
  {
    get => Ui.ThemeAccent;
    set => Ui.ThemeAccent = value;
  }

  [JsonIgnore]
  public string ThemeAccentSoft
  {
    get => Ui.ThemeAccentSoft;
    set => Ui.ThemeAccentSoft = value;
  }

  [JsonIgnore]
  public string ThemeUnreadStart
  {
    get => Ui.ThemeUnreadStart;
    set => Ui.ThemeUnreadStart = value;
  }

  [JsonIgnore]
  public string ThemeUnreadEnd
  {
    get => Ui.ThemeUnreadEnd;
    set => Ui.ThemeUnreadEnd = value;
  }

  [JsonIgnore]
  public string ThemeReadStart
  {
    get => Ui.ThemeReadStart;
    set => Ui.ThemeReadStart = value;
  }

  [JsonIgnore]
  public string ThemeReadEnd
  {
    get => Ui.ThemeReadEnd;
    set => Ui.ThemeReadEnd = value;
  }

  public static readonly string FileName = "Notifications.config.json";
  public static readonly string LegacyFileName = "Beamerviewer.config.json";
  private static readonly Regex HexColorRegex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

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
      "Notifications"
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

    cfg.Ui ??= new UiConfig();
    cfg.Ui.AppTitle = NormalizeTitle(cfg.Ui.AppTitle, "Notifications");
    cfg.Ui.HeaderTitle = NormalizeTitle(cfg.Ui.HeaderTitle, cfg.Ui.AppTitle);
    cfg.Ui.ThemePageBackground = NormalizeHexColor(cfg.Ui.ThemePageBackground, "#ffffff");
    cfg.Ui.ThemeHeaderStart = NormalizeHexColor(cfg.Ui.ThemeHeaderStart, "#ffffff");
    cfg.Ui.ThemeHeaderEnd = NormalizeHexColor(cfg.Ui.ThemeHeaderEnd, "#f5faff");
    cfg.Ui.ThemeTextMain = NormalizeHexColor(cfg.Ui.ThemeTextMain, "#1a4e86");
    cfg.Ui.ThemeTextMuted = NormalizeHexColor(cfg.Ui.ThemeTextMuted, "#4b6f95");
    cfg.Ui.ThemeAccent = NormalizeHexColor(cfg.Ui.ThemeAccent, "#0000ff");
    cfg.Ui.ThemeAccentSoft = NormalizeHexColor(cfg.Ui.ThemeAccentSoft, "#80ffff");
    cfg.Ui.ThemeUnreadStart = NormalizeHexColor(cfg.Ui.ThemeUnreadStart, "#6bbbf6");
    cfg.Ui.ThemeUnreadEnd = NormalizeHexColor(cfg.Ui.ThemeUnreadEnd, "#519fdd");
    cfg.Ui.ThemeReadStart = NormalizeHexColor(cfg.Ui.ThemeReadStart, "#758499");
    cfg.Ui.ThemeReadEnd = NormalizeHexColor(cfg.Ui.ThemeReadEnd, "#657486");

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

      if (cfg is null) return false;
      ApplyLegacyUiFromRawJson(raw, cfg);
      return true;
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

  public static bool Save(string path, AppConfig cfg)
  {
    return TrySave(path, Normalize(cfg));
  }

  private static void ApplyLegacyUiFromRawJson(string raw, AppConfig cfg)
  {
    try
    {
      using var doc = JsonDocument.Parse(raw);
      if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
      var root = doc.RootElement;

      if (TryReadBoolProperty(root, "EnableSecretMenu", out var enableSecretMenu)) cfg.Ui.EnableSecretMenu = enableSecretMenu;
      if (TryReadStringProperty(root, "UiAppTitle", out var appTitle)) cfg.Ui.AppTitle = appTitle;
      if (TryReadStringProperty(root, "UiHeaderTitle", out var headerTitle)) cfg.Ui.HeaderTitle = headerTitle;
      if (TryReadStringProperty(root, "ThemePageBackground", out var themePageBackground)) cfg.Ui.ThemePageBackground = themePageBackground;
      if (TryReadStringProperty(root, "ThemeHeaderStart", out var themeHeaderStart)) cfg.Ui.ThemeHeaderStart = themeHeaderStart;
      if (TryReadStringProperty(root, "ThemeHeaderEnd", out var themeHeaderEnd)) cfg.Ui.ThemeHeaderEnd = themeHeaderEnd;
      if (TryReadStringProperty(root, "ThemeTextMain", out var themeTextMain)) cfg.Ui.ThemeTextMain = themeTextMain;
      if (TryReadStringProperty(root, "ThemeTextMuted", out var themeTextMuted)) cfg.Ui.ThemeTextMuted = themeTextMuted;
      if (TryReadStringProperty(root, "ThemeAccent", out var themeAccent)) cfg.Ui.ThemeAccent = themeAccent;
      if (TryReadStringProperty(root, "ThemeAccentSoft", out var themeAccentSoft)) cfg.Ui.ThemeAccentSoft = themeAccentSoft;
      if (TryReadStringProperty(root, "ThemeUnreadStart", out var themeUnreadStart)) cfg.Ui.ThemeUnreadStart = themeUnreadStart;
      if (TryReadStringProperty(root, "ThemeUnreadEnd", out var themeUnreadEnd)) cfg.Ui.ThemeUnreadEnd = themeUnreadEnd;
      if (TryReadStringProperty(root, "ThemeReadStart", out var themeReadStart)) cfg.Ui.ThemeReadStart = themeReadStart;
      if (TryReadStringProperty(root, "ThemeReadEnd", out var themeReadEnd)) cfg.Ui.ThemeReadEnd = themeReadEnd;
    }
    catch
    {
    }
  }

  private static bool TryReadStringProperty(JsonElement obj, string propertyName, out string value)
  {
    value = "";
    if (!obj.TryGetProperty(propertyName, out var prop)) return false;

    value = prop.ValueKind switch
    {
      JsonValueKind.String => prop.GetString() ?? "",
      JsonValueKind.Number => prop.GetRawText(),
      JsonValueKind.True => "true",
      JsonValueKind.False => "false",
      _ => ""
    };

    value = value.Trim();
    return !string.IsNullOrWhiteSpace(value);
  }

  private static bool TryReadBoolProperty(JsonElement obj, string propertyName, out bool value)
  {
    value = false;
    if (!obj.TryGetProperty(propertyName, out var prop)) return false;

    if (prop.ValueKind == JsonValueKind.True)
    {
      value = true;
      return true;
    }

    if (prop.ValueKind == JsonValueKind.False)
    {
      value = false;
      return true;
    }

    if (prop.ValueKind == JsonValueKind.String)
    {
      var raw = (prop.GetString() ?? "").Trim();
      if (bool.TryParse(raw, out var parsedBool))
      {
        value = parsedBool;
        return true;
      }

      if (raw == "1")
      {
        value = true;
        return true;
      }

      if (raw == "0")
      {
        value = false;
        return true;
      }
    }

    if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var parsedNumber))
    {
      value = parsedNumber != 0;
      return true;
    }

    return false;
  }

  private static string NormalizeTitle(string? value, string fallback)
  {
    var v = (value ?? "").Trim();
    if (string.IsNullOrWhiteSpace(v)) return fallback;
    return v.Length <= 80 ? v : v[..80];
  }

  private static string NormalizeHexColor(string? value, string fallback)
  {
    var v = (value ?? "").Trim();
    if (!HexColorRegex.IsMatch(v)) return fallback;
    return v.ToLowerInvariant();
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
