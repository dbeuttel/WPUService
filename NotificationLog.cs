using System.Text.Json;

namespace WPUService;

internal sealed class NotificationEntry
{
    public DateTime Timestamp { get; set; }
    public string AppName { get; set; } = "";
    public string AppUserModelId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsTeams { get; set; }
}

internal sealed class NotificationLog
{
    private const int MaxEntries = 1000;

    private readonly object _sync = new();
    private readonly List<NotificationEntry> _entries = new();

    public static string LogPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WPUService",
            "notifications.json");

    public NotificationLog()
    {
        Load();
    }

    public IReadOnlyList<NotificationEntry> Snapshot()
    {
        lock (_sync) return _entries.ToList();
    }

    public int Count
    {
        get { lock (_sync) return _entries.Count; }
    }

    public void Add(NotificationEntry entry)
    {
        lock (_sync)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
            SaveUnlocked();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
            SaveUnlocked();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(LogPath)) return;
            var json = File.ReadAllText(LogPath);
            var loaded = JsonSerializer.Deserialize<List<NotificationEntry>>(json);
            if (loaded != null)
            {
                lock (_sync) _entries.AddRange(loaded);
            }
        }
        catch { }
    }

    private void SaveUnlocked()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = false });
            File.WriteAllText(LogPath, json);
        }
        catch { }
    }
}
