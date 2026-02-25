using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace Notifications;

public partial class MainWindow
{
  private const string HostName = "notifications.local";
  private const string AppFolderName = "Notifications_app";
  private const string BeamerTrackBaseUrl = "https://backend.getbeamer.com/track";
  private const string BeamerTrackViewsBaseUrl = "https://app.getbeamer.com/trackViews";
  private const string CHeaderDbfPath = @"C:\TEMP\cheader.dbf";
  private const string ArchiveFolderName = "MESSAGE_ARCHIVE";

  private readonly AppConfig _cfg;
  private readonly string _cfgPath;

  private bool _allowClose;
  private DateTime _lastForcedForegroundAt = DateTime.MinValue;

  private readonly HttpClient _http = new();
  private readonly JsonSerializerOptions _jsonOptions = new()
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
  };

  private CancellationTokenSource? _pollCts;
  private string? _lastStateEnvelopeJson;
  private readonly object _stateLock = new();
  private string? _lastArchiveEnvelopeJson;
  private readonly object _archiveLock = new();
  private readonly string _archiveFolderPath;
  private readonly bool _archiveMessagesEnabled;
  private List<ArchivedMessage> _archivedMessages = new();

  private string _lastFingerprint = "";
  private HashSet<string> _knownPostIds = new(StringComparer.Ordinal);
  private List<UiPost> _latestPosts = new();
  private int? _lastUnreadCount;
  private DateTimeOffset _lastPostsFetchAt = DateTimeOffset.MinValue;
  private AuthMode _authMode = AuthMode.Unknown;
  private bool _pollStarted;
  private readonly string _viewerUserId;
  private readonly string _viewerFirstName;
  private readonly string _viewerLastName;
  private readonly string _viewerEmail;
  private readonly string _beamerVisitorUserId;
  private readonly ConcurrentDictionary<string, byte> _trackedViewPostIds = new(StringComparer.Ordinal);
  private DateTimeOffset _publicDescriptionCacheAt = DateTimeOffset.MinValue;
  private Dictionary<string, string> _publicDescriptionByTitle = new(StringComparer.Ordinal);
  private Dictionary<string, string> _publicDescriptionBySlug = new(StringComparer.Ordinal);
  private readonly SemaphoreSlim _publicDescriptionCacheGate = new(1, 1);

  private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled | RegexOptions.Singleline);
  private static readonly string[] TrackIdPropertyNames =
  {
    "descriptionId",
    "description_id",
    "id",
    "postId",
    "post_id",
    "announcementId",
    "announcement_id",
    "featureId",
    "feature_id",
    "changelogId",
    "changelog_id",
    "notificationId",
    "notification_id",
    "_id"
  };
  private static readonly Regex PublicDescriptionRegex = new(
    "data-description-id=\"(?<id>\\d+)\"[^>]*data-post-title=\"(?<title>[^\"]*)\"",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);
  private static readonly Regex HrefRegex = new(
    "href=\"(?<href>https://app\\.getbeamer\\.com/[^\"]+)\"",
    RegexOptions.Compiled | RegexOptions.IgnoreCase);
  private static readonly Regex HexColorRegex = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);
  private static readonly TimeSpan PostFetchFallbackInterval = TimeSpan.FromMinutes(3);
  private static readonly TimeSpan PublicDescriptionCacheTtl = TimeSpan.FromMinutes(2);

  private const int WM_CLOSE = 0x0010;
  private const int WM_SYSCOMMAND = 0x0112;
  private const int SC_CLOSE = 0xF060;

  private const int SW_RESTORE = 9;

  private const uint FLASHW_ALL = 3;
  private const uint FLASHW_TIMERNOFG = 12;

  private enum AuthMode
  {
    Unknown,
    BearerHeader,
    XBeamerApiKeyHeader,
    XApiKeyHeader,
    QueryParameter
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct FLASHWINFO
  {
    public uint cbSize;
    public IntPtr hwnd;
    public uint dwFlags;
    public uint uCount;
    public uint dwTimeout;
  }

  [DllImport("user32.dll")]
  private static extern bool SetForegroundWindow(IntPtr hWnd);

  [DllImport("user32.dll")]
  private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

  [DllImport("user32.dll")]
  private static extern bool IsIconic(IntPtr hWnd);

  [DllImport("user32.dll")]
  private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

  [DllImport("user32.dll")]
  private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

  [DllImport("user32.dll")]
  private static extern uint GetCurrentThreadId();

  [DllImport("user32.dll")]
  private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

  [DllImport("user32.dll")]
  private static extern IntPtr GetForegroundWindow();

  public MainWindow()
  {
    InitializeComponent();
    (_cfg, _cfgPath) = AppConfig.LoadOrCreate();
    Title = _cfg.UiAppTitle;
    _archiveMessagesEnabled = true;
    _archiveFolderPath = Path.Combine(AppContext.BaseDirectory, ArchiveFolderName);
    try
    {
      Directory.CreateDirectory(_archiveFolderPath);
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Unable to create archive folder at startup: {ex}");
    }

    _archivedMessages = LoadArchivedMessagesFromDisk();
    CacheArchiveStateEnvelope();

    TryLoadCHeaderIdentity(out var cHeaderName, out var cHeaderUserId);

    var viewerUserIdSeed = string.IsNullOrWhiteSpace(_cfg.ViewerUserId)
      ? (string.IsNullOrWhiteSpace(cHeaderUserId) ? Environment.UserName : cHeaderUserId)
      : _cfg.ViewerUserId;
    var viewerNameSeed = string.IsNullOrWhiteSpace(_cfg.ViewerName)
      ? (string.IsNullOrWhiteSpace(cHeaderName) ? Environment.MachineName : cHeaderName)
      : _cfg.ViewerName;

    _viewerUserId = NormalizeIdentity(viewerUserIdSeed, "local-user");
    _viewerFirstName = NormalizeIdentity(viewerNameSeed, _viewerUserId);
    _viewerLastName = "";
    _viewerEmail = NormalizeOptionalIdentity(_cfg.ViewerEmail, 254);
    _beamerVisitorUserId = ResolveBeamerVisitorUserId(_cfg.UserId);

    _http.Timeout = TimeSpan.FromMilliseconds(_cfg.RequestTimeoutMs);
    _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    Closing += (_, e) =>
    {
      if (!_allowClose) e.Cancel = true;
    };

    Closed += (_, _) =>
    {
      try { _pollCts?.Cancel(); } catch { }
      try { _http.Dispose(); } catch { }
    };

    PreviewKeyDown += (_, e) =>
    {
      if (e.Key == Key.F4 && (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
      {
        e.Handled = true;
      }
    };

    SourceInitialized += (_, _) =>
    {
      var src = (HwndSource)PresentationSource.FromVisual(this);
      src.AddHook(WndProc);
    };

    Loaded += async (_, _) => await InitAsync();
  }

  private sealed class UiPost
  {
    public string Id { get; init; } = "";
    public string? Code { get; init; }
    public List<string> TrackIdCandidates { get; init; } = new();
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public string Category { get; init; } = "Update";
    public string? DateUtc { get; init; }
    public string? PostUrl { get; init; }
    public string? LinkUrl { get; init; }
    public string? LinkText { get; init; }
    public bool IsPublished { get; init; }
  }

  private sealed class UiState
  {
    public string Status { get; init; } = "loading";
    public string Message { get; init; } = "";
    public long RefreshMs { get; init; }
    public string ConfigPath { get; init; } = "";
    public string? LastSyncUtc { get; init; }
    public List<UiPost> Posts { get; init; } = new();
    public List<string> NewPostIds { get; init; } = new();
  }

  private sealed class FetchResult
  {
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public HttpStatusCode? StatusCode { get; init; }
    public List<UiPost> Posts { get; init; } = new();
  }

  private sealed class UnreadCountResult
  {
    public bool Success { get; init; }
    public int Count { get; init; }
    public string? ErrorMessage { get; init; }
    public HttpStatusCode? StatusCode { get; init; }
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

  private sealed class MarkReadMessage
  {
    public string PostId { get; init; } = "";
    public string Code { get; init; } = "";
    public bool BeaconTracked { get; init; }
    public List<string> TrackIds { get; init; } = new();
    public string PostKey { get; init; } = "";
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public string Category { get; init; } = "Update";
    public string? DateUtc { get; init; }
    public string? PostUrl { get; init; }
    public string? LinkUrl { get; init; }
    public string? LinkText { get; init; }
  }

  private sealed class SaveUiSettingsMessage
  {
    public string AppTitle { get; init; } = "";
    public string HeaderTitle { get; init; } = "";
    public string ThemePageBackground { get; init; } = "";
    public string ThemeHeaderStart { get; init; } = "";
    public string ThemeHeaderEnd { get; init; } = "";
    public string ThemeTextMain { get; init; } = "";
    public string ThemeTextMuted { get; init; } = "";
    public string ThemeAccent { get; init; } = "";
    public string ThemeAccentSoft { get; init; } = "";
    public string ThemeUnreadStart { get; init; } = "";
    public string ThemeUnreadEnd { get; init; } = "";
    public string ThemeReadStart { get; init; } = "";
    public string ThemeReadEnd { get; init; } = "";
    public bool DisableSecretMenuAfterSave { get; init; }
  }

  private sealed class ArchivedMessage
  {
    public string ArchiveId { get; set; } = "";
    public string SourcePostId { get; set; } = "";
    public string? SourcePostCode { get; set; }
    public string SourcePostKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string Category { get; set; } = "Update";
    public string? DateUtc { get; set; }
    public string? PostUrl { get; set; }
    public string? LinkUrl { get; set; }
    public string? LinkText { get; set; }
    public string AckUtc { get; set; } = "";
  }

  private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
  {
    if (!_allowClose)
    {
      if (msg == WM_CLOSE)
      {
        handled = true;
        return IntPtr.Zero;
      }

      if (msg == WM_SYSCOMMAND)
      {
        var cmd = wParam.ToInt64() & 0xFFF0;
        if (cmd == SC_CLOSE)
        {
          handled = true;
          return IntPtr.Zero;
        }
      }
    }

    return IntPtr.Zero;
  }

  private static string ReadEmbeddedText(string fileName)
  {
    var asm = typeof(MainWindow).Assembly;
    var res = asm.GetManifestResourceNames()
      .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

    if (string.IsNullOrWhiteSpace(res))
      throw new FileNotFoundException($"Embedded resource not found: {fileName}");

    using var s = asm.GetManifestResourceStream(res);
    if (s is null)
      throw new FileNotFoundException($"Embedded resource stream not found: {fileName}");

    using var r = new StreamReader(s);
    return r.ReadToEnd();
  }

  private static bool TryWriteText(string path, string content)
  {
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.WriteAllText(path, content);
      return true;
    }
    catch
    {
      return false;
    }
  }

  private static string ResolveWritableAppFolder()
  {
    var preferred = Path.Combine(AppContext.BaseDirectory, AppFolderName);
    try
    {
      Directory.CreateDirectory(preferred);
      var probe = Path.Combine(preferred, ".write_test");
      File.WriteAllText(probe, "ok");
      File.Delete(probe);
      return preferred;
    }
    catch
    {
      var fallback = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Notifications",
        AppFolderName
      );
      Directory.CreateDirectory(fallback);
      return fallback;
    }
  }

  private async Task InitAsync()
  {
    await Web.EnsureCoreWebView2Async();

    Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
    Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
#if DEBUG
    Web.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
    Web.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif

    try { Web.CoreWebView2.Profile.PreferredTrackingPreventionLevel = CoreWebView2TrackingPreventionLevel.None; } catch { }

    Web.CoreWebView2.WebMessageReceived += (_, e) => HandleWebMessage(e.WebMessageAsJson);

    Web.CoreWebView2.NavigationStarting += (_, e) =>
    {
      if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) return;
      if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;
      if (string.Equals(uri.Host, HostName, StringComparison.OrdinalIgnoreCase)) return;

      e.Cancel = true;
      OpenExternalUrl(uri.ToString());
    };

    var appFolder = ResolveWritableAppFolder();
    var indexPath = Path.Combine(appFolder, "index.html");

    // Always refresh runtime HTML so UI updates ship with every publish.
    var html = ReadEmbeddedText("index.html");
    var wroteIndex = TryWriteText(indexPath, html);
    if (!wroteIndex && !File.Exists(indexPath))
    {
      throw new IOException($"Unable to write index.html to {indexPath}");
    }

    Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
      HostName,
      appFolder,
      CoreWebView2HostResourceAccessKind.Allow
    );

    Web.CoreWebView2.NavigationCompleted += async (_, e) =>
    {
      if (!e.IsSuccess) return;
      try
      {
        await InjectConfigAndBootAsync();
        if (!_pollStarted)
        {
          _pollStarted = true;
          StartPolling();
        }
      }
      catch (Exception ex)
      {
        Debug.WriteLine($"InjectConfigAndBootAsync failed: {ex}");
      }
    };

    Web.Source = new Uri($"https://{HostName}/index.html");
  }

  private void HandleWebMessage(string json)
  {
    try
    {
      using var doc = JsonDocument.Parse(json);
      if (!doc.RootElement.TryGetProperty("type", out var typeProp)) return;
      var type = (typeProp.GetString() ?? "").Trim();

      if (type == "ui_ready")
      {
        SendCachedStateToUi();
        SendCachedArchiveStateToUi();
        _ = PublishUiConfigAsync();
        return;
      }

      if (type == "open_external")
      {
        if (!doc.RootElement.TryGetProperty("payload", out var payload)) return;
        if (payload.ValueKind != JsonValueKind.Object) return;
        if (!payload.TryGetProperty("url", out var urlProp)) return;
        var url = (urlProp.GetString() ?? "").Trim();
        OpenExternalUrl(url);
        return;
      }

      if (type == "mark_read")
      {
        if (!doc.RootElement.TryGetProperty("payload", out var payload)) return;
        if (payload.ValueKind != JsonValueKind.Object) return;

        var markRead = new MarkReadMessage
        {
          PostId = ReadJsonMessageString(payload, "id"),
          Code = ReadJsonMessageString(payload, "code"),
          BeaconTracked = ReadBool(payload, "beaconTracked") ?? false,
          TrackIds = ReadJsonStringArray(payload, "trackIds"),
          PostKey = ReadJsonMessageString(payload, "postKey"),
          Title = ReadJsonMessageString(payload, "title"),
          Content = ReadJsonMessageString(payload, "content"),
          Category = ReadJsonMessageString(payload, "category"),
          DateUtc = ReadJsonMessageString(payload, "dateUtc"),
          PostUrl = ReadJsonMessageString(payload, "postUrl"),
          LinkUrl = ReadJsonMessageString(payload, "linkUrl"),
          LinkText = ReadJsonMessageString(payload, "linkText")
        };

        if (string.IsNullOrWhiteSpace(markRead.PostId) &&
            string.IsNullOrWhiteSpace(markRead.PostKey) &&
            string.IsNullOrWhiteSpace(markRead.Title) &&
            string.IsNullOrWhiteSpace(markRead.Content))
        {
          return;
        }

        _ = Task.Run(async () =>
        {
          try
          {
            var trackIdCandidates = new List<string>();
            if (markRead.TrackIds.Count > 0)
            {
              trackIdCandidates.AddRange(markRead.TrackIds);
            }
            if (!string.IsNullOrWhiteSpace(markRead.PostId))
            {
              trackIdCandidates.Add(markRead.PostId);
            }
            if (!string.IsNullOrWhiteSpace(markRead.PostKey) &&
                !markRead.PostKey.StartsWith("local:", StringComparison.OrdinalIgnoreCase))
            {
              trackIdCandidates.Add(markRead.PostKey);
            }
            if (!string.IsNullOrWhiteSpace(markRead.Code))
            {
              trackIdCandidates.Add(markRead.Code);
            }

            if (!trackIdCandidates.Any(LooksNumeric))
            {
              var resolvedDescriptionId = await ResolveDescriptionIdAsync(markRead, CancellationToken.None);
              if (!string.IsNullOrWhiteSpace(resolvedDescriptionId))
              {
                trackIdCandidates.Insert(0, resolvedDescriptionId);
              }
            }

            if (trackIdCandidates.Count > 0)
            {
              await TrackPostViewsAsync(trackIdCandidates, CancellationToken.None);
            }

            if (_archiveMessagesEnabled && TryArchiveAcknowledgedMessage(markRead))
            {
              await PublishArchiveStateAsync();
            }
          }
          catch
          {
          }
        });
        return;
      }

      if (type == "save_ui_settings")
      {
        if (!doc.RootElement.TryGetProperty("payload", out var payload)) return;
        if (payload.ValueKind != JsonValueKind.Object) return;

        var settings = new SaveUiSettingsMessage
        {
          AppTitle = ReadJsonMessageString(payload, "appTitle"),
          HeaderTitle = ReadJsonMessageString(payload, "headerTitle"),
          ThemePageBackground = ReadJsonMessageString(payload, "themePageBackground"),
          ThemeHeaderStart = ReadJsonMessageString(payload, "themeHeaderStart"),
          ThemeHeaderEnd = ReadJsonMessageString(payload, "themeHeaderEnd"),
          ThemeTextMain = ReadJsonMessageString(payload, "themeTextMain"),
          ThemeTextMuted = ReadJsonMessageString(payload, "themeTextMuted"),
          ThemeAccent = ReadJsonMessageString(payload, "themeAccent"),
          ThemeAccentSoft = ReadJsonMessageString(payload, "themeAccentSoft"),
          ThemeUnreadStart = ReadJsonMessageString(payload, "themeUnreadStart"),
          ThemeUnreadEnd = ReadJsonMessageString(payload, "themeUnreadEnd"),
          ThemeReadStart = ReadJsonMessageString(payload, "themeReadStart"),
          ThemeReadEnd = ReadJsonMessageString(payload, "themeReadEnd"),
          DisableSecretMenuAfterSave = ReadBool(payload, "disableSecretMenuAfterSave") ?? false
        };

        _ = Task.Run(() => SaveUiSettingsAsync(settings));
      }
    }
    catch
    {
    }
  }

  private static void OpenExternalUrl(string url)
  {
    try
    {
      if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
      if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;

      Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
    }
    catch
    {
    }
  }

  private void StartPolling()
  {
    _pollCts?.Cancel();
    _pollCts = new CancellationTokenSource();
    _ = Task.Run(() => PollLoopAsync(_pollCts.Token));
  }

  private async Task PollLoopAsync(CancellationToken token)
  {
    var refreshMs = Math.Max(1_000, _cfg.RefreshMs);

    await PublishStateAsync(new UiState
    {
      Status = "loading",
      Message = "Starting API sync…",
      RefreshMs = refreshMs,
      ConfigPath = _cfgPath,
      LastSyncUtc = null,
      Posts = _latestPosts,
      NewPostIds = new List<string>()
    });

    while (!token.IsCancellationRequested)
    {
      try
      {
        await PollOnceAsync(refreshMs, token);
      }
      catch (OperationCanceledException)
      {
        break;
      }
      catch (Exception ex)
      {
        await PublishStateAsync(new UiState
        {
        Status = "error",
        Message = $"Unexpected error: {ex.Message}",
        RefreshMs = refreshMs,
        ConfigPath = _cfgPath,
        LastSyncUtc = DateTimeOffset.UtcNow.ToString("O"),
        Posts = _latestPosts,
        NewPostIds = new List<string>()
      });
      }

      try
      {
        await Task.Delay((int)refreshMs, token);
      }
      catch (OperationCanceledException)
      {
        break;
      }
    }
  }

  private async Task PollOnceAsync(long refreshMs, CancellationToken token)
  {
    if (string.IsNullOrWhiteSpace(_cfg.ApiKey))
    {
      await PublishStateAsync(new UiState
      {
        Status = "config_error",
        Message = "Missing ApiKey. Add your private Beamer API key in the config file and restart.",
        RefreshMs = refreshMs,
        ConfigPath = _cfgPath,
        LastSyncUtc = null,
        Posts = _latestPosts,
        NewPostIds = new List<string>()
      });
      return;
    }

    var nowUtc = DateTimeOffset.UtcNow;
    var unread = await FetchUnreadCountAsync(token);

    var shouldFetchPosts = _latestPosts.Count == 0;
    if (unread.Success)
    {
      if (!_lastUnreadCount.HasValue || unread.Count != _lastUnreadCount.Value)
      {
        shouldFetchPosts = true;
      }
      _lastUnreadCount = unread.Count;
    }
    else if ((nowUtc - _lastPostsFetchAt) >= PostFetchFallbackInterval)
    {
      shouldFetchPosts = true;
    }

    if (!shouldFetchPosts)
    {
      var stableStatusMessage = _latestPosts.Count == 0
        ? "Connected. No posts available."
        : "Connected. Waiting for new posts...";

      await PublishStateAsync(new UiState
      {
        Status = "ok",
        Message = stableStatusMessage,
        RefreshMs = refreshMs,
        ConfigPath = _cfgPath,
        LastSyncUtc = nowUtc.ToString("O"),
        Posts = _latestPosts,
        NewPostIds = new List<string>()
      });
      return;
    }

    var fetch = await FetchPostsAsync(token);
    if (!fetch.Success)
    {
      await PublishStateAsync(new UiState
      {
        Status = "error",
        Message = fetch.ErrorMessage ?? "Failed to fetch Beamer posts.",
        RefreshMs = refreshMs,
        ConfigPath = _cfgPath,
        LastSyncUtc = DateTimeOffset.UtcNow.ToString("O"),
        Posts = _latestPosts,
        NewPostIds = new List<string>()
      });
      return;
    }

    _lastPostsFetchAt = nowUtc;

    var posts = fetch.Posts
      .OrderByDescending(GetSortDate)
      .ThenByDescending(p => p.Id)
      .Take(_cfg.MaxPosts)
      .ToList();

    var archiveUpdated = await ArchivePostsAsync(posts);

    _latestPosts = posts;

    var fingerprint = BuildFingerprint(posts);
    var hadSnapshot = !string.IsNullOrWhiteSpace(_lastFingerprint);
    var hasNew = hadSnapshot && !string.Equals(fingerprint, _lastFingerprint, StringComparison.Ordinal);
    _lastFingerprint = fingerprint;

    var currentIds = posts
      .Select(p => p.Id)
      .Where(id => !string.IsNullOrWhiteSpace(id))
      .ToHashSet(StringComparer.Ordinal);

    var newPostIds = new List<string>();
    if (_knownPostIds.Count > 0)
    {
      newPostIds = currentIds
        .Where(id => !_knownPostIds.Contains(id))
        .ToList();
    }

    _knownPostIds = currentIds;

    if (hasNew && newPostIds.Count == 0 && posts.Count > 0 && !string.IsNullOrWhiteSpace(posts[0].Id))
    {
      newPostIds.Add(posts[0].Id);
    }

    if (hasNew)
    {
      await Dispatcher.InvokeAsync(() =>
      {
        // Always foreground the app when a new message arrives.
        BringToFront(force: true);
      });
    }

    var statusMessage = posts.Count == 0
      ? "Connected. No posts available."
      : hasNew
        ? $"Received {Math.Max(1, newPostIds.Count)} new post(s)."
        : $"Connected. {posts.Count} post(s) loaded.";

    await PublishStateAsync(new UiState
    {
      Status = "ok",
      Message = statusMessage,
      RefreshMs = refreshMs,
      ConfigPath = _cfgPath,
      LastSyncUtc = DateTimeOffset.UtcNow.ToString("O"),
      Posts = posts,
      NewPostIds = hasNew ? newPostIds : new List<string>()
    });

    if (archiveUpdated)
    {
      await PublishArchiveStateAsync();
    }
  }

  private async Task<FetchResult> FetchPostsAsync(CancellationToken token)
  {
    HttpStatusCode? lastStatus = null;
    string? lastAuthError = null;

    foreach (var mode in GetAuthModesToTry())
    {
      using var req = BuildPostsRequest(mode);

      HttpResponseMessage resp;
      string raw;

      try
      {
        resp = await _http.SendAsync(req, token);
        raw = await resp.Content.ReadAsStringAsync(token);
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (Exception ex)
      {
        return new FetchResult
        {
          Success = false,
          ErrorMessage = $"Network error: {ex.Message}"
        };
      }

      if (resp.IsSuccessStatusCode)
      {
        List<UiPost> posts;
        try
        {
          posts = ParsePosts(raw);
        }
        catch (Exception ex)
        {
          return new FetchResult
          {
            Success = false,
            ErrorMessage = $"Invalid API response format: {ex.Message}",
            StatusCode = resp.StatusCode
          };
        }

        _authMode = mode;
        return new FetchResult
        {
          Success = true,
          Posts = posts,
          StatusCode = resp.StatusCode
        };
      }

      lastStatus = resp.StatusCode;
      var detail = ExtractMessage(raw);

      if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
      {
        lastAuthError = string.IsNullOrWhiteSpace(detail) ? "Authentication failed." : detail;
        continue;
      }

      return new FetchResult
      {
        Success = false,
        StatusCode = resp.StatusCode,
        ErrorMessage = $"Beamer API error {(int)resp.StatusCode}: {detail}"
      };
    }

    return new FetchResult
    {
      Success = false,
      StatusCode = lastStatus,
      ErrorMessage = string.IsNullOrWhiteSpace(lastAuthError)
        ? "Authentication failed. Verify ApiKey in config."
        : $"Authentication failed: {lastAuthError}"
    };
  }

  private async Task<UnreadCountResult> FetchUnreadCountAsync(CancellationToken token)
  {
    HttpStatusCode? lastStatus = null;
    string? lastAuthError = null;

    foreach (var mode in GetAuthModesToTry())
    {
      using var req = BuildUnreadCountRequest(mode);

      HttpResponseMessage resp;
      string raw;

      try
      {
        resp = await _http.SendAsync(req, token);
        raw = await resp.Content.ReadAsStringAsync(token);
      }
      catch (OperationCanceledException)
      {
        throw;
      }
      catch (Exception ex)
      {
        return new UnreadCountResult
        {
          Success = false,
          ErrorMessage = $"Network error: {ex.Message}"
        };
      }

      if (resp.IsSuccessStatusCode)
      {
        int count;
        try
        {
          count = ParseUnreadCount(raw);
        }
        catch (Exception ex)
        {
          return new UnreadCountResult
          {
            Success = false,
            ErrorMessage = $"Invalid unread count response format: {ex.Message}",
            StatusCode = resp.StatusCode
          };
        }

        _authMode = mode;
        return new UnreadCountResult
        {
          Success = true,
          Count = count,
          StatusCode = resp.StatusCode
        };
      }

      lastStatus = resp.StatusCode;
      var detail = ExtractMessage(raw);

      if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
      {
        lastAuthError = string.IsNullOrWhiteSpace(detail) ? "Authentication failed." : detail;
        continue;
      }

      return new UnreadCountResult
      {
        Success = false,
        StatusCode = resp.StatusCode,
        ErrorMessage = $"Beamer API error {(int)resp.StatusCode}: {detail}"
      };
    }

    return new UnreadCountResult
    {
      Success = false,
      StatusCode = lastStatus,
      ErrorMessage = string.IsNullOrWhiteSpace(lastAuthError)
        ? "Authentication failed. Verify ApiKey in config."
        : $"Authentication failed: {lastAuthError}"
    };
  }

  private IEnumerable<AuthMode> GetAuthModesToTry()
  {
    var all = new[]
    {
      AuthMode.BearerHeader,
      AuthMode.XBeamerApiKeyHeader,
      AuthMode.XApiKeyHeader,
      AuthMode.QueryParameter
    };

    if (_authMode == AuthMode.Unknown) return all;
    return new[] { _authMode }.Concat(all.Where(m => m != _authMode));
  }

  private HttpRequestMessage BuildPostsRequest(AuthMode mode)
  {
    var includeApiKeyInQuery = mode == AuthMode.QueryParameter;
    var uri = BuildPostsUri(includeApiKeyInQuery);

    var req = new HttpRequestMessage(HttpMethod.Get, uri);
    req.Headers.Accept.Clear();
    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    var key = _cfg.ApiKey;

    switch (mode)
    {
      case AuthMode.BearerHeader:
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        break;

      case AuthMode.XBeamerApiKeyHeader:
        req.Headers.TryAddWithoutValidation("X-Beamer-API-Key", key);
        req.Headers.TryAddWithoutValidation("Beamer-API-Key", key);
        break;

      case AuthMode.XApiKeyHeader:
        req.Headers.TryAddWithoutValidation("X-API-Key", key);
        req.Headers.TryAddWithoutValidation("API-Key", key);
        break;

      case AuthMode.QueryParameter:
      case AuthMode.Unknown:
      default:
        break;
    }

    return req;
  }

  private HttpRequestMessage BuildUnreadCountRequest(AuthMode mode)
  {
    var includeApiKeyInQuery = mode == AuthMode.QueryParameter;
    var uri = BuildUnreadCountUri(includeApiKeyInQuery);

    var req = new HttpRequestMessage(HttpMethod.Get, uri);
    req.Headers.Accept.Clear();
    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

    var key = _cfg.ApiKey;

    switch (mode)
    {
      case AuthMode.BearerHeader:
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        break;

      case AuthMode.XBeamerApiKeyHeader:
        req.Headers.TryAddWithoutValidation("X-Beamer-API-Key", key);
        req.Headers.TryAddWithoutValidation("Beamer-API-Key", key);
        break;

      case AuthMode.XApiKeyHeader:
        req.Headers.TryAddWithoutValidation("X-API-Key", key);
        req.Headers.TryAddWithoutValidation("API-Key", key);
        break;

      case AuthMode.QueryParameter:
      case AuthMode.Unknown:
      default:
        break;
    }

    return req;
  }

  private Uri BuildPostsUri(bool includeApiKey)
  {
    var baseUrl = _cfg.ApiBaseUrl.TrimEnd('/');
    var path = $"{baseUrl}/posts";

    var query = new List<string>();
    if (!string.IsNullOrWhiteSpace(_cfg.ProductId))
    {
      query.Add($"product_id={Uri.EscapeDataString(_cfg.ProductId)}");
      query.Add($"productId={Uri.EscapeDataString(_cfg.ProductId)}");
    }

    AppendViewerIdentityQuery(query);

    if (_cfg.MaxPosts > 0)
    {
      query.Add($"limit={_cfg.MaxPosts}");
      query.Add($"per_page={_cfg.MaxPosts}");
    }

    if (includeApiKey && !string.IsNullOrWhiteSpace(_cfg.ApiKey))
      query.Add($"api_key={Uri.EscapeDataString(_cfg.ApiKey)}");

    if (query.Count == 0) return new Uri(path);
    return new Uri($"{path}?{string.Join("&", query)}");
  }

  private Uri BuildUnreadCountUri(bool includeApiKey)
  {
    var baseUrl = _cfg.ApiBaseUrl.TrimEnd('/');
    var path = $"{baseUrl}/unread/count";

    var query = new List<string>();
    if (!string.IsNullOrWhiteSpace(_cfg.ProductId))
    {
      query.Add($"product_id={Uri.EscapeDataString(_cfg.ProductId)}");
      query.Add($"productId={Uri.EscapeDataString(_cfg.ProductId)}");
    }

    AppendViewerIdentityQuery(query);

    if (includeApiKey && !string.IsNullOrWhiteSpace(_cfg.ApiKey))
      query.Add($"api_key={Uri.EscapeDataString(_cfg.ApiKey)}");

    if (query.Count == 0) return new Uri(path);
    return new Uri($"{path}?{string.Join("&", query)}");
  }

  private async Task<bool> TrackPostViewsAsync(IEnumerable<string> postIds, CancellationToken token)
  {
    if (!_cfg.EnableViewTracking) return false;
    if (string.IsNullOrWhiteSpace(_cfg.ProductId)) return false;

    var trackedAny = false;

    foreach (var postId in postIds
      .Where(id => !string.IsNullOrWhiteSpace(id))
      .Select(id => id.Trim())
      .Distinct(StringComparer.Ordinal))
    {
      if (!_trackedViewPostIds.TryAdd(postId, 0))
      {
        trackedAny = true;
        continue;
      }

      var tracked = await TrackSinglePostViewAsync(postId, token);
      if (tracked)
      {
        trackedAny = true;
      }
      if (!tracked)
      {
        _trackedViewPostIds.TryRemove(postId, out _);
      }
    }

    return trackedAny;
  }

  private async Task<bool> TrackSinglePostViewAsync(string postId, CancellationToken token)
  {
    try
    {
      if (LooksNumeric(postId))
      {
        var trackViewsUri = BuildTrackViewsUri(postId);
        using var trackViewsReq = new HttpRequestMessage(HttpMethod.Post, trackViewsUri)
        {
          Content = new ByteArrayContent(Array.Empty<byte>())
        };
        trackViewsReq.Headers.Accept.Clear();
        trackViewsReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        using var trackViewsResp = await _http.SendAsync(trackViewsReq, token);
        if (trackViewsResp.IsSuccessStatusCode) return true;

        var trackViewsBody = await trackViewsResp.Content.ReadAsStringAsync(token);
        Debug.WriteLine($"Track view app trackViews POST failed for post {postId}: {(int)trackViewsResp.StatusCode} {ExtractMessage(trackViewsBody)}");
      }

      var trackParams = BuildTrackParameters(postId);
      var queryTrackUri = BuildTrackUri(trackParams);

      // Keep this endpoint aligned with the 2/20 behavior that was known-good.
      using var queryPostReq = new HttpRequestMessage(HttpMethod.Post, queryTrackUri);
      queryPostReq.Headers.Accept.Clear();
      queryPostReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
      using var queryPostResp = await _http.SendAsync(queryPostReq, token);
      if (queryPostResp.IsSuccessStatusCode) return true;

      var queryPostBody = await queryPostResp.Content.ReadAsStringAsync(token);
      Debug.WriteLine($"Track view query POST failed for post {postId}: {(int)queryPostResp.StatusCode} {ExtractMessage(queryPostBody)}");
      return false;
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Track view exception for post {postId}: {ex.Message}");
      return false;
    }
  }

  private List<KeyValuePair<string, string>> BuildTrackParameters(string postId)
  {
    var query = new List<KeyValuePair<string, string>>
    {
      new("product", _cfg.ProductId),
      new("id", postId)
    };

    AppendViewerIdentityParameters(query);

    return query;
  }

  private Uri BuildTrackViewsUri(string descriptionId)
  {
    var query = new List<string>
    {
      $"app_id={Uri.EscapeDataString(_cfg.ProductId)}",
      $"descriptionId={Uri.EscapeDataString(descriptionId)}"
    };

    if (!string.IsNullOrWhiteSpace(_viewerUserId))
    {
      query.Add($"user_id={Uri.EscapeDataString(_viewerUserId)}");
      query.Add($"custom_user_id={Uri.EscapeDataString(_viewerUserId)}");
    }

    if (!string.IsNullOrWhiteSpace(_viewerFirstName))
    {
      query.Add($"firstname={Uri.EscapeDataString(_viewerFirstName)}");
    }

    if (!string.IsNullOrWhiteSpace(_viewerLastName))
    {
      query.Add($"lastname={Uri.EscapeDataString(_viewerLastName)}");
    }

    if (!string.IsNullOrWhiteSpace(_viewerEmail))
    {
      query.Add($"email={Uri.EscapeDataString(_viewerEmail)}");
    }

    return new Uri($"{BeamerTrackViewsBaseUrl}?{string.Join("&", query)}");
  }

  private async Task<string?> ResolveDescriptionIdAsync(MarkReadMessage markRead, CancellationToken token)
  {
    if (string.IsNullOrWhiteSpace(_cfg.ProductId)) return null;

    var slug = ExtractSlug(markRead.PostUrl);
    var titleKey = NormalizeLookupKey(markRead.Title);

    await EnsurePublicDescriptionCacheAsync(token);

    if (!string.IsNullOrWhiteSpace(slug) &&
        _publicDescriptionBySlug.TryGetValue(slug, out var bySlug) &&
        LooksNumeric(bySlug))
    {
      return bySlug;
    }

    if (!string.IsNullOrWhiteSpace(titleKey) &&
        _publicDescriptionByTitle.TryGetValue(titleKey, out var byTitle) &&
        LooksNumeric(byTitle))
    {
      return byTitle;
    }

    return null;
  }

  private async Task EnsurePublicDescriptionCacheAsync(CancellationToken token)
  {
    if (string.IsNullOrWhiteSpace(_cfg.ProductId)) return;
    if ((DateTimeOffset.UtcNow - _publicDescriptionCacheAt) < PublicDescriptionCacheTtl) return;

    await _publicDescriptionCacheGate.WaitAsync(token);
    try
    {
      if ((DateTimeOffset.UtcNow - _publicDescriptionCacheAt) < PublicDescriptionCacheTtl) return;

      var uri = $"https://app.getbeamer.com/loadMoreNews?app_id={Uri.EscapeDataString(_cfg.ProductId)}";
      using var req = new HttpRequestMessage(HttpMethod.Get, uri);
      using var resp = await _http.SendAsync(req, token);
      if (!resp.IsSuccessStatusCode)
      {
        _publicDescriptionCacheAt = DateTimeOffset.UtcNow;
        return;
      }

      var html = await resp.Content.ReadAsStringAsync(token);
      var byTitle = new Dictionary<string, string>(StringComparer.Ordinal);
      var bySlug = new Dictionary<string, string>(StringComparer.Ordinal);

      var matches = PublicDescriptionRegex.Matches(html);
      for (var i = 0; i < matches.Count; i++)
      {
        var match = matches[i];
        var id = (match.Groups["id"].Value ?? "").Trim();
        if (!LooksNumeric(id)) continue;

        var titleRaw = WebUtility.HtmlDecode(match.Groups["title"].Value ?? "");
        var titleKey = NormalizeLookupKey(titleRaw);
        if (!string.IsNullOrWhiteSpace(titleKey))
        {
          byTitle[titleKey] = id;
        }

        var nextIndex = i + 1 < matches.Count ? matches[i + 1].Index : html.Length;
        var segmentLength = Math.Max(0, nextIndex - match.Index);
        if (segmentLength > 0)
        {
          var segment = html.Substring(match.Index, segmentLength);
          var hrefMatch = HrefRegex.Match(segment);
          if (hrefMatch.Success)
          {
            var slug = ExtractSlug(hrefMatch.Groups["href"].Value);
            if (!string.IsNullOrWhiteSpace(slug))
            {
              bySlug[slug] = id;
            }
          }
        }
      }

      _publicDescriptionByTitle = byTitle;
      _publicDescriptionBySlug = bySlug;
      _publicDescriptionCacheAt = DateTimeOffset.UtcNow;
    }
    catch (Exception ex)
    {
      Debug.WriteLine($"Failed to refresh public description cache: {ex.Message}");
      _publicDescriptionCacheAt = DateTimeOffset.UtcNow;
    }
    finally
    {
      _publicDescriptionCacheGate.Release();
    }
  }

  private static string NormalizeLookupKey(string? value)
  {
    var v = (value ?? "").Trim();
    if (string.IsNullOrWhiteSpace(v)) return "";
    v = Regex.Replace(v, "\\s+", " ");
    return v.ToLowerInvariant();
  }

  private static string ExtractSlug(string? url)
  {
    var raw = (url ?? "").Trim();
    if (string.IsNullOrWhiteSpace(raw)) return "";

    if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
    {
      return "";
    }

    var segment = uri.Segments.LastOrDefault() ?? "";
    return segment.Trim('/').ToLowerInvariant();
  }

  private static bool LooksNumeric(string value)
  {
    if (string.IsNullOrWhiteSpace(value)) return false;
    foreach (var ch in value.Trim())
    {
      if (!char.IsDigit(ch)) return false;
    }
    return true;
  }

  private static Uri BuildTrackUri(IReadOnlyList<KeyValuePair<string, string>> queryParams)
  {
    var query = string.Join("&", queryParams.Select(p =>
      $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value ?? "")}"));

    if (string.IsNullOrWhiteSpace(query))
    {
      return new Uri(BeamerTrackBaseUrl);
    }

    return new Uri($"{BeamerTrackBaseUrl}?{query}");
  }

  private void AppendViewerIdentityParameters(List<KeyValuePair<string, string>> queryParams)
  {
    if (!string.IsNullOrWhiteSpace(_viewerUserId))
    {
      queryParams.Add(new KeyValuePair<string, string>("user_id", _viewerUserId));
      queryParams.Add(new KeyValuePair<string, string>("custom_user_id", _viewerUserId));
      queryParams.Add(new KeyValuePair<string, string>("userId", _viewerUserId));
      queryParams.Add(new KeyValuePair<string, string>("customUserId", _viewerUserId));
    }

    if (!string.IsNullOrWhiteSpace(_viewerFirstName))
    {
      queryParams.Add(new KeyValuePair<string, string>("user_firstname", _viewerFirstName));
      queryParams.Add(new KeyValuePair<string, string>("firstname", _viewerFirstName));
      queryParams.Add(new KeyValuePair<string, string>("userFirstName", _viewerFirstName));
      queryParams.Add(new KeyValuePair<string, string>("firstName", _viewerFirstName));
    }

    if (!string.IsNullOrWhiteSpace(_viewerLastName))
    {
      queryParams.Add(new KeyValuePair<string, string>("user_lastname", _viewerLastName));
      queryParams.Add(new KeyValuePair<string, string>("lastname", _viewerLastName));
      queryParams.Add(new KeyValuePair<string, string>("userLastName", _viewerLastName));
      queryParams.Add(new KeyValuePair<string, string>("lastName", _viewerLastName));
    }

    if (!string.IsNullOrWhiteSpace(_viewerEmail))
    {
      queryParams.Add(new KeyValuePair<string, string>("user_email", _viewerEmail));
      queryParams.Add(new KeyValuePair<string, string>("email", _viewerEmail));
      queryParams.Add(new KeyValuePair<string, string>("userEmail", _viewerEmail));
    }
  }

  private void AppendViewerIdentityQuery(List<string> query)
  {
    var identityPairs = new List<KeyValuePair<string, string>>();
    AppendViewerIdentityParameters(identityPairs);
    foreach (var pair in identityPairs)
    {
      query.Add($"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value ?? "")}");
    }
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
    offset = 1; // first byte in record is deletion flag
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
    if (string.IsNullOrWhiteSpace(v)) return fallback;
    return v.Length <= 120 ? v : v[..120];
  }

  private static string NormalizeOptionalIdentity(string? value, int maxLength)
  {
    var v = (value ?? "").Trim();
    if (string.IsNullOrWhiteSpace(v)) return "";
    return v.Length <= maxLength ? v : v[..maxLength];
  }

  private static string ResolveBeamerVisitorUserId(string? configuredUserId)
  {
    var raw = (configuredUserId ?? "").Trim();
    if (Guid.TryParse(raw, out var directGuid)) return directGuid.ToString("D");

    if (raw.StartsWith("local-", StringComparison.OrdinalIgnoreCase))
    {
      var suffix = raw["local-".Length..].Trim();
      if (Guid.TryParse(suffix, out var localGuid)) return localGuid.ToString("D");
    }

    return Guid.NewGuid().ToString("D");
  }

  private static string BuildFingerprint(IReadOnlyList<UiPost> posts)
  {
    if (posts.Count == 0) return "empty";

    var top = posts[0];
    return string.Join("|", new[]
    {
      top.Id ?? "",
      top.DateUtc ?? "",
      top.Title ?? "",
      top.Content ?? ""
    });
  }

  private static string NormalizeUiTitle(string? value, string fallback)
  {
    var v = (value ?? "").Trim();
    if (string.IsNullOrWhiteSpace(v)) return fallback;
    return v.Length <= 80 ? v : v[..80];
  }

  private static string NormalizeUiColor(string? value, string fallback)
  {
    var v = (value ?? "").Trim();
    if (!HexColorRegex.IsMatch(v)) return fallback;
    return v.ToLowerInvariant();
  }

  private async Task PublishStateAsync(UiState state)
  {
    var envelopeJson = JsonSerializer.Serialize(new
    {
      type = "state",
      payload = state
    }, _jsonOptions);

    lock (_stateLock)
    {
      _lastStateEnvelopeJson = envelopeJson;
    }

    await Dispatcher.InvokeAsync(() =>
    {
      try
      {
        if (Web.CoreWebView2 is null) return;
        Web.CoreWebView2.PostWebMessageAsJson(envelopeJson);
      }
      catch
      {
      }
    });
  }

  private void SendCachedStateToUi()
  {
    string? envelope;
    lock (_stateLock)
    {
      envelope = _lastStateEnvelopeJson;
    }

    if (string.IsNullOrWhiteSpace(envelope)) return;

    try
    {
      Web.CoreWebView2?.PostWebMessageAsJson(envelope);
    }
    catch
    {
    }
  }

  private async Task PublishArchiveStateAsync()
  {
    if (!_archiveMessagesEnabled) return;

    string envelopeJson;
    lock (_archiveLock)
    {
      var snapshot = _archivedMessages
        .OrderByDescending(m => m.AckUtc)
        .ToList();
      envelopeJson = BuildArchiveStateEnvelopeJson(snapshot);
      _lastArchiveEnvelopeJson = envelopeJson;
    }

    await Dispatcher.InvokeAsync(() =>
    {
      try
      {
        if (Web.CoreWebView2 is null) return;
        Web.CoreWebView2.PostWebMessageAsJson(envelopeJson);
      }
      catch
      {
      }
    });
  }

  private void SendCachedArchiveStateToUi()
  {
    if (!_archiveMessagesEnabled) return;

    string envelope;
    lock (_archiveLock)
    {
      if (string.IsNullOrWhiteSpace(_lastArchiveEnvelopeJson))
      {
        var snapshot = _archivedMessages
          .OrderByDescending(m => m.AckUtc)
          .ToList();
        _lastArchiveEnvelopeJson = BuildArchiveStateEnvelopeJson(snapshot);
      }

      envelope = _lastArchiveEnvelopeJson!;
    }

    try
    {
      Web.CoreWebView2?.PostWebMessageAsJson(envelope);
    }
    catch
    {
    }
  }

  private void CacheArchiveStateEnvelope()
  {
    if (!_archiveMessagesEnabled) return;

    lock (_archiveLock)
    {
      var snapshot = _archivedMessages
        .OrderByDescending(m => m.AckUtc)
        .ToList();
      _lastArchiveEnvelopeJson = BuildArchiveStateEnvelopeJson(snapshot);
    }
  }

  private string BuildArchiveStateEnvelopeJson(IReadOnlyList<ArchivedMessage> messages)
  {
    return JsonSerializer.Serialize(new
    {
      type = "archive_state",
      payload = new
      {
        messages
      }
    }, _jsonOptions);
  }

  private object BuildUiConfigPayload()
  {
    return new
    {
      enable_secret_menu = _cfg.EnableSecretMenu,
      ui_app_title = _cfg.UiAppTitle,
      ui_header_title = _cfg.UiHeaderTitle,
      theme_page_background = _cfg.ThemePageBackground,
      theme_header_start = _cfg.ThemeHeaderStart,
      theme_header_end = _cfg.ThemeHeaderEnd,
      theme_text_main = _cfg.ThemeTextMain,
      theme_text_muted = _cfg.ThemeTextMuted,
      theme_accent = _cfg.ThemeAccent,
      theme_accent_soft = _cfg.ThemeAccentSoft,
      theme_unread_start = _cfg.ThemeUnreadStart,
      theme_unread_end = _cfg.ThemeUnreadEnd,
      theme_read_start = _cfg.ThemeReadStart,
      theme_read_end = _cfg.ThemeReadEnd
    };
  }

  private async Task PublishUiConfigAsync()
  {
    await SendToUiAsync("ui_config", BuildUiConfigPayload());
  }

  private async Task SaveUiSettingsAsync(SaveUiSettingsMessage settings)
  {
    var fallbackAppTitle = NormalizeUiTitle(_cfg.UiAppTitle, "Notifications");
    var nextAppTitle = NormalizeUiTitle(settings.AppTitle, fallbackAppTitle);
    _cfg.UiAppTitle = nextAppTitle;
    _cfg.UiHeaderTitle = NormalizeUiTitle(settings.HeaderTitle, nextAppTitle);
    _cfg.ThemePageBackground = NormalizeUiColor(settings.ThemePageBackground, _cfg.ThemePageBackground);
    _cfg.ThemeHeaderStart = NormalizeUiColor(settings.ThemeHeaderStart, _cfg.ThemeHeaderStart);
    _cfg.ThemeHeaderEnd = NormalizeUiColor(settings.ThemeHeaderEnd, _cfg.ThemeHeaderEnd);
    _cfg.ThemeTextMain = NormalizeUiColor(settings.ThemeTextMain, _cfg.ThemeTextMain);
    _cfg.ThemeTextMuted = NormalizeUiColor(settings.ThemeTextMuted, _cfg.ThemeTextMuted);
    _cfg.ThemeAccent = NormalizeUiColor(settings.ThemeAccent, _cfg.ThemeAccent);
    _cfg.ThemeAccentSoft = NormalizeUiColor(settings.ThemeAccentSoft, _cfg.ThemeAccentSoft);
    _cfg.ThemeUnreadStart = NormalizeUiColor(settings.ThemeUnreadStart, _cfg.ThemeUnreadStart);
    _cfg.ThemeUnreadEnd = NormalizeUiColor(settings.ThemeUnreadEnd, _cfg.ThemeUnreadEnd);
    _cfg.ThemeReadStart = NormalizeUiColor(settings.ThemeReadStart, _cfg.ThemeReadStart);
    _cfg.ThemeReadEnd = NormalizeUiColor(settings.ThemeReadEnd, _cfg.ThemeReadEnd);
    var previousEnableSecretMenu = _cfg.EnableSecretMenu;
    if (settings.DisableSecretMenuAfterSave)
    {
      _cfg.EnableSecretMenu = false;
    }

    var saved = AppConfig.Save(_cfgPath, _cfg);
    if (!saved && settings.DisableSecretMenuAfterSave)
    {
      _cfg.EnableSecretMenu = previousEnableSecretMenu;
    }

    await Dispatcher.InvokeAsync(() =>
    {
      Title = _cfg.UiAppTitle;
    });

    await PublishUiConfigAsync();
    await SendToUiAsync("ui_settings_saved", new
    {
      success = saved,
      message = saved
        ? (settings.DisableSecretMenuAfterSave ? "UI settings saved. Secret menu disabled." : "UI settings saved.")
        : "Unable to save UI settings to config."
    });
  }

  private async Task SendToUiAsync(string type, object payload)
  {
    var envelopeJson = JsonSerializer.Serialize(new
    {
      type,
      payload
    }, _jsonOptions);

    await Dispatcher.InvokeAsync(() =>
    {
      try
      {
        if (Web.CoreWebView2 is null) return;
        Web.CoreWebView2.PostWebMessageAsJson(envelopeJson);
      }
      catch
      {
      }
    });
  }

  private List<ArchivedMessage> LoadArchivedMessagesFromDisk()
  {
    var archived = new List<ArchivedMessage>();

    try
    {
      if (!Directory.Exists(_archiveFolderPath)) return archived;

      foreach (var file in Directory.EnumerateFiles(_archiveFolderPath, "*.json", SearchOption.TopDirectoryOnly))
      {
        try
        {
          var raw = File.ReadAllText(file);
          var item = JsonSerializer.Deserialize<ArchivedMessage>(raw, new JsonSerializerOptions
          {
            PropertyNameCaseInsensitive = true
          });

          if (item is null) continue;
          if (string.IsNullOrWhiteSpace(item.SourcePostKey))
          {
            item.SourcePostKey = !string.IsNullOrWhiteSpace(item.SourcePostId)
              ? item.SourcePostId
              : item.ArchiveId;
          }
          if (string.IsNullOrWhiteSpace(item.AckUtc))
          {
            item.AckUtc = DateTimeOffset.UtcNow.ToString("O");
          }

          archived.Add(item);
        }
        catch
        {
        }
      }
    }
    catch
    {
    }

    return archived
      .OrderByDescending(m => m.AckUtc)
      .ToList();
  }

  private Task<bool> ArchivePostsAsync(IReadOnlyList<UiPost> posts)
  {
    if (!_archiveMessagesEnabled || posts.Count == 0) return Task.FromResult(false);

    var updated = false;
    foreach (var post in posts)
    {
      if (TryArchivePost(post))
      {
        updated = true;
      }
    }

    return Task.FromResult(updated);
  }

  private bool TryArchivePost(UiPost post)
  {
    var markRead = new MarkReadMessage
    {
      PostId = post.Id ?? "",
      Code = post.Code ?? "",
      PostKey = BuildArchiveSourceKey(post),
      Title = post.Title ?? "",
      Content = post.Content ?? "",
      Category = post.Category ?? "Update",
      DateUtc = post.DateUtc,
      PostUrl = post.PostUrl,
      LinkUrl = post.LinkUrl,
      LinkText = post.LinkText
    };

    return TryArchiveAcknowledgedMessage(markRead);
  }

  private bool TryArchiveAcknowledgedMessage(MarkReadMessage message)
  {
    if (!_archiveMessagesEnabled) return false;

    var sourceKey = BuildArchiveSourceKey(message);
    if (string.IsNullOrWhiteSpace(sourceKey)) return false;

    lock (_archiveLock)
    {
      if (_archivedMessages.Any(m => string.Equals(m.SourcePostKey, sourceKey, StringComparison.Ordinal)))
      {
        return false;
      }
    }

    try
    {
      Directory.CreateDirectory(_archiveFolderPath);
    }
    catch
    {
      return false;
    }

    var nowUtc = DateTimeOffset.UtcNow;
    var archive = new ArchivedMessage
    {
      ArchiveId = Guid.NewGuid().ToString("N"),
      SourcePostId = (message.PostId ?? "").Trim(),
      SourcePostCode = string.IsNullOrWhiteSpace(message.Code) ? null : message.Code.Trim(),
      SourcePostKey = sourceKey,
      Title = (message.Title ?? "").Trim(),
      Content = (message.Content ?? "").Trim(),
      Category = string.IsNullOrWhiteSpace(message.Category) ? "Update" : message.Category.Trim(),
      DateUtc = string.IsNullOrWhiteSpace(message.DateUtc) ? null : message.DateUtc.Trim(),
      PostUrl = string.IsNullOrWhiteSpace(message.PostUrl) ? null : message.PostUrl.Trim(),
      LinkUrl = string.IsNullOrWhiteSpace(message.LinkUrl) ? null : message.LinkUrl.Trim(),
      LinkText = string.IsNullOrWhiteSpace(message.LinkText) ? null : message.LinkText.Trim(),
      AckUtc = nowUtc.ToString("O")
    };

    var fileHint = !string.IsNullOrWhiteSpace(archive.SourcePostId)
      ? archive.SourcePostId
      : sourceKey;
    fileHint = SanitizeFileNamePart(fileHint);
    if (string.IsNullOrWhiteSpace(fileHint))
    {
      fileHint = archive.ArchiveId;
    }

    var filePath = Path.Combine(_archiveFolderPath, $"{nowUtc:yyyyMMdd_HHmmss_fff}_{fileHint}.json");

    try
    {
      var json = JsonSerializer.Serialize(archive, new JsonSerializerOptions
      {
        WriteIndented = true
      });
      File.WriteAllText(filePath, json);
    }
    catch
    {
      return false;
    }

    lock (_archiveLock)
    {
      _archivedMessages.Add(archive);
      _archivedMessages = _archivedMessages
        .OrderByDescending(m => m.AckUtc)
        .ToList();
      _lastArchiveEnvelopeJson = BuildArchiveStateEnvelopeJson(_archivedMessages);
    }

    return true;
  }

  private static string BuildArchiveSourceKey(MarkReadMessage message)
  {
    if (!string.IsNullOrWhiteSpace(message.PostKey))
      return message.PostKey.Trim();

    if (!string.IsNullOrWhiteSpace(message.PostId))
      return message.PostId.Trim();

    var title = (message.Title ?? "").Trim();
    var dateUtc = (message.DateUtc ?? "").Trim();
    var postUrl = (message.PostUrl ?? "").Trim();
    var linkUrl = (message.LinkUrl ?? "").Trim();
    var content = (message.Content ?? "").Trim();
    var composed = !string.IsNullOrWhiteSpace(postUrl) || !string.IsNullOrWhiteSpace(linkUrl)
      ? $"local:{title}|{dateUtc}|{postUrl}|{linkUrl}|{content}"
      : $"local:{title}|{dateUtc}|{content}";
    return composed.Trim();
  }

  private static string BuildArchiveSourceKey(UiPost post)
  {
    if (!string.IsNullOrWhiteSpace(post.Id))
      return post.Id.Trim();

    var title = (post.Title ?? "").Trim();
    var dateUtc = (post.DateUtc ?? "").Trim();
    var postUrl = (post.PostUrl ?? "").Trim();
    var linkUrl = (post.LinkUrl ?? "").Trim();
    var content = (post.Content ?? "").Trim();
    var composed = !string.IsNullOrWhiteSpace(postUrl) || !string.IsNullOrWhiteSpace(linkUrl)
      ? $"local:{title}|{dateUtc}|{postUrl}|{linkUrl}|{content}"
      : $"local:{title}|{dateUtc}|{content}";
    return composed.Trim();
  }

  private static string SanitizeFileNamePart(string value)
  {
    var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
    var cleaned = new string(value
      .Where(ch => !invalidChars.Contains(ch))
      .ToArray());

    cleaned = cleaned.Trim();
    if (cleaned.Length > 80) cleaned = cleaned[..80];
    return cleaned;
  }

  private static List<UiPost> ParsePosts(string rawJson)
  {
    if (string.IsNullOrWhiteSpace(rawJson)) return new List<UiPost>();

    using var doc = JsonDocument.Parse(rawJson);
    var postCandidates = new List<JsonElement>();
    CollectPostCandidates(doc.RootElement, postCandidates, depth: 0);

    var posts = new List<UiPost>();
    foreach (var candidate in postCandidates)
    {
      if (candidate.ValueKind != JsonValueKind.Object) continue;
      var mapped = MapPost(candidate);
      if (mapped is null) continue;
      posts.Add(mapped);
    }

    return posts;
  }

  private static void CollectPostCandidates(JsonElement node, List<JsonElement> sink, int depth)
  {
    if (depth > 6) return;

    if (node.ValueKind == JsonValueKind.Array)
    {
      if (LooksLikePostsArray(node))
      {
        foreach (var item in node.EnumerateArray()) sink.Add(item);
        return;
      }

      foreach (var item in node.EnumerateArray())
      {
        CollectPostCandidates(item, sink, depth + 1);
      }

      return;
    }

    if (node.ValueKind != JsonValueKind.Object) return;

    var preferredKeys = new[] { "posts", "data", "results", "items", "rows" };
    foreach (var key in preferredKeys)
    {
      if (!node.TryGetProperty(key, out var candidate)) continue;
      if (candidate.ValueKind != JsonValueKind.Array) continue;
      if (!LooksLikePostsArray(candidate)) continue;

      foreach (var item in candidate.EnumerateArray()) sink.Add(item);
      return;
    }

    foreach (var prop in node.EnumerateObject())
    {
      CollectPostCandidates(prop.Value, sink, depth + 1);
    }
  }

  private static bool LooksLikePostsArray(JsonElement arr)
  {
    if (arr.ValueKind != JsonValueKind.Array) return false;

    foreach (var item in arr.EnumerateArray())
    {
      if (item.ValueKind != JsonValueKind.Object) continue;

      if (
        item.TryGetProperty("title", out _) ||
        item.TryGetProperty("content", out _) ||
        item.TryGetProperty("contentHtml", out _) ||
        item.TryGetProperty("postUrl", out _)
      )
      {
        return true;
      }
    }

    return false;
  }

  private static UiPost? MapPost(JsonElement obj)
  {
    var code = ReadFirstString(obj, "code", "postCode", "post_code");
    var id = ReadFirstString(obj, TrackIdPropertyNames) ?? "";
    var trackIdCandidates = BuildTrackIdCandidates(obj, id, code);
    var title = ReadString(obj, "title") ?? "Untitled post";

    var content = ReadString(obj, "content");
    var contentHtml = ReadString(obj, "contentHtml");

    if (string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(contentHtml))
    {
      content = HtmlToPlainText(contentHtml);
    }

    var dateRaw = ReadString(obj, "date")
      ?? ReadString(obj, "publishedAt")
      ?? ReadString(obj, "createdAt")
      ?? ReadString(obj, "updatedAt");

    var post = new UiPost
    {
      Id = id,
      Code = code,
      TrackIdCandidates = trackIdCandidates,
      Title = title,
      Content = content?.Trim() ?? "",
      Category = ReadString(obj, "category") ?? "Update",
      DateUtc = ParseDateToIsoUtc(dateRaw),
      PostUrl = ReadString(obj, "postUrl") ?? ReadString(obj, "url"),
      LinkUrl = ReadString(obj, "linkUrl"),
      LinkText = ReadString(obj, "linkText"),
      IsPublished = ReadBool(obj, "published") ?? true
    };

    return post;
  }

  private static List<string> BuildTrackIdCandidates(JsonElement obj, string? preferredId, string? code)
  {
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var candidates = new List<string>();

    static void AddCandidate(List<string> sink, HashSet<string> seenIds, string? value)
    {
      var candidate = (value ?? "").Trim();
      if (string.IsNullOrWhiteSpace(candidate)) return;
      if (seenIds.Add(candidate))
      {
        sink.Add(candidate);
      }
    }

    AddCandidate(candidates, seen, preferredId);

    foreach (var key in TrackIdPropertyNames)
    {
      AddCandidate(candidates, seen, ReadString(obj, key));
    }

    if (obj.TryGetProperty("post", out var nestedPost) && nestedPost.ValueKind == JsonValueKind.Object)
    {
      foreach (var key in TrackIdPropertyNames)
      {
        AddCandidate(candidates, seen, ReadString(nestedPost, key));
      }
    }

    AddCandidate(candidates, seen, code);
    return candidates;
  }

  private static string? ParseDateToIsoUtc(string? raw)
  {
    if (string.IsNullOrWhiteSpace(raw)) return null;

    return DateTimeOffset.TryParse(raw, out var dto)
      ? dto.UtcDateTime.ToString("O")
      : null;
  }

  private static DateTimeOffset GetSortDate(UiPost post)
  {
    if (string.IsNullOrWhiteSpace(post.DateUtc)) return DateTimeOffset.MinValue;
    return DateTimeOffset.TryParse(post.DateUtc, out var dto) ? dto : DateTimeOffset.MinValue;
  }

  private static string HtmlToPlainText(string html)
  {
    if (string.IsNullOrWhiteSpace(html)) return "";

    var noTags = HtmlTagRegex.Replace(html, " ");
    var decoded = WebUtility.HtmlDecode(noTags);
    var normalized = Regex.Replace(decoded, @"\s+", " ").Trim();
    return normalized;
  }

  private static string? ReadString(JsonElement obj, string propertyName)
  {
    if (!obj.TryGetProperty(propertyName, out var value)) return null;

    return value.ValueKind switch
    {
      JsonValueKind.String => value.GetString(),
      JsonValueKind.Number => value.GetRawText(),
      JsonValueKind.True => "true",
      JsonValueKind.False => "false",
      _ => null
    };
  }

  private static string? ReadFirstString(JsonElement obj, params string[] propertyNames)
  {
    foreach (var propertyName in propertyNames)
    {
      var value = ReadString(obj, propertyName);
      if (!string.IsNullOrWhiteSpace(value))
      {
        return value;
      }
    }

    return null;
  }

  private static string ReadJsonMessageString(JsonElement obj, string propertyName)
  {
    if (!obj.TryGetProperty(propertyName, out var value)) return "";

    return value.ValueKind switch
    {
      JsonValueKind.String => value.GetString() ?? "",
      JsonValueKind.Number => value.GetRawText(),
      JsonValueKind.True => "true",
      JsonValueKind.False => "false",
      _ => ""
    };
  }

  private static List<string> ReadJsonStringArray(JsonElement obj, string propertyName)
  {
    var values = new List<string>();
    if (!obj.TryGetProperty(propertyName, out var arrayValue)) return values;
    if (arrayValue.ValueKind != JsonValueKind.Array) return values;

    foreach (var item in arrayValue.EnumerateArray())
    {
      var value = item.ValueKind switch
      {
        JsonValueKind.String => item.GetString() ?? "",
        JsonValueKind.Number => item.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => ""
      };

      value = value.Trim();
      if (!string.IsNullOrWhiteSpace(value))
      {
        values.Add(value);
      }
    }

    return values;
  }

  private static bool? ReadBool(JsonElement obj, string propertyName)
  {
    if (!obj.TryGetProperty(propertyName, out var value)) return null;

    if (value.ValueKind == JsonValueKind.True) return true;
    if (value.ValueKind == JsonValueKind.False) return false;

    if (value.ValueKind == JsonValueKind.String)
    {
      var s = value.GetString();
      if (bool.TryParse(s, out var b)) return b;
      if (string.Equals(s, "1", StringComparison.Ordinal)) return true;
      if (string.Equals(s, "0", StringComparison.Ordinal)) return false;
    }

    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
    {
      return n != 0;
    }

    return null;
  }

  private static string ExtractMessage(string? body)
  {
    if (string.IsNullOrWhiteSpace(body)) return "No response body.";

    var trimmed = body.Trim();

    if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
    {
      try
      {
        using var doc = JsonDocument.Parse(trimmed);

        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
          if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
            return m.GetString() ?? "Unknown API error.";

          if (root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
            return e.GetString() ?? "Unknown API error.";
        }
      }
      catch
      {
      }
    }

    if (trimmed.Length <= 300) return trimmed;
    return trimmed[..300] + "…";
  }

  private static int ParseUnreadCount(string rawJson)
  {
    if (string.IsNullOrWhiteSpace(rawJson)) return 0;

    using var doc = JsonDocument.Parse(rawJson);
    var root = doc.RootElement;

    if (root.ValueKind == JsonValueKind.Number && root.TryGetInt32(out var direct))
    {
      return Math.Max(0, direct);
    }

    if (root.ValueKind != JsonValueKind.Object) return 0;

    var candidateKeys = new[] { "number", "count", "unread", "unreadCount", "unread_count", "total" };
    foreach (var key in candidateKeys)
    {
      if (!root.TryGetProperty(key, out var value)) continue;

      if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
      {
        return Math.Max(0, n);
      }

      if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
      {
        return Math.Max(0, parsed);
      }
    }

    return 0;
  }

  private bool ShouldForceForegroundForUrgent(int? unreadDelta)
  {
    if (WindowState == WindowState.Minimized) return true;

    if (_cfg.FocusStealCooldownMs > 0 && _lastForcedForegroundAt != DateTime.MinValue)
    {
      var elapsedMs = (DateTime.UtcNow - _lastForcedForegroundAt).TotalMilliseconds;
      if (elapsedMs >= 0 && elapsedMs < _cfg.FocusStealCooldownMs) return false;
    }

    if (_cfg.ForceForegroundOnUrgent) return true;
    return unreadDelta.HasValue && unreadDelta.Value >= _cfg.UrgentFocusUnreadDelta;
  }

  private void BringToFront(bool force)
  {
    var hwnd = new WindowInteropHelper(this).Handle;
    if (hwnd == IntPtr.Zero) return;

    try
    {
      if (IsIconic(hwnd) || WindowState == WindowState.Minimized)
      {
        ShowWindow(hwnd, SW_RESTORE);
        WindowState = WindowState.Normal;
      }

      Show();
      Activate();

      if (force)
      {
        var fg = GetForegroundWindow();
        var fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
        var curThread = GetCurrentThreadId();
        var attached = false;

        try
        {
          if (fg != IntPtr.Zero && fgThread != 0 && fgThread != curThread)
          {
            attached = AttachThreadInput(curThread, fgThread, true);
          }
          SetForegroundWindow(hwnd);
        }
        finally
        {
          if (attached)
          {
            AttachThreadInput(curThread, fgThread, false);
          }
        }

        var wasTop = Topmost;
        Topmost = true;
        Topmost = wasTop;
        _lastForcedForegroundAt = DateTime.UtcNow;
      }
      else
      {
        FlashTaskbar(hwnd);
      }

      Focus();
    }
    catch
    {
    }
  }

  private static void FlashTaskbar(IntPtr hwnd)
  {
    try
    {
      var fi = new FLASHWINFO
      {
        cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
        hwnd = hwnd,
        dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
        uCount = 2,
        dwTimeout = 0
      };
      FlashWindowEx(ref fi);
    }
    catch
    {
    }
  }

  private async Task InjectConfigAndBootAsync()
  {
    var payload = new
    {
      refresh_ms = _cfg.RefreshMs,
      max_posts = _cfg.MaxPosts,
      product_id = _cfg.ProductId,
      beamer_user_id = _beamerVisitorUserId,
      custom_user_id = _viewerUserId,
      viewer_user_id = _viewerUserId,
      viewer_first_name = _viewerFirstName,
      viewer_last_name = _viewerLastName,
      viewer_email = _viewerEmail,
      config_path = _cfgPath,
      enable_secret_menu = _cfg.EnableSecretMenu,
      ui_app_title = _cfg.UiAppTitle,
      ui_header_title = _cfg.UiHeaderTitle,
      theme_page_background = _cfg.ThemePageBackground,
      theme_header_start = _cfg.ThemeHeaderStart,
      theme_header_end = _cfg.ThemeHeaderEnd,
      theme_text_main = _cfg.ThemeTextMain,
      theme_text_muted = _cfg.ThemeTextMuted,
      theme_accent = _cfg.ThemeAccent,
      theme_accent_soft = _cfg.ThemeAccentSoft,
      theme_unread_start = _cfg.ThemeUnreadStart,
      theme_unread_end = _cfg.ThemeUnreadEnd,
      theme_read_start = _cfg.ThemeReadStart,
      theme_read_end = _cfg.ThemeReadEnd
    };

    var json = JsonSerializer.Serialize(payload, _jsonOptions);

    var js = $$"""
      (function(){
        window.__BV_CFG__ = {{json}};
        if (typeof window.__BV_BOOT__ === 'function') window.__BV_BOOT__();
      })();
    """;

    await Web.ExecuteScriptAsync(js);
  }

  public void AllowCloseForUpdateOrSystem()
  {
    _allowClose = true;
  }
}
