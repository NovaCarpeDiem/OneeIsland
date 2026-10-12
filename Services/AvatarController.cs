using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DynamicIslandWin.Services;

public enum AvatarState
{
    Idle,
    Listening,
    Thinking,
    Searching,
    Working,
    Happy,
    Excited,
    Surprised,
    Confused,
    Bored,
    Angry,
    Drowsy,
    Curious,
    Proud,
    Shy,
    Sad,
    Laughing,
    Scared,
    Playful,
    Celebrate,
    Sleeping,
    Waking
}

public class AvatarController
{
    private readonly WebView2 _webView;
    private bool _isInitialized = false;
    private AvatarState _currentState = AvatarState.Idle;
    private string _currentAnimationName = "idle";
    private Color _currentBodyColor = Color.FromRgb(108, 99, 255); // #6C63FF default

    public event Action<string>? AnimationChanged;
    public event Action? Ready;

    public AvatarState CurrentState => _currentState;
    public string CurrentAnimationName => _currentAnimationName;
    public bool IsInitialized => _isInitialized;

    public AvatarController(WebView2 webView)
    {
        _webView = webView ?? throw new ArgumentNullException(nameof(webView));
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Use local app data folder for WebView2 user cache to keep it clean and isolated
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DynamicIslandWin",
                "WebView2Profile");
            
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(env);

            // Make WebView2 background transparent so Dynamic Island glass background shows through cleanly
            _webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            // Configure settings: disable unnecessary UI chrome and context menus
            var settings = _webView.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;

            _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // Navigate to avatar_view.html in application directory
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string htmlPath = Path.Combine(appDir, "Avatar", "avatar_view.html");

            if (File.Exists(htmlPath))
            {
                _webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[AvatarController] HTML path not found: {htmlPath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AvatarController] Init failed: {ex.Message}");
        }
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            if (doc.RootElement.TryGetProperty("type", out var typeElem))
            {
                string? type = typeElem.GetString();
                if (type == "ready")
                {
                    _isInitialized = true;
                    // Apply pending color and state
                    SetAccentColor(_currentBodyColor);
                    SetState(_currentState);
                    Ready?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AvatarController] WebMessage error: {ex.Message}");
        }
    }

    /// <summary>
    /// Play animation by state enum
    /// </summary>
    public void SetState(AvatarState state)
    {
        _currentState = state;
        string anim = state switch
        {
            AvatarState.Idle => "idle",
            AvatarState.Listening => "listening",
            AvatarState.Thinking => "thinking",
            AvatarState.Searching => "searching",
            AvatarState.Working => "working",
            AvatarState.Happy => "happy",
            AvatarState.Excited => "excited",
            AvatarState.Surprised => "surprised",
            AvatarState.Confused => "confused",
            AvatarState.Bored => "bored",
            AvatarState.Angry => "angry",
            AvatarState.Drowsy => "drowsy",
            AvatarState.Curious => "curious",
            AvatarState.Proud => "proud",
            AvatarState.Shy => "shy",
            AvatarState.Sad => "sad",
            AvatarState.Laughing => "laughing",
            AvatarState.Scared => "scared",
            AvatarState.Playful => "playful",
            AvatarState.Celebrate => "celebrate",
            AvatarState.Sleeping => "sleeping",
            AvatarState.Waking => "waking",
            _ => "idle"
        };

        PlayAnimation(anim);
    }

    /// <summary>
    /// Play animation by exact string name
    /// </summary>
    public void PlayAnimation(string animationName)
    {
        _currentAnimationName = animationName;
        AnimationChanged?.Invoke(animationName);

        if (!_isInitialized || _webView.CoreWebView2 == null) return;

        try
        {
            var msg = new { type = "play", animation = animationName };
            string json = JsonSerializer.Serialize(msg);
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AvatarController] PlayAnimation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Adapt avatar body color to Dynamic Island's theme or music accent
    /// </summary>
    public void SetAccentColor(Color color)
    {
        _currentBodyColor = color;

        if (!_isInitialized || _webView.CoreWebView2 == null) return;

        try
        {
            string hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            var msg = new { type = "setColor", color = hex };
            string json = JsonSerializer.Serialize(msg);
            _webView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AvatarController] SetAccentColor error: {ex.Message}");
        }
    }
}
