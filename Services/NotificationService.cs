using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace DynamicIslandWin.Services;

public sealed class IslandToastBanner
{
    public string Icon { get; init; } = "🔔";
    public string Title { get; init; } = "";
    public string Message { get; init; } = "";
    public string AccentHex { get; init; } = "#6C63FF";
}

public sealed class NotificationService
{
    private UserNotificationListener? _listener;
    private bool _isAuthorized;
    private readonly HashSet<uint> _seenNotificationIds = new();
    private bool _firstPoll = true;

    public async Task InitializeAsync()
    {
        try
        {
            _listener = UserNotificationListener.Current;
            var access = await _listener.RequestAccessAsync();
            _isAuthorized = access == UserNotificationListenerAccessStatus.Allowed;
        }
        catch
        {
            _isAuthorized = false;
        }
    }

    public async Task<IslandToastBanner?> PollLatestWindowsNotificationAsync()
    {
        if (!_isAuthorized || _listener == null) return null;

        try
        {
            var notifs = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
            if (notifs == null) return null;

            IslandToastBanner? newestBanner = null;

            foreach (var n in notifs)
            {
                if (_seenNotificationIds.Add(n.Id) && !_firstPoll)
                {
                    var binding = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
                    if (binding != null)
                    {
                        var texts = binding.GetTextElements().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
                        string appName = "Bildirim";
                        try { appName = n.AppInfo?.DisplayInfo?.DisplayName ?? "Bildirim"; } catch { }

                        string title = texts.Count > 0 ? texts[0] : appName;
                        string body = texts.Count > 1 ? texts[1] : "";

                        newestBanner = new IslandToastBanner
                        {
                            Icon = "🔔",
                            Title = $"{appName}: {title}",
                            Message = body,
                            AccentHex = "#00E5A3"
                        };
                    }
                }
            }

            _firstPoll = false;
            return newestBanner;
        }
        catch
        {
            return null;
        }
    }
}
