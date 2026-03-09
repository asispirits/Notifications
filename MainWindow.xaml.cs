using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Notifications;

public partial class MainWindow
{
  private const string HostName = "notifications.widget.local";
  private const string AppFolderName = "Notifications_widget_app";
  private const string CHeaderDbfPath = @"C:\TEMP\cheader.dbf";

  private readonly AppConfig _cfg;
  private readonly JsonSerializerOptions _jsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
  };

  private readonly string _viewerUserId;
  private readonly string _viewerName;
  private readonly string _viewerEmail;

  public MainWindow()
  {
    InitializeComponent();

    (_cfg, _) = AppConfig.LoadOrCreate();
    Title = _cfg.UiAppTitle;
    Width = Math.Max(420, _cfg.WidgetWidth);

    TryLoadCHeaderIdentity(out var cHeaderName, out var cHeaderUserId);

    var viewerUserIdSeed = string.IsNullOrWhiteSpace(_cfg.UserId)
      ? (string.IsNullOrWhiteSpace(_cfg.ViewerUserId)
        ? (string.IsNullOrWhiteSpace(cHeaderUserId) ? Environment.UserName : cHeaderUserId)
        : _cfg.ViewerUserId)
      : _cfg.UserId;

    var viewerNameSeed = string.IsNullOrWhiteSpace(_cfg.ViewerName)
      ? (string.IsNullOrWhiteSpace(cHeaderName) ? Environment.MachineName : cHeaderName)
      : _cfg.ViewerName;

    _viewerUserId = NormalizeIdentity(viewerUserIdSeed, "local-user");
    _viewerName = NormalizeIdentity(viewerNameSeed, _viewerUserId);
    _viewerEmail = NormalizeOptionalIdentity(_cfg.ViewerEmail, 254);

    Loaded += async (_, _) => await InitAsync();
  }

  private async Task InitAsync()
  {
    try
    {
      await Web.EnsureCoreWebView2Async();
      if (Web.CoreWebView2 is null) return;

      Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
      Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#if DEBUG
      Web.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
      Web.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif

      try
      {
        Web.CoreWebView2.Profile.PreferredTrackingPreventionLevel = CoreWebView2TrackingPreventionLevel.None;
      }
      catch
      {
      }

      Web.CoreWebView2.NewWindowRequested += (_, e) =>
      {
        e.Handled = true;
        OpenExternal(e.Uri);
      };

      Web.CoreWebView2.NavigationStarting += (_, e) =>
      {
        if (!e.IsUserInitiated) return;
        if (IsHostUri(e.Uri)) return;

        e.Cancel = true;
        OpenExternal(e.Uri);
      };

      var appFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName
      );
      Directory.CreateDirectory(appFolder);

      var indexPath = Path.Combine(appFolder, "index.html");
      var html = ReadEmbeddedText("index.html");
      File.WriteAllText(indexPath, html, Encoding.UTF8);

      Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
        HostName,
        appFolder,
        CoreWebView2HostResourceAccessKind.Allow
      );

      var segmentFilters = ResolveSegmentFilters(_cfg, _viewerUserId);

      var bootConfig = new
      {
        product_id = _cfg.ProductId,
        user_id = _viewerUserId,
        viewer_user_id = _viewerUserId,
        viewer_name = _viewerName,
        viewer_email = _viewerEmail,
        segment_role = FirstSegmentFilter(segmentFilters),
        segment_filters = segmentFilters,
        segment_force_filter = NormalizeOptionalIdentity(_cfg.SegmentForceFilter, 80),
        segment_multi_user = _cfg.SegmentMultiUser,
        app_title = _cfg.UiAppTitle,
        auto_open_on_launch = _cfg.AutoOpenOnLaunch,
        refresh_ms = _cfg.WidgetRefreshMs,
        enable_secret_menu = _cfg.EnableSecretMenu
      };

      var bootJson = JsonSerializer.Serialize(bootConfig, _jsonOptions);
      await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync($"window.__NW_CFG__ = {bootJson};");

      Web.Source = new Uri($"https://{HostName}/index.html");
    }
    catch (Exception ex)
    {
      MessageBox.Show(
        $"Failed to initialize Notifications widget host.\\n\\n{ex.Message}",
        "Notifications",
        MessageBoxButton.OK,
        MessageBoxImage.Error
      );
    }
  }

  private static bool IsHostUri(string? uri)
  {
    if (string.IsNullOrWhiteSpace(uri)) return false;
    if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return false;
    return parsed.Scheme == Uri.UriSchemeHttps &&
           string.Equals(parsed.Host, HostName, StringComparison.OrdinalIgnoreCase);
  }

  private static void OpenExternal(string? uri)
  {
    if (string.IsNullOrWhiteSpace(uri)) return;

    try
    {
      Process.Start(new ProcessStartInfo
      {
        FileName = uri,
        UseShellExecute = true
      });
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Failed to open external URI '{uri}': {ex}");
    }
  }

  private static string ReadEmbeddedText(string fileName)
  {
    var asm = typeof(App).Assembly;
    var res = asm.GetManifestResourceNames()
      .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

    if (res is null)
      throw new InvalidOperationException($"Embedded resource not found: {fileName}");

    using var s = asm.GetManifestResourceStream(res)
      ?? throw new InvalidOperationException($"Embedded stream not found: {res}");

    using var sr = new StreamReader(s, Encoding.UTF8);
    return sr.ReadToEnd();
  }

  private readonly struct DbfField
  {
    public DbfField(string name, int length)
    {
      Name = name;
      Length = length;
    }

    public string Name { get; }
    public int Length { get; }
  }

  private static bool TryLoadCHeaderIdentity(out string viewerName, out string viewerUserId)
  {
    viewerName = "";
    viewerUserId = "";

    if (!File.Exists(CHeaderDbfPath)) return false;

    try
    {
      using var fs = new FileStream(CHeaderDbfPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
      using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);

      if (fs.Length < 33) return false;

      fs.Seek(4, SeekOrigin.Begin);
      var recordCount = br.ReadInt32();
      var headerLength = br.ReadUInt16();
      var recordLength = br.ReadUInt16();

      if (recordCount <= 0 || headerLength < 33 || recordLength < 2) return false;
      if (headerLength > fs.Length) return false;

      fs.Seek(32, SeekOrigin.Begin);
      var fields = new List<DbfField>();

      while (fs.Position < headerLength)
      {
        var descriptor = br.ReadBytes(32);
        if (descriptor.Length == 0) break;
        if (descriptor[0] == 0x0D) break;
        if (descriptor.Length < 32) return false;

        var fieldName = Encoding.ASCII
          .GetString(descriptor, 0, 11)
          .TrimEnd('\0', ' ');

        if (string.IsNullOrWhiteSpace(fieldName)) continue;

        var fieldLength = descriptor[16];
        fields.Add(new DbfField(fieldName, fieldLength));
      }

      if (fields.Count == 0) return false;

      var hasCName = TryGetDbfFieldLayout(fields, "Cname", out var cnameOffset, out var cnameLength);
      var hasCThisReg = TryGetDbfFieldLayout(fields, "Cthisreg", out var cthisregOffset, out var cthisregLength);
      if (!hasCName && !hasCThisReg) return false;

      var recordsStart = headerLength;
      for (var i = 0; i < recordCount; i++)
      {
        var pos = recordsStart + ((long)i * recordLength);
        if (pos + recordLength > fs.Length) break;

        fs.Seek(pos, SeekOrigin.Begin);
        var record = br.ReadBytes(recordLength);
        if (record.Length < recordLength) break;

        if (record[0] == (byte)'*') continue;

        if (hasCName)
        {
          viewerName = ReadDbfFieldValue(record, cnameOffset, cnameLength);
        }

        if (hasCThisReg)
        {
          viewerUserId = ReadDbfFieldValue(record, cthisregOffset, cthisregLength);
        }

        return !string.IsNullOrWhiteSpace(viewerName) || !string.IsNullOrWhiteSpace(viewerUserId);
      }
    }
    catch
    {
    }

    return false;
  }

  private static bool TryGetDbfFieldLayout(IReadOnlyList<DbfField> fields, string targetFieldName, out int offset, out int length)
  {
    offset = 1;
    length = 0;

    foreach (var field in fields)
    {
      if (string.Equals(field.Name, targetFieldName, StringComparison.OrdinalIgnoreCase))
      {
        length = field.Length;
        return length > 0;
      }

      offset += field.Length;
    }

    offset = 0;
    return false;
  }

  private static string ReadDbfFieldValue(byte[] record, int offset, int length)
  {
    if (offset <= 0 || length <= 0 || offset >= record.Length) return "";

    var safeLength = Math.Min(length, record.Length - offset);
    if (safeLength <= 0) return "";

    return Encoding.Latin1
      .GetString(record, offset, safeLength)
      .Trim()
      .Trim('\0');
  }

  private static string NormalizeIdentity(string? value, string fallback)
  {
    var v = (value ?? "").Trim();
    if (string.IsNullOrWhiteSpace(v)) v = fallback;

    v = v.Trim();
    if (v.Length > 80) v = v[..80];

    if (string.IsNullOrWhiteSpace(v)) v = fallback;
    return v;
  }

  private static string NormalizeOptionalIdentity(string? value, int maxLen)
  {
    var v = (value ?? "").Trim();
    if (v.Length > maxLen) v = v[..maxLen];
    return v;
  }

  private static string ResolveSegmentFilters(AppConfig cfg, string viewerUserId)
  {
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var tokens = new List<string>();

    static void AddToken(HashSet<string> seenSet, List<string> tokenList, string value)
    {
      var v = value.Trim();
      if (string.IsNullOrWhiteSpace(v)) return;
      if (!seenSet.Add(v)) return;
      tokenList.Add(v);
    }

    static void AddFilterString(HashSet<string> seenSet, List<string> tokenList, string value)
    {
      if (string.IsNullOrWhiteSpace(value)) return;

      foreach (var token in value.Split(';', StringSplitOptions.RemoveEmptyEntries))
      {
        AddToken(seenSet, tokenList, token);
      }
    }

    AddFilterString(seen, tokens, NormalizeOptionalIdentity(cfg.SegmentFilters, 3000));
    AddToken(seen, tokens, NormalizeOptionalIdentity(cfg.SegmentRole, 80));
    AddToken(seen, tokens, NormalizeOptionalIdentity(cfg.SegmentFilter, 80));

    if (cfg.IncludeViewerUserIdSegment)
    {
      AddToken(seen, tokens, NormalizeOptionalIdentity(viewerUserId, 80));
    }

    return tokens.Count == 0 ? "" : string.Join(';', tokens);
  }

  private static string FirstSegmentFilter(string segmentFilters)
  {
    if (string.IsNullOrWhiteSpace(segmentFilters)) return "";

    foreach (var token in segmentFilters.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
      var v = token.Trim();
      if (!string.IsNullOrWhiteSpace(v)) return v;
    }

    return "";
  }
}
