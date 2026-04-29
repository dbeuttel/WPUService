using System.Text;
using System.Xml;
using Microsoft.Data.Sqlite;

namespace WPUService;

internal sealed class TeamsNotificationEventArgs : EventArgs
{
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public string AppName { get; init; } = "";
}

internal sealed class NotificationCapturedEventArgs : EventArgs
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string AppName { get; init; } = "";
    public string AppUserModelId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public bool IsTeams { get; init; }
}

/// <summary>
/// Polls Windows' wpndatabase.db (the toast/Action-Center store) to capture every notification
/// without needing UserNotificationListener consent.
/// </summary>
internal sealed class TeamsNotificationWatcher : IDisposable
{
    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "Windows", "Notifications", "wpndatabase.db");

    private const int PollIntervalMs = 2000;

    private static readonly string[] CallMarkers =
    {
        "incoming call",
        "is calling",
        "calling you",
        "ringing",
    };

    private System.Threading.Timer? _timer;
    private long _lastOrder = -1;
    private volatile TeamsFilterMode _filter;
    private DateTime? _lastNotificationAt;
    private string _lastAccessStatus = "Unknown";

    public event EventHandler<TeamsNotificationEventArgs>? TeamsNotificationReceived;
    public event EventHandler<NotificationCapturedEventArgs>? NotificationCaptured;

    public bool AccessGranted { get; private set; }
    public string LastAccessStatus => _lastAccessStatus;
    public DateTime? LastNotificationAt => _lastNotificationAt;

    public TeamsFilterMode Filter
    {
        get => _filter;
        set => _filter = value;
    }

    public TeamsNotificationWatcher(TeamsFilterMode initialFilter)
    {
        _filter = initialFilter;
    }

    public Task<bool> StartAsync()
    {
        try
        {
            if (!File.Exists(DbPath))
            {
                _lastAccessStatus = "wpndatabase.db not found at " + DbPath;
                AccessGranted = false;
                return Task.FromResult(false);
            }

            using (var conn = OpenReadOnly())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT MAX(\"Order\") FROM Notification";
                var r = cmd.ExecuteScalar();
                _lastOrder = (r != null && r != DBNull.Value) ? Convert.ToInt64(r) : 0;
            }

            _lastAccessStatus = "Reading wpndatabase.db";
            AccessGranted = true;

            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ => Poll(), null, PollIntervalMs, PollIntervalMs);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _lastAccessStatus = $"{ex.GetType().Name} (HRESULT 0x{ex.HResult:X8}): {ex.Message}";
            AccessGranted = false;
            return Task.FromResult(false);
        }
    }

    private static SqliteConnection OpenReadOnly()
    {
        var cs = $"Data Source={DbPath};Mode=ReadOnly";
        var conn = new SqliteConnection(cs);
        conn.Open();
        return conn;
    }

    private void Poll()
    {
        try
        {
            using var conn = OpenReadOnly();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT n.""Order"", n.Payload, n.PayloadType, n.ArrivalTime, n.Type, h.PrimaryId
                FROM Notification n
                LEFT JOIN NotificationHandler h ON n.HandlerId = h.RecordId
                WHERE n.""Order"" > $last
                ORDER BY n.""Order"" ASC";
            cmd.Parameters.AddWithValue("$last", _lastOrder);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var order = reader.GetInt64(0);
                _lastOrder = order;

                var payload = reader.IsDBNull(1) ? null : (byte[])reader["Payload"];
                var payloadType = reader.IsDBNull(2) ? "" : reader.GetString(2);
                var arrival = reader.IsDBNull(3) ? 0 : reader.GetInt64(3);
                var type = reader.IsDBNull(4) ? "" : reader.GetString(4);
                var primaryId = reader.IsDBNull(5) ? "" : reader.GetString(5);

                if (!string.Equals(type, "toast", StringComparison.OrdinalIgnoreCase)) continue;
                if (payload == null || payload.Length == 0) continue;

                var (title, body) = ParseToastPayload(payload, payloadType);
                if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body)) continue;

                var ts = ConvertWindowsTicks(arrival);
                var isTeams = LooksLikeTeams(primaryId);
                var appName = ExtractAppName(primaryId);

                NotificationCaptured?.Invoke(this, new NotificationCapturedEventArgs
                {
                    Timestamp = ts,
                    AppName = appName,
                    AppUserModelId = primaryId,
                    Title = title,
                    Body = body,
                    IsTeams = isTeams,
                });

                if (!isTeams) continue;
                if (_filter == TeamsFilterMode.CallsOnly && !LooksLikeCall(title, body)) continue;

                _lastNotificationAt = ts;
                TeamsNotificationReceived?.Invoke(this, new TeamsNotificationEventArgs
                {
                    Title = title,
                    Body = body,
                    AppName = appName,
                });
            }
        }
        catch (Exception ex)
        {
            _lastAccessStatus = $"Poll error: {ex.Message}";
        }
    }

    private static bool LooksLikeTeams(string aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return false;
        return aumid.Contains("Teams", StringComparison.OrdinalIgnoreCase)
            || aumid.Contains("MSTeams", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractAppName(string aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return "<unknown>";
        if (aumid.Contains("MSTeams", StringComparison.OrdinalIgnoreCase)
            || aumid.Contains("com.squirrel.Teams", StringComparison.OrdinalIgnoreCase))
            return "Microsoft Teams";
        if (aumid.Contains("Outlook", StringComparison.OrdinalIgnoreCase)) return "Outlook";
        if (aumid.Contains("Slack", StringComparison.OrdinalIgnoreCase)) return "Slack";
        if (aumid.Contains("Discord", StringComparison.OrdinalIgnoreCase)) return "Discord";
        // Strip package family + "!appid" syntax to a readable form
        var bang = aumid.IndexOf('!');
        var trimmed = bang > 0 ? aumid.Substring(bang + 1) : aumid;
        var parts = trimmed.Split('.', '_');
        return parts.Length > 0 ? parts[^1] : aumid;
    }

    private static (string title, string body) ParseToastPayload(byte[] payload, string payloadType)
    {
        try
        {
            var xml = DecodeBytes(payload);
            if (string.IsNullOrWhiteSpace(xml)) return ("", "");

            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(xml);
            }
            catch
            {
                return ("", "");
            }

            var texts = doc.GetElementsByTagName("text");
            if (texts.Count == 0) return ("", "");

            var title = texts[0]?.InnerText?.Trim() ?? "";
            var bodyParts = new List<string>();
            for (int i = 1; i < texts.Count; i++)
            {
                var t = texts[i]?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(t)) bodyParts.Add(t!);
            }
            return (title, string.Join("\n", bodyParts));
        }
        catch
        {
            return ("", "");
        }
    }

    private static string DecodeBytes(byte[] data)
    {
        if (data.Length == 0) return "";
        // UTF-16 LE BOM
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return Encoding.Unicode.GetString(data, 2, data.Length - 2);
        // UTF-8 BOM
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return Encoding.UTF8.GetString(data, 3, data.Length - 3);
        // Heuristic: every other byte zero suggests UTF-16 LE
        if (data.Length >= 4 && data[1] == 0 && data[3] == 0)
            return Encoding.Unicode.GetString(data);
        return Encoding.UTF8.GetString(data);
    }

    private static DateTime ConvertWindowsTicks(long ticks)
    {
        try
        {
            return ticks > 0 ? DateTime.FromFileTime(ticks) : DateTime.Now;
        }
        catch
        {
            return DateTime.Now;
        }
    }

    private static bool LooksLikeCall(string title, string body)
    {
        var haystack = (title + " " + body).ToLowerInvariant();
        foreach (var marker in CallMarkers)
        {
            if (haystack.Contains(marker)) return true;
        }
        return false;
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
