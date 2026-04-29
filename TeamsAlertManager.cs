using System.Text;

namespace WPUService;

internal sealed class TeamsAlertManager : IDisposable
{
    private readonly object _sync = new();
    private readonly List<PendingItem> _pending = new();
    private readonly Func<string, string, Task<(bool Ok, string Error)>> _sendAsync;
    private readonly Func<int> _alertDelaySecondsProvider;
    private System.Threading.Timer? _timer;
    private bool _alertScheduled;
    private bool _alertSent;

    public TeamsAlertManager(Func<string, string, Task<(bool, string)>> sendAsync, Func<int> alertDelaySecondsProvider)
    {
        _sendAsync = sendAsync;
        _alertDelaySecondsProvider = alertDelaySecondsProvider;
    }

    public void OnPauseStarted()
    {
        lock (_sync)
        {
            if (_alertScheduled) return;
            _alertScheduled = true;
            _alertSent = false;
            var delayMs = Math.Max(30, _alertDelaySecondsProvider()) * 1000;
            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ => Fire(), null, delayMs, Timeout.Infinite);
        }
    }

    public void OnPauseEnded()
    {
        lock (_sync)
        {
            _timer?.Dispose();
            _timer = null;
            _pending.Clear();
            _alertScheduled = false;
            _alertSent = false;
        }
    }

    public void OnTeamsNotification(TeamsNotificationEventArgs args)
    {
        lock (_sync)
        {
            if (!_alertScheduled || _alertSent) return;
            _pending.Add(new PendingItem(DateTime.Now, args.AppName, args.Title, args.Body));
        }
    }

    private void Fire()
    {
        List<PendingItem> snapshot;
        lock (_sync)
        {
            if (_alertSent) return;
            _alertSent = true;
            snapshot = _pending.ToList();
        }
        if (snapshot.Count == 0) return;

        var (subject, body) = Format(snapshot);
        _ = _sendAsync(subject, body);
    }

    private static (string subject, string body) Format(List<PendingItem> items)
    {
        if (items.Count == 1)
        {
            var i = items[0];
            var subject = $"WPU - {Truncate(i.Title, 40)}";
            var sb = new StringBuilder();
            sb.AppendLine($"[{i.At:t}] {i.AppName}");
            if (!string.IsNullOrWhiteSpace(i.Title)) sb.AppendLine(i.Title);
            if (!string.IsNullOrWhiteSpace(i.Body)) sb.AppendLine(i.Body);
            return (subject, sb.ToString().TrimEnd());
        }
        else
        {
            var subject = $"WPU - {items.Count} missed Teams notifications";
            var sb = new StringBuilder();
            sb.AppendLine($"{items.Count} Teams notifications while you were away:");
            sb.AppendLine();
            foreach (var i in items)
            {
                sb.AppendLine($"[{i.At:t}] {i.AppName}");
                if (!string.IsNullOrWhiteSpace(i.Title)) sb.AppendLine(i.Title);
                if (!string.IsNullOrWhiteSpace(i.Body)) sb.AppendLine(i.Body);
                sb.AppendLine();
            }
            return (subject, sb.ToString().TrimEnd());
        }
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= max ? s : s.Substring(0, max - 1) + "…";

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private sealed record PendingItem(DateTime At, string AppName, string Title, string Body);
}
