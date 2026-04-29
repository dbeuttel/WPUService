using System.Reflection;

namespace WPUService;

internal sealed class TrayContext : ApplicationContext
{
    private static readonly (string Label, int Seconds)[] IdlePresets =
    {
        ("1 minute", 60),
        ("2 minutes", 120),
        ("3 minutes", 180),
        ("5 minutes", 300),
        ("10 minutes", 600),
        ("15 minutes", 900),
    };

    private static readonly (string Label, int Seconds)[] AlertDelayPresets =
    {
        ("1 minute", 60),
        ("2 minutes", 120),
        ("5 minutes", 300),
        ("10 minutes", 600),
        ("15 minutes", 900),
    };

    private readonly NotifyIcon _notifyIcon;
    private readonly PresenceEngine _engine;
    private readonly RealInputWatcher _realInput;
    private readonly TeamsNotificationWatcher _teamsWatcher;
    private readonly TeamsAlertManager _alertManager;
    private readonly NotificationLog _notificationLog;
    private readonly Config _config;
    private readonly Icon _activeIcon;
    private readonly Icon _inactiveIcon;
    private readonly Control _uiMarshal;

    public TrayContext()
    {
        _config = Config.Load();
        _config.StartWithWindows = Autostart.IsEnabled();

        if (Installer.IsRunningFromInstallLocation())
        {
            try { Installer.EnsureShortcut(); } catch { }
            try { Installer.EnsureAumidRegistration(); } catch { }
        }

        _uiMarshal = new Control();
        _ = _uiMarshal.Handle;

        _realInput = new RealInputWatcher();
        _engine = new PresenceEngine(
            _config.Enabled,
            _config.PauseOnTeamsCall,
            _config.IdleThresholdSeconds,
            () => _realInput.LastRealInputTick);

        _alertManager = new TeamsAlertManager(
            SendMessageAsync,
            () => _config.AlertDelaySeconds);

        _realInput.RealInputDetected += (_, _) => _engine.HandleRealInput();
        _engine.PausedByCallChanged += (_, _) =>
        {
            if (_engine.PausedByCall) _alertManager.OnPauseStarted();
            else _alertManager.OnPauseEnded();
            PostToUi(UpdateIconAndTooltip);
        };

        _notificationLog = new NotificationLog();

        _teamsWatcher = new TeamsNotificationWatcher(_config.TeamsFilter);
        _teamsWatcher.NotificationCaptured += (_, args) =>
        {
            _notificationLog.Add(new NotificationEntry
            {
                Timestamp = args.Timestamp,
                AppName = args.AppName,
                AppUserModelId = args.AppUserModelId,
                Title = args.Title,
                Body = args.Body,
                IsTeams = args.IsTeams,
            });
        };
        _teamsWatcher.TeamsNotificationReceived += (_, args) =>
        {
            _engine.HandleTeamsNotification();
            _alertManager.OnTeamsNotification(args);
        };
        // Defer until the WinForms message loop is running so the access prompt can render.
        _uiMarshal.BeginInvoke(new Action(async () =>
        {
            var ok = await _teamsWatcher.StartAsync();
            if (!ok && _notifyIcon != null)
            {
                _notifyIcon.BalloonTipTitle = "WPUService";
                _notifyIcon.BalloonTipText =
                    "Notification access not granted. Right-click the tray > Diagnostics > Request notification access.";
                _notifyIcon.BalloonTipIcon = ToolTipIcon.Warning;
                _notifyIcon.ShowBalloonTip(8000);
            }
        }));

        _activeIcon = LoadEmbeddedIcon("active.ico") ?? SystemIcons.Application;
        _inactiveIcon = LoadEmbeddedIcon("inactive.ico") ?? SystemIcons.Application;

        var openSettingsItem = new ToolStripMenuItem("Open Settings");
        openSettingsItem.Click += (_, _) => OnSettingsClicked();

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.Add(openSettingsItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quitItem);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            _config.Enabled = !_config.Enabled;
            _engine.Enabled = _config.Enabled;
            _config.Save();
            UpdateIconAndTooltip();
        };

        UpdateIconAndTooltip();
    }

    private async Task<(bool, string)> SendMessageAsync(string subject, string body)
    {
        if (_config.SendMode == SendMode.Pushover)
        {
            var push = new PushoverSettings
            {
                UserKey = _config.PushoverUserKey,
                ApiToken = Config.UnprotectPassword(_config.PushoverApiTokenEncrypted),
            };
            return await PushoverSender.SendAsync(push, subject, body);
        }

        var to = _config.RecipientMode == RecipientMode.Sms
            ? Carriers.BuildRecipient(_config.PhoneNumber, _config.CarrierKey, _config.CustomGateway)
            : (_config.RecipientEmail ?? "").Trim();
        if (string.IsNullOrEmpty(to)) return (false, "Recipient not configured.");

        if (_config.SendMode == SendMode.Outlook && OutlookSender.IsAvailable())
            return await OutlookSender.SendAsync(to, subject, body);

        var settings = new EmailSettings
        {
            Host = _config.SmtpHost,
            Port = _config.SmtpPort,
            UseSsl = _config.SmtpUseSsl,
            Username = _config.SmtpUsername,
            Password = Config.UnprotectPassword(_config.SmtpPasswordEncrypted),
            From = _config.SmtpFromAddress,
            To = to,
        };
        return await EmailSender.SendAsync(settings, subject, body);
    }

    private void OnSettingsClicked()
    {
        var actions = new SettingsActions
        {
            ViewNotifications = OnNotificationsClicked,
            ShowStatus = ShowStatus,
            RequestNotificationAccess = OnRequestAccessClickedAsync,
            SimulateTeamsNotification = OnSimulateClicked,
            SendTestAlertNow = async () => await OnSendNowClickedAsync(),
            Uninstall = OnUninstallClicked,
            IsActive = () => _config.Enabled && !_engine.PausedByCall,
            ActiveIcon = _activeIcon,
            InactiveIcon = _inactiveIcon,
        };

        using var form = new SettingsForm(_config, actions);
        if (form.ShowDialog() != DialogResult.OK) return;

        // Form mutates _config and saves it; push runtime-affecting values
        // into the live components.
        _engine.Enabled = _config.Enabled;
        _engine.PauseOnTeamsCall = _config.PauseOnTeamsCall;
        _engine.IdleThresholdSeconds = _config.IdleThresholdSeconds;
        _teamsWatcher.Filter = _config.TeamsFilter;

        UpdateIconAndTooltip();
    }

    private void OnNotificationsClicked()
    {
        using var form = new NotificationsForm(_notificationLog);
        form.ShowDialog();
    }

    private void ShowStatus()
    {
        var idleSec = (Environment.TickCount64 - _realInput.LastRealInputTick) / 1000;
        var lastNotif = _teamsWatcher.LastNotificationAt?.ToString("g") ?? "never";
        var msg =
            $"Notification access: {_teamsWatcher.LastAccessStatus}\n" +
            $"Input hooks installed: {_realInput.HooksInstalled}\n" +
            $"Engine enabled: {_engine.Enabled}\n" +
            $"Pause on Teams: {_engine.PauseOnTeamsCall}\n" +
            $"Currently paused: {_engine.PausedByCall}\n" +
            $"Idle threshold: {_engine.IdleThresholdSeconds}s\n" +
            $"Alert delay: {_config.AlertDelaySeconds}s\n" +
            $"Teams filter: {_config.TeamsFilter}\n" +
            $"Send mode: {_config.SendMode}\n" +
            $"Real-input idle: {idleSec}s\n" +
            $"Last Teams notification seen: {lastNotif}";
        MessageBox.Show(msg, "WPUService Diagnostics",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task OnRequestAccessClickedAsync()
    {
        var shortcutOk = Installer.EnsureShortcut();
        Installer.EnsureAumidRegistration();
        var ok = await _teamsWatcher.StartAsync();
        if (ok)
        {
            MessageBox.Show("Notification access granted.", "WPUService",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var msg =
            "Notification access NOT granted.\n\n" +
            $"Status: {_teamsWatcher.LastAccessStatus}\n\n" +
            (shortcutOk
                ? "A Start Menu shortcut with the app's identity has been created. Quit and relaunch WPUService, then try again — Windows needs a fresh process to bind the identity."
                : "A Start Menu shortcut for the app could not be created. Try running the app once with admin rights or reinstalling.");
        MessageBox.Show(msg, "WPUService",
            MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void OnSimulateClicked()
    {
        _engine.ForceCallPause();
        _alertManager.OnTeamsNotification(new TeamsNotificationEventArgs
        {
            AppName = "Microsoft Teams",
            Title = "Simulated test",
            Body = "Simulated Teams notification body for end-to-end testing.",
        });
        MessageBox.Show(
            $"Pause forced. Alert will fire in {_config.AlertDelaySeconds}s if you do not move the mouse or type.\n" +
            "Move the mouse to clear the pause without sending.",
            "WPUService",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task OnSendNowClickedAsync()
    {
        var (ok, err) = await SendMessageAsync(
            "WPUService test",
            "WPUService test alert (sent now from tray, bypassing pause and delay).");
        var msg = ok ? "Test alert sent." : "Failed: " + err;
        MessageBox.Show(msg, "WPUService",
            MessageBoxButtons.OK,
            ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private void OnUninstallClicked()
    {
        var confirm = MessageBox.Show(
            "Uninstall Workstation Presence Utility?\n\n" +
            "This will stop the utility, remove its autostart entry, and delete all of its files.",
            "Uninstall",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirm != DialogResult.Yes) return;

        _notifyIcon.Visible = false;
        Installer.Uninstall();
        ExitThread();
    }

    private void UpdateIconAndTooltip()
    {
        var active = _config.Enabled && !_engine.PausedByCall;
        _notifyIcon.Icon = active ? _activeIcon : _inactiveIcon;
        _notifyIcon.Text = !_config.Enabled
            ? "Workstation Presence Utility - Paused"
            : _engine.PausedByCall
                ? "Workstation Presence Utility - Paused (Teams notification)"
                : "Workstation Presence Utility - Active";
    }

    private void PostToUi(Action action)
    {
        if (_uiMarshal.IsDisposed) return;
        if (_uiMarshal.InvokeRequired)
        {
            try { _uiMarshal.BeginInvoke(action); } catch { }
        }
        else
        {
            action();
        }
    }

    private static Icon? LoadEmbeddedIcon(string resourceName)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            var fullName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));
            if (fullName == null) return null;
            using var stream = asm.GetManifestResourceStream(fullName);
            return stream == null ? null : new Icon(stream);
        }
        catch
        {
            return null;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _alertManager.Dispose();
            _teamsWatcher.Dispose();
            _engine.Dispose();
            _realInput.Dispose();
            _uiMarshal.Dispose();
        }
        base.Dispose(disposing);
    }
}
