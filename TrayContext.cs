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
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _autostartItem;
    private readonly ToolStripMenuItem _pauseOnCallItem;
    private readonly ToolStripMenuItem _idleSubmenu;
    private readonly ToolStripMenuItem _alertDelaySubmenu;
    private readonly ToolStripMenuItem _detectSubmenu;
    private readonly ToolStripMenuItem _detectAnyItem;
    private readonly ToolStripMenuItem _detectCallsItem;
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

        _enabledItem = new ToolStripMenuItem("Enabled") { CheckOnClick = true, Checked = _config.Enabled };
        _enabledItem.CheckedChanged += OnEnabledChanged;

        _autostartItem = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = _config.StartWithWindows };
        _autostartItem.CheckedChanged += OnAutostartChanged;

        _pauseOnCallItem = new ToolStripMenuItem("Pause on Teams notification") { CheckOnClick = true, Checked = _config.PauseOnTeamsCall };
        _pauseOnCallItem.CheckedChanged += OnPauseOnCallChanged;

        _idleSubmenu = new ToolStripMenuItem("Idle threshold");
        foreach (var (label, seconds) in IdlePresets)
        {
            var item = new ToolStripMenuItem(label) { Tag = seconds, Checked = seconds == _config.IdleThresholdSeconds };
            item.Click += OnIdlePresetClicked;
            _idleSubmenu.DropDownItems.Add(item);
        }

        _alertDelaySubmenu = new ToolStripMenuItem("Alert delay");
        foreach (var (label, seconds) in AlertDelayPresets)
        {
            var item = new ToolStripMenuItem(label) { Tag = seconds, Checked = seconds == _config.AlertDelaySeconds };
            item.Click += OnAlertDelayClicked;
            _alertDelaySubmenu.DropDownItems.Add(item);
        }

        _detectAnyItem = new ToolStripMenuItem("Any Teams notification") { Checked = _config.TeamsFilter == TeamsFilterMode.Any };
        _detectAnyItem.Click += (_, _) => SetFilter(TeamsFilterMode.Any);
        _detectCallsItem = new ToolStripMenuItem("Only call notifications") { Checked = _config.TeamsFilter == TeamsFilterMode.CallsOnly };
        _detectCallsItem.Click += (_, _) => SetFilter(TeamsFilterMode.CallsOnly);
        _detectSubmenu = new ToolStripMenuItem("Detect");
        _detectSubmenu.DropDownItems.Add(_detectAnyItem);
        _detectSubmenu.DropDownItems.Add(_detectCallsItem);

        var settingsItem = new ToolStripMenuItem("Alert settings...");
        settingsItem.Click += (_, _) => OnSettingsClicked();

        var notificationsItem = new ToolStripMenuItem("View notifications...");
        notificationsItem.Click += (_, _) => OnNotificationsClicked();

        var diagnosticsSubmenu = new ToolStripMenuItem("Diagnostics");
        var statusItem = new ToolStripMenuItem("Show status...");
        statusItem.Click += (_, _) => ShowStatus();
        var requestAccessItem = new ToolStripMenuItem("Request notification access");
        requestAccessItem.Click += async (_, _) => await OnRequestAccessClickedAsync();
        var simulateItem = new ToolStripMenuItem("Simulate Teams notification");
        simulateItem.Click += (_, _) => OnSimulateClicked();
        var sendNowItem = new ToolStripMenuItem("Send test alert now");
        sendNowItem.Click += async (_, _) => await OnSendNowClickedAsync();
        diagnosticsSubmenu.DropDownItems.Add(statusItem);
        diagnosticsSubmenu.DropDownItems.Add(requestAccessItem);
        diagnosticsSubmenu.DropDownItems.Add(simulateItem);
        diagnosticsSubmenu.DropDownItems.Add(sendNowItem);

        var uninstallItem = new ToolStripMenuItem("Uninstall...");
        uninstallItem.Click += (_, _) => OnUninstallClicked();

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_pauseOnCallItem);
        menu.Items.Add(_detectSubmenu);
        menu.Items.Add(_idleSubmenu);
        menu.Items.Add(_alertDelaySubmenu);
        menu.Items.Add(settingsItem);
        menu.Items.Add(notificationsItem);
        menu.Items.Add(_autostartItem);
        menu.Items.Add(diagnosticsSubmenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(uninstallItem);
        menu.Items.Add(quitItem);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _enabledItem.Checked = !_enabledItem.Checked;
            }
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

    private void OnEnabledChanged(object? sender, EventArgs e)
    {
        _config.Enabled = _enabledItem.Checked;
        _engine.Enabled = _config.Enabled;
        _config.Save();
        UpdateIconAndTooltip();
    }

    private void OnAutostartChanged(object? sender, EventArgs e)
    {
        if (_autostartItem.Checked)
            Autostart.Enable();
        else
            Autostart.Disable();
        _config.StartWithWindows = _autostartItem.Checked;
        _config.Save();
    }

    private void OnPauseOnCallChanged(object? sender, EventArgs e)
    {
        _config.PauseOnTeamsCall = _pauseOnCallItem.Checked;
        _engine.PauseOnTeamsCall = _config.PauseOnTeamsCall;
        _config.Save();
        UpdateIconAndTooltip();
    }

    private void OnIdlePresetClicked(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem clicked || clicked.Tag is not int seconds) return;
        _config.IdleThresholdSeconds = seconds;
        _engine.IdleThresholdSeconds = seconds;
        foreach (ToolStripMenuItem item in _idleSubmenu.DropDownItems)
            item.Checked = item == clicked;
        _config.Save();
    }

    private void OnAlertDelayClicked(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem clicked || clicked.Tag is not int seconds) return;
        _config.AlertDelaySeconds = seconds;
        foreach (ToolStripMenuItem item in _alertDelaySubmenu.DropDownItems)
            item.Checked = item == clicked;
        _config.Save();
    }

    private void SetFilter(TeamsFilterMode mode)
    {
        _config.TeamsFilter = mode;
        _teamsWatcher.Filter = mode;
        _detectAnyItem.Checked = mode == TeamsFilterMode.Any;
        _detectCallsItem.Checked = mode == TeamsFilterMode.CallsOnly;
        _config.Save();
    }

    private void OnSettingsClicked()
    {
        using var form = new SettingsForm(_config);
        form.ShowDialog();
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
