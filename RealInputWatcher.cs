using System.Runtime.InteropServices;

namespace WPUService;

internal sealed class RealInputWatcher : IDisposable
{
    private readonly NativeMethods.LowLevelProc _keyboardProc;
    private readonly NativeMethods.LowLevelProc _mouseProc;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private long _lastRealInputTick;

    public event EventHandler? RealInputDetected;

    public RealInputWatcher()
    {
        _lastRealInputTick = Environment.TickCount64;
        _keyboardProc = KeyboardHookProc;
        _mouseProc = MouseHookProc;
        _keyboardHook = NativeMethods.InstallLowLevelHook(NativeMethods.WH_KEYBOARD_LL, _keyboardProc);
        _mouseHook = NativeMethods.InstallLowLevelHook(NativeMethods.WH_MOUSE_LL, _mouseProc);
    }

    public bool HooksInstalled => _keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero;

    public long LastRealInputTick => Interlocked.Read(ref _lastRealInputTick);

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == NativeMethods.HC_ACTION && lParam != IntPtr.Zero)
        {
            var flags = Marshal.ReadInt32(lParam, NativeMethods.KBDLLHOOKSTRUCT_FLAGS_OFFSET);
            if ((flags & NativeMethods.LLKHF_INJECTED) == 0)
                OnRealInput();
        }
        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == NativeMethods.HC_ACTION && lParam != IntPtr.Zero)
        {
            var flags = Marshal.ReadInt32(lParam, NativeMethods.MSLLHOOKSTRUCT_FLAGS_OFFSET);
            if ((flags & NativeMethods.LLMHF_INJECTED) == 0)
                OnRealInput();
        }
        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void OnRealInput()
    {
        Interlocked.Exchange(ref _lastRealInputTick, Environment.TickCount64);
        RealInputDetected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            try { NativeMethods.UnhookWindowsHookEx(_keyboardHook); } catch { }
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            try { NativeMethods.UnhookWindowsHookEx(_mouseHook); } catch { }
            _mouseHook = IntPtr.Zero;
        }
    }
}
