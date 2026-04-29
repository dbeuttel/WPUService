namespace WPUService;

internal sealed class PresenceEngine : IDisposable
{
    private const int TickIntervalMs = 30_000;
    private const long RecentManualInputMs = 4 * 60 * 1000;

    private readonly System.Threading.Timer _timer;
    private readonly Func<long> _lastRealInputTick;
    private readonly object _sync = new();
    private volatile bool _enabled;
    private volatile bool _pauseOnTeamsCall;
    private volatile bool _pausedByCall;
    private volatile uint _idleThresholdMs;

    public event EventHandler? PausedByCallChanged;

    public PresenceEngine(bool enabled, bool pauseOnTeamsCall, int idleThresholdSeconds, Func<long> lastRealInputTick)
    {
        _enabled = enabled;
        _pauseOnTeamsCall = pauseOnTeamsCall;
        _idleThresholdMs = (uint)Math.Max(30, idleThresholdSeconds) * 1000u;
        _lastRealInputTick = lastRealInputTick;
        _timer = new System.Threading.Timer(OnTick, null, TickIntervalMs, TickIntervalMs);
    }

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public bool PauseOnTeamsCall
    {
        get => _pauseOnTeamsCall;
        set
        {
            _pauseOnTeamsCall = value;
            if (!value) ClearCallPause();
        }
    }

    public int IdleThresholdSeconds
    {
        get => (int)(_idleThresholdMs / 1000u);
        set => _idleThresholdMs = (uint)Math.Max(30, value) * 1000u;
    }

    public bool PausedByCall => _pausedByCall;

    public void HandleTeamsNotification()
    {
        if (!_enabled || !_pauseOnTeamsCall) return;

        lock (_sync)
        {
            if (_pausedByCall) return;
            var sinceRealInput = Environment.TickCount64 - _lastRealInputTick();
            if (sinceRealInput < RecentManualInputMs) return;
            _pausedByCall = true;
        }
        PausedByCallChanged?.Invoke(this, EventArgs.Empty);
    }

    public void HandleRealInput()
    {
        ClearCallPause();
    }

    public void ForceCallPause()
    {
        lock (_sync)
        {
            if (_pausedByCall) return;
            _pausedByCall = true;
        }
        PausedByCallChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearCallPause()
    {
        bool wasPaused;
        lock (_sync)
        {
            wasPaused = _pausedByCall;
            _pausedByCall = false;
        }
        if (wasPaused) PausedByCallChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnTick(object? state)
    {
        try
        {
            if (!_enabled) return;
            if (_pausedByCall) return;
            var idle = NativeMethods.GetIdleMilliseconds();
            if (idle >= _idleThresholdMs)
                NativeMethods.SendKeyTap(NativeMethods.VK_F15);
        }
        catch { }
    }

    public void Dispose() => _timer.Dispose();
}
