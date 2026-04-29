using System.Drawing.Drawing2D;

namespace WPUService;

internal sealed class SettingsForm : Form
{
    private const string IPhoneVipUrl = "https://support.apple.com/en-us/104971";
    private const string AndroidPriorityUrl = "https://support.google.com/mail/answer/1075549?hl=en&co=GENIE.Platform%3DAndroid";
    private const string PushoverHomeUrl = "https://pushover.net/";
    private const string PushoverAppBuildUrl = "https://pushover.net/apps/build";

    private const int FormWidth = 600;
    private const int ContentWidth = FormWidth - 32;
    private const int InputLeft = 160;
    private const int InputWidth = 380;

    private static readonly Color Bg = Color.FromArgb(26, 26, 28);
    private static readonly Color BgElevated = Color.FromArgb(35, 35, 39);
    private static readonly Color BgInput = Color.FromArgb(44, 44, 48);
    private static readonly Color BorderColor = Color.FromArgb(52, 52, 58);
    private static readonly Color TextColor = Color.FromArgb(240, 240, 242);
    private static readonly Color TextDim = Color.FromArgb(154, 154, 163);
    private static readonly Color Accent = Color.FromArgb(217, 119, 87);

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
        ("Immediate", 0),
        ("1 minute", 60),
        ("2 minutes", 120),
        ("5 minutes", 300),
        ("10 minutes", 600),
        ("15 minutes", 900),
    };

    private readonly Config _config;
    private readonly SettingsActions _actions;
    private readonly bool _outlookAvailable;

    private readonly FlowLayoutPanel _flow;

    private readonly CheckBox _enabledBox;
    private readonly CheckBox _autostartBox;

    private readonly CheckBox _pauseOnCallBox;
    private readonly ComboBox _filterCombo;
    private readonly ComboBox _idleCombo;
    private readonly ComboBox _delayCombo;

    private readonly RadioButton _outlookRadio;
    private readonly RadioButton _smtpRadio;
    private readonly RadioButton _pushoverRadio;

    private readonly Panel _recipientSection;
    private readonly RadioButton _recipientEmailRadio;
    private readonly RadioButton _recipientSmsRadio;
    private readonly Label _emailLabel;
    private readonly TextBox _emailBox;
    private readonly Panel _emailHelpPanel;
    private readonly Label _phoneLabel;
    private readonly TextBox _phoneBox;
    private readonly Label _carrierLabel;
    private readonly ComboBox _carrierBox;
    private readonly Label _customGatewayLabel;
    private readonly TextBox _customGatewayBox;
    private readonly Label _smsHint;

    private readonly Panel _smtpSection;
    private readonly TextBox _smtpHostBox;
    private readonly NumericUpDown _smtpPortBox;
    private readonly CheckBox _smtpSslBox;
    private readonly TextBox _smtpFromBox;
    private readonly TextBox _smtpUserBox;
    private readonly TextBox _smtpPasswordBox;

    private readonly Panel _pushoverSection;
    private readonly TextBox _pushoverUserKeyBox;
    private readonly TextBox _pushoverTokenBox;

    private bool _initialized;
    private PictureBox? _simulateStatusIcon;
    private System.Windows.Forms.Timer? _iconPollTimer;
    private bool? _lastActiveState;

    public SettingsForm(Config config, SettingsActions actions)
    {
        _config = config;
        _actions = actions;
        _outlookAvailable = OutlookSender.IsAvailable();

        Text = "WPUService Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Bg;
        ForeColor = TextColor;
        Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        AutoScroll = true;
        ClientSize = new Size(FormWidth, 720);

        _flow = new FlowLayoutPanel
        {
            Location = new Point(12, 12),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Bg,
        };
        Controls.Add(_flow);

        // ===== Title =====
        var title = new Label
        {
            Text = "WPUService Settings",
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
            ForeColor = TextColor,
            AutoSize = true,
            Margin = new Padding(2, 0, 0, 10),
        };
        _flow.Controls.Add(title);

        // ===== General =====
        _enabledBox = MakeCheckBox("Enabled", _config.Enabled);
        var enabledDesc = MakeDescription(
            "Master switch. When off, WPUService stops watching for Teams notifications and never sends alerts. " +
            "Tip: left-click the tray icon to toggle this on or off without opening Settings. Right-click the tray icon to reach this window or quit.");

        _autostartBox = MakeCheckBox("Start with Windows", _config.StartWithWindows);
        var autostartDesc = MakeDescription(
            "Launch WPUService automatically when you log in.");

        _flow.Controls.Add(BuildSection("General",
            _enabledBox, enabledDesc,
            Spacer(8),
            _autostartBox, autostartDesc));

        // ===== Detection =====
        _pauseOnCallBox = MakeCheckBox("Pause on Teams notification", _config.PauseOnTeamsCall);
        var pauseDesc = MakeDescription(
            "When a Teams notification arrives, start a pause window. If you don't react before the alert delay expires, " +
            "an alert is sent. Turn this off to disable the pause/alert flow entirely.");

        _filterCombo = MakeCombo(InputWidth);
        _filterCombo.Items.AddRange(new object[] { "Any Teams notification", "Only call notifications" });
        _filterCombo.SelectedIndex = _config.TeamsFilter == TeamsFilterMode.CallsOnly ? 1 : 0;
        var filterRow = MakeFieldRow("Detect:", _filterCombo);
        var filterDesc = MakeDescription(
            "What kinds of Teams notifications trigger a pause. \"Calls only\" ignores chats, mentions, and reactions.");

        _idleCombo = MakeCombo(160);
        foreach (var (label, _) in IdlePresets) _idleCombo.Items.Add(label);
        _idleCombo.SelectedIndex = FindPresetIndex(IdlePresets, _config.IdleThresholdSeconds);
        var idleRow = MakeFieldRow("Idle threshold:", _idleCombo);
        var idleDesc = MakeDescription(
            "How long without keyboard or mouse activity before WPUService considers you away. Pauses below this don't fire alerts.");

        _delayCombo = MakeCombo(160);
        foreach (var (label, _) in AlertDelayPresets) _delayCombo.Items.Add(label);
        _delayCombo.SelectedIndex = FindPresetIndex(AlertDelayPresets, _config.AlertDelaySeconds);
        var delayRow = MakeFieldRow("Alert delay:", _delayCombo);
        var delayDesc = MakeDescription(
            "Grace period after a Teams notification before sending an alert. Move the mouse or type during this window to cancel. Choose Immediate to send the alert with no grace period.");

        _flow.Controls.Add(BuildSection("Detection",
            _pauseOnCallBox, pauseDesc,
            Spacer(8),
            filterRow, filterDesc,
            Spacer(6),
            idleRow, idleDesc,
            Spacer(6),
            delayRow, delayDesc));

        // ===== Delivery method =====
        _outlookRadio = MakeRadio("Outlook (logged-in profile)");
        _outlookRadio.Enabled = _outlookAvailable;
        _outlookRadio.CheckedChanged += (_, _) => Relayout();
        var outlookDesc = MakeDescription(_outlookAvailable
            ? "Send through your installed Outlook desktop app using the currently logged-in account."
            : "Outlook is not installed on this machine, so this option is disabled.");

        _smtpRadio = MakeRadio("SMTP server");
        _smtpRadio.CheckedChanged += (_, _) => Relayout();
        var smtpDesc = MakeDescription(
            "Send through a generic SMTP server (e.g. Gmail with an app password, your work mail relay).");

        _pushoverRadio = MakeRadio("Pushover (mobile push, discreet)");
        _pushoverRadio.CheckedChanged += (_, _) => Relayout();
        var pushoverDesc = MakeDescription(
            "Send a silent push notification to your phone via Pushover. No SMS, no email — discreet and reliable.");

        _flow.Controls.Add(BuildSection("Delivery method",
            _outlookRadio, outlookDesc,
            Spacer(6),
            _smtpRadio, smtpDesc,
            Spacer(6),
            _pushoverRadio, pushoverDesc));

        // ===== Recipient =====
        _recipientEmailRadio = new RadioButton
        {
            Text = "Email address",
            AutoSize = true,
            Checked = _config.RecipientMode == RecipientMode.Email,
            ForeColor = TextColor,
            BackColor = Bg,
        };
        _recipientEmailRadio.CheckedChanged += (_, _) => UpdateRecipientFields();

        _recipientSmsRadio = new RadioButton
        {
            Text = "SMS via carrier gateway",
            AutoSize = true,
            Checked = _config.RecipientMode == RecipientMode.Sms,
            ForeColor = TextColor,
            BackColor = Bg,
        };
        _recipientSmsRadio.CheckedChanged += (_, _) => UpdateRecipientFields();

        var recipientModeRow = new Panel
        {
            Width = ContentWidth - 24,
            Height = 26,
            BackColor = Bg,
        };
        _recipientEmailRadio.Location = new Point(0, 4);
        _recipientSmsRadio.Location = new Point(160, 4);
        recipientModeRow.Controls.Add(_recipientEmailRadio);
        recipientModeRow.Controls.Add(_recipientSmsRadio);

        var recipientModeDesc = MakeDescription(
            "Choose whether the alert is delivered to a regular email inbox or to a phone number via your carrier's email-to-SMS gateway.");

        _emailLabel = MakeFieldLabel("Email address:");
        _emailBox = MakeTextBox(_config.RecipientEmail);
        _emailBox.PlaceholderText = "you@example.com";
        var emailFieldRow = MakeRowFromControls(_emailLabel, _emailBox);

        _emailHelpPanel = new Panel
        {
            Width = ContentWidth - 24,
            Height = 22,
            BackColor = Bg,
            Margin = new Padding(0, 2, 0, 0),
        };
        var helpLabel = new Label
        {
            Text = "Make alerts pop on your phone:",
            AutoSize = true,
            ForeColor = TextDim,
            BackColor = Bg,
            Location = new Point(0, 4),
        };
        _emailHelpPanel.Controls.Add(helpLabel);
        var iphoneLink = MakeLink("iPhone (VIP)", IPhoneVipUrl);
        iphoneLink.Location = new Point(186, 4);
        _emailHelpPanel.Controls.Add(iphoneLink);
        var sepLabel = new Label
        {
            Text = "|",
            AutoSize = true,
            ForeColor = TextDim,
            BackColor = Bg,
            Location = new Point(258, 4),
        };
        _emailHelpPanel.Controls.Add(sepLabel);
        var androidLink = MakeLink("Android (Gmail)", AndroidPriorityUrl);
        androidLink.Location = new Point(268, 4);
        _emailHelpPanel.Controls.Add(androidLink);

        _phoneLabel = MakeFieldLabel("Phone number:");
        _phoneBox = MakeTextBox(_config.PhoneNumber);
        _phoneBox.PlaceholderText = "5551234567";
        var phoneFieldRow = MakeRowFromControls(_phoneLabel, _phoneBox);

        _carrierLabel = MakeFieldLabel("Carrier:");
        _carrierBox = MakeCombo(InputWidth);
        foreach (var c in Carriers.All) _carrierBox.Items.Add(c.DisplayName);
        SelectCarrier(_config.CarrierKey);
        _carrierBox.SelectedIndexChanged += (_, _) => UpdateCustomGatewayVisibility();
        var carrierFieldRow = MakeRowFromControls(_carrierLabel, _carrierBox);

        _customGatewayLabel = MakeFieldLabel("Custom gateway:");
        _customGatewayBox = MakeTextBox(_config.CustomGateway);
        _customGatewayBox.PlaceholderText = "e.g. tmomail.net";
        var customGatewayFieldRow = MakeRowFromControls(_customGatewayLabel, _customGatewayBox);

        _smsHint = MakeDescription("Note: carrier gateways may delay or silently drop messages.");

        _recipientSection = BuildSection("Recipient",
            recipientModeRow, recipientModeDesc,
            Spacer(8),
            emailFieldRow,
            _emailHelpPanel,
            phoneFieldRow,
            carrierFieldRow,
            customGatewayFieldRow,
            _smsHint);
        _flow.Controls.Add(_recipientSection);

        // ===== SMTP =====
        _smtpHostBox = MakeTextBox(_config.SmtpHost);
        _smtpHostBox.PlaceholderText = "smtp.gmail.com";
        var hostRow = MakeRowFromControls(MakeFieldLabel("Server host:"), _smtpHostBox);
        var hostDesc = MakeDescription("Hostname of your outgoing mail server.");

        _smtpPortBox = new NumericUpDown
        {
            Width = 100,
            Minimum = 1,
            Maximum = 65535,
            Value = Math.Clamp(_config.SmtpPort, 1, 65535),
            BackColor = BgInput,
            ForeColor = TextColor,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _smtpSslBox = new CheckBox
        {
            Text = "Use SSL/TLS",
            AutoSize = true,
            Checked = _config.SmtpUseSsl,
            ForeColor = TextColor,
            BackColor = Bg,
        };
        var portRowPanel = new Panel
        {
            Width = ContentWidth - 24,
            Height = 28,
            BackColor = Bg,
        };
        var portLabel = MakeFieldLabel("Port:");
        portLabel.Location = new Point(0, 6);
        _smtpPortBox.Location = new Point(InputLeft, 3);
        _smtpSslBox.Location = new Point(InputLeft + 110, 6);
        portRowPanel.Controls.Add(portLabel);
        portRowPanel.Controls.Add(_smtpPortBox);
        portRowPanel.Controls.Add(_smtpSslBox);
        var portDesc = MakeDescription("Common values: 587 (STARTTLS) or 465 (implicit SSL). Most providers want SSL/TLS on.");

        _smtpFromBox = MakeTextBox(_config.SmtpFromAddress);
        _smtpFromBox.PlaceholderText = "alerts@yourdomain.com";
        var fromRow = MakeRowFromControls(MakeFieldLabel("From address:"), _smtpFromBox);
        var fromDesc = MakeDescription("Address that appears as the sender. For most providers this must match the username.");

        _smtpUserBox = MakeTextBox(_config.SmtpUsername);
        var userRow = MakeRowFromControls(MakeFieldLabel("Username:"), _smtpUserBox);

        _smtpPasswordBox = MakeTextBox(Config.UnprotectPassword(_config.SmtpPasswordEncrypted));
        _smtpPasswordBox.UseSystemPasswordChar = true;
        var passRow = MakeRowFromControls(MakeFieldLabel("Password:"), _smtpPasswordBox);
        var passDesc = MakeDescription(
            "Stored encrypted with Windows DPAPI in your user profile. Gmail requires a 16-character app password (with 2FA enabled).");

        _smtpSection = BuildSection("SMTP server",
            hostRow, hostDesc,
            Spacer(6),
            portRowPanel, portDesc,
            Spacer(6),
            fromRow, fromDesc,
            Spacer(6),
            userRow,
            passRow, passDesc);
        _flow.Controls.Add(_smtpSection);

        // ===== Pushover =====
        var pushoverIntro = MakeDescription(
            "Pushover delivers a silent, discreet push notification to your phone — no SMS carrier hops, no inbox spam.");

        var step1 = MakeFlow(
            MakeBodyLabel("1. Sign up & install the app:"),
            MakeLink("pushover.net", PushoverHomeUrl));
        var step2 = MakeBodyLabel("2. Copy your User Key from the dashboard.");
        var step3 = MakeFlow(
            MakeBodyLabel("3. Create an Application/API token:"),
            MakeLink("pushover.net/apps/build", PushoverAppBuildUrl));

        _pushoverUserKeyBox = MakeTextBox(_config.PushoverUserKey);
        var userKeyRow = MakeRowFromControls(MakeFieldLabel("User Key:"), _pushoverUserKeyBox);

        _pushoverTokenBox = MakeTextBox(Config.UnprotectPassword(_config.PushoverApiTokenEncrypted));
        _pushoverTokenBox.UseSystemPasswordChar = true;
        var tokenRow = MakeRowFromControls(MakeFieldLabel("API Token:"), _pushoverTokenBox);
        var tokenDesc = MakeDescription("Stored encrypted with Windows DPAPI in your user profile.");

        _pushoverSection = BuildSection("Pushover",
            pushoverIntro,
            Spacer(4),
            step1, step2, step3,
            Spacer(6),
            userKeyRow,
            tokenRow, tokenDesc);
        _flow.Controls.Add(_pushoverSection);

        // ===== Diagnostics =====
        var viewNotificationsBtn = MakeSecondaryButton("View notifications…", 200);
        viewNotificationsBtn.Click += (_, _) => _actions.ViewNotifications?.Invoke();
        var viewNotificationsDesc = MakeDescription(
            "Open the log of every notification WPUService has detected (Teams and others). Useful for confirming detection is working.");

        var statusBtn = MakeSecondaryButton("Show status…", 200);
        statusBtn.Click += (_, _) => _actions.ShowStatus?.Invoke();
        var statusDesc = MakeDescription(
            "Show internal counters: notification access state, idle time, last Teams notification seen, current pause status. Useful for debugging.");

        var requestAccessBtn = MakeSecondaryButton("Request notification access", 220);
        requestAccessBtn.Click += async (_, _) =>
        {
            if (_actions.RequestNotificationAccess != null) await _actions.RequestNotificationAccess();
        };
        var requestAccessDesc = MakeDescription(
            "Ask Windows to grant WPUService access to notification history. Run this if Teams notifications aren't being detected.");

        var simulateBtn = MakeSecondaryButton("Simulate Teams notification", 220);
        simulateBtn.Click += (_, _) => _actions.SimulateTeamsNotification?.Invoke();
        _simulateStatusIcon = new PictureBox
        {
            Width = 24,
            Height = 24,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = BgElevated,
            Margin = new Padding(10, 3, 0, 0),
        };
        var simulateRow = MakeFlow(simulateBtn, _simulateStatusIcon);
        var simulateDesc = MakeDescription(
            "Inject a fake Teams notification to test the full pause + alert flow without waiting for a real one. " +
            "Watch the icon to the right: it turns gray while paused, then back to color once you move the mouse or the alert fires.");

        var sendNowBtn = MakeSecondaryButton("Send test alert now", 200);
        sendNowBtn.Click += async (_, _) =>
        {
            if (_actions.SendTestAlertNow != null) await _actions.SendTestAlertNow();
        };
        var sendNowDesc = MakeDescription(
            "Send an alert immediately using the saved settings, bypassing the pause and delay. Confirms your delivery configuration end-to-end.");

        _flow.Controls.Add(BuildSection("Diagnostics",
            viewNotificationsBtn, viewNotificationsDesc,
            Spacer(8),
            statusBtn, statusDesc,
            Spacer(8),
            requestAccessBtn, requestAccessDesc,
            Spacer(8),
            simulateRow, simulateDesc,
            Spacer(8),
            sendNowBtn, sendNowDesc));

        // ===== Danger zone =====
        var uninstallBtn = MakeDangerButton("Uninstall WPUService…", 220);
        uninstallBtn.Click += (_, _) =>
        {
            // Close the settings window first so it isn't orphaned when the
            // tray exits as part of uninstall.
            DialogResult = DialogResult.Cancel;
            Close();
            _actions.Uninstall?.Invoke();
        };
        var uninstallDesc = MakeDescription(
            "Stop the utility, remove its autostart entry, and delete all of its files and saved settings. This cannot be undone.");

        _flow.Controls.Add(BuildSection("Danger zone",
            uninstallBtn, uninstallDesc));

        // ===== Buttons =====
        var buttonPanel = new Panel
        {
            Width = ContentWidth,
            Height = 44,
            BackColor = Bg,
            Margin = new Padding(0, 4, 0, 0),
        };
        var testButton = MakeSecondaryButton("Send Test", 110);
        testButton.Location = new Point(0, 8);
        testButton.Click += async (_, _) => await OnTestClickedAsync();
        var saveButton = MakePrimaryButton("Save", 100);
        saveButton.Location = new Point(ContentWidth - 220, 8);
        saveButton.DialogResult = DialogResult.OK;
        saveButton.Click += (_, _) => OnSaveClicked();
        var cancelButton = MakeSecondaryButton("Cancel", 100);
        cancelButton.Location = new Point(ContentWidth - 110, 8);
        cancelButton.DialogResult = DialogResult.Cancel;
        buttonPanel.Controls.Add(testButton);
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(cancelButton);
        _flow.Controls.Add(buttonPanel);
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        switch (_config.SendMode)
        {
            case SendMode.Pushover: _pushoverRadio.Checked = true; break;
            case SendMode.Smtp: _smtpRadio.Checked = true; break;
            default:
                if (_outlookAvailable) _outlookRadio.Checked = true;
                else _smtpRadio.Checked = true;
                break;
        }

        _initialized = true;
        UpdateRecipientFields();
        UpdateCustomGatewayVisibility();
        Relayout();

        _iconPollTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _iconPollTimer.Tick += (_, _) => UpdateLiveIcon();
        _iconPollTimer.Start();
        UpdateLiveIcon();
    }

    private void UpdateLiveIcon()
    {
        if (_simulateStatusIcon == null || _actions.IsActive == null) return;
        var active = _actions.IsActive();
        if (_lastActiveState == active) return;
        _lastActiveState = active;
        var icon = active ? _actions.ActiveIcon : _actions.InactiveIcon;
        if (icon == null) return;
        var newImage = icon.ToBitmap();
        var oldImage = _simulateStatusIcon.Image;
        _simulateStatusIcon.Image = newImage;
        oldImage?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _iconPollTimer?.Stop();
            _iconPollTimer?.Dispose();
            _iconPollTimer = null;
            if (_simulateStatusIcon?.Image != null)
            {
                var img = _simulateStatusIcon.Image;
                _simulateStatusIcon.Image = null;
                img.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    // ----- Section builder & helpers -----

    private Panel BuildSection(string title, params Control[] body)
    {
        var section = new FlowLayoutPanel
        {
            Width = ContentWidth,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BgElevated,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(14, 12, 14, 14),
        };
        section.Paint += DrawSectionBorder;

        var header = new Label
        {
            Text = title.ToUpperInvariant(),
            Font = new Font("Segoe UI Semibold", 8.25f, FontStyle.Bold),
            ForeColor = TextDim,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
        };
        section.Controls.Add(header);

        foreach (var c in body) section.Controls.Add(c);

        return section;
    }

    private static void DrawSectionBorder(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel p) return;
        using var pen = new Pen(BorderColor, 1f);
        var r = new Rectangle(0, 0, p.Width - 1, p.Height - 1);
        using var path = RoundedRect(r, 6);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(pen, path);
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private CheckBox MakeCheckBox(string text, bool isChecked)
    {
        return new CheckBox
        {
            Text = text,
            AutoSize = true,
            Checked = isChecked,
            ForeColor = TextColor,
            BackColor = BgElevated,
            Margin = new Padding(0, 0, 0, 0),
        };
    }

    private RadioButton MakeRadio(string text)
    {
        return new RadioButton
        {
            Text = text,
            AutoSize = true,
            ForeColor = TextColor,
            BackColor = BgElevated,
            Margin = new Padding(0, 0, 0, 0),
        };
    }

    private Label MakeFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = TextColor,
            BackColor = BgElevated,
        };
    }

    private Label MakeBodyLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = TextColor,
            BackColor = BgElevated,
            Margin = new Padding(0, 0, 6, 0),
        };
    }

    private Label MakeDescription(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth - 28, 0),
            ForeColor = TextDim,
            BackColor = BgElevated,
            Margin = new Padding(0, 2, 0, 4),
        };
    }

    private LinkLabel MakeLink(string text, string url)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            BackColor = BgElevated,
            LinkColor = Accent,
            ActiveLinkColor = Accent,
            VisitedLinkColor = Accent,
            Margin = new Padding(0, 0, 6, 0),
        };
        link.LinkClicked += (_, _) => OpenUrl(url);
        return link;
    }

    private TextBox MakeTextBox(string text)
    {
        return new TextBox
        {
            Text = text,
            Width = InputWidth,
            BackColor = BgInput,
            ForeColor = TextColor,
            BorderStyle = BorderStyle.FixedSingle,
        };
    }

    private ComboBox MakeCombo(int width)
    {
        return new ComboBox
        {
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = BgInput,
            ForeColor = TextColor,
            FlatStyle = FlatStyle.Flat,
        };
    }

    private Panel MakeFieldRow(string labelText, Control input)
    {
        return MakeRowFromControls(MakeFieldLabel(labelText), input);
    }

    private Panel MakeRowFromControls(Label label, Control input)
    {
        var row = new Panel
        {
            Width = ContentWidth - 24,
            Height = 28,
            BackColor = BgElevated,
            Margin = new Padding(0, 0, 0, 0),
        };
        label.Location = new Point(0, 6);
        input.Location = new Point(InputLeft, 3);
        row.Controls.Add(label);
        row.Controls.Add(input);
        return row;
    }

    private FlowLayoutPanel MakeFlow(params Control[] children)
    {
        var f = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = BgElevated,
            Margin = new Padding(0, 0, 0, 2),
        };
        foreach (var c in children) f.Controls.Add(c);
        return f;
    }

    private Panel Spacer(int height)
    {
        return new Panel
        {
            Width = ContentWidth - 24,
            Height = height,
            BackColor = BgElevated,
            Margin = new Padding(0),
        };
    }

    private Button MakePrimaryButton(string text, int width)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.FromArgb(26, 26, 28),
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderColor = Accent;
        b.FlatAppearance.BorderSize = 1;
        return b;
    }

    private Button MakeSecondaryButton(string text, int width)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = BgElevated,
            ForeColor = TextColor,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderColor = BorderColor;
        b.FlatAppearance.BorderSize = 1;
        return b;
    }

    private Button MakeDangerButton(string text, int width)
    {
        var b = new Button
        {
            Text = text,
            Width = width,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = BgElevated,
            ForeColor = Color.FromArgb(239, 107, 107),
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderColor = Color.FromArgb(239, 107, 107);
        b.FlatAppearance.BorderSize = 1;
        return b;
    }

    private static int FindPresetIndex((string, int Seconds)[] presets, int seconds)
    {
        for (int i = 0; i < presets.Length; i++)
            if (presets[i].Seconds == seconds) return i;
        // Fall back to nearest (default 5min if value isn't in presets)
        return presets.Length / 2;
    }

    // ----- Existing logic, updated for new control set -----

    private SendMode SelectedSendMode =>
        _pushoverRadio.Checked ? SendMode.Pushover :
        _smtpRadio.Checked ? SendMode.Smtp :
        SendMode.Outlook;

    private RecipientMode SelectedRecipientMode =>
        _recipientSmsRadio.Checked ? RecipientMode.Sms : RecipientMode.Email;

    private void Relayout()
    {
        if (!_initialized) return;
        var sendMode = SelectedSendMode;
        _recipientSection.Visible = sendMode != SendMode.Pushover;
        _smtpSection.Visible = sendMode == SendMode.Smtp;
        _pushoverSection.Visible = sendMode == SendMode.Pushover;
    }

    private void UpdateRecipientFields()
    {
        if (!_initialized) return;
        var sms = SelectedRecipientMode == RecipientMode.Sms;
        _emailLabel.Visible = !sms;
        _emailBox.Visible = !sms;
        _emailHelpPanel.Visible = !sms;
        _phoneLabel.Visible = sms;
        _phoneBox.Visible = sms;
        _carrierLabel.Visible = sms;
        _carrierBox.Visible = sms;
        _smsHint.Visible = sms;
        UpdateCustomGatewayVisibility();
    }

    private void SelectCarrier(string key)
    {
        for (int i = 0; i < Carriers.All.Length; i++)
        {
            if (string.Equals(Carriers.All[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                _carrierBox.SelectedIndex = i;
                return;
            }
        }
        if (_carrierBox.Items.Count > 0) _carrierBox.SelectedIndex = 0;
    }

    private string SelectedCarrierKey =>
        _carrierBox.SelectedIndex >= 0 ? Carriers.All[_carrierBox.SelectedIndex].Key : "";

    private void UpdateCustomGatewayVisibility()
    {
        if (!_initialized) return;
        var sms = SelectedRecipientMode == RecipientMode.Sms;
        var isCustom = sms && string.Equals(SelectedCarrierKey, Carriers.CustomKey, StringComparison.OrdinalIgnoreCase);
        _customGatewayLabel.Visible = isCustom;
        _customGatewayBox.Visible = isCustom;
    }

    private string BuildEmailRecipient()
    {
        if (SelectedRecipientMode == RecipientMode.Email)
            return _emailBox.Text.Trim();
        return Carriers.BuildRecipient(_phoneBox.Text, SelectedCarrierKey, _customGatewayBox.Text);
    }

    private void OnSaveClicked()
    {
        _config.Enabled = _enabledBox.Checked;
        var wantAutostart = _autostartBox.Checked;
        if (wantAutostart != _config.StartWithWindows)
        {
            if (wantAutostart) Autostart.Enable();
            else Autostart.Disable();
        }
        _config.StartWithWindows = wantAutostart;

        _config.PauseOnTeamsCall = _pauseOnCallBox.Checked;
        _config.TeamsFilter = _filterCombo.SelectedIndex == 1 ? TeamsFilterMode.CallsOnly : TeamsFilterMode.Any;
        _config.IdleThresholdSeconds = IdlePresets[Math.Max(0, _idleCombo.SelectedIndex)].Seconds;
        _config.AlertDelaySeconds = AlertDelayPresets[Math.Max(0, _delayCombo.SelectedIndex)].Seconds;

        _config.SendMode = SelectedSendMode;
        _config.RecipientMode = SelectedRecipientMode;
        _config.RecipientEmail = _emailBox.Text.Trim();
        _config.PhoneNumber = _phoneBox.Text.Trim();
        _config.CarrierKey = SelectedCarrierKey;
        _config.CustomGateway = _customGatewayBox.Text.Trim();
        _config.SmtpHost = _smtpHostBox.Text.Trim();
        _config.SmtpPort = (int)_smtpPortBox.Value;
        _config.SmtpUseSsl = _smtpSslBox.Checked;
        _config.SmtpFromAddress = _smtpFromBox.Text.Trim();
        _config.SmtpUsername = _smtpUserBox.Text.Trim();
        _config.SmtpPasswordEncrypted = Config.ProtectPassword(_smtpPasswordBox.Text);
        _config.PushoverUserKey = _pushoverUserKeyBox.Text.Trim();
        _config.PushoverApiTokenEncrypted = Config.ProtectPassword(_pushoverTokenBox.Text);
        _config.Save();
    }

    private async Task OnTestClickedAsync()
    {
        Cursor = Cursors.WaitCursor;
        try
        {
            (bool ok, string err) result;
            switch (SelectedSendMode)
            {
                case SendMode.Pushover:
                    var pushSettings = new PushoverSettings
                    {
                        UserKey = _pushoverUserKeyBox.Text.Trim(),
                        ApiToken = _pushoverTokenBox.Text.Trim(),
                    };
                    if (!pushSettings.IsValid)
                    {
                        MessageBox.Show(this, "Fill in your Pushover User Key and API Token first.",
                            "Send Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    result = await PushoverSender.SendAsync(pushSettings, "WPUService test",
                        "WPUService test message. If you can read this, alerts are working.");
                    break;

                case SendMode.Outlook:
                {
                    var to = BuildEmailRecipient();
                    if (string.IsNullOrEmpty(to))
                    {
                        MessageBox.Show(this, "Fill in recipient details first.",
                            "Send Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    result = await OutlookSender.SendAsync(to, "WPUService test",
                        "WPUService test message. If you can read this, alerts are working.");
                    break;
                }

                case SendMode.Smtp:
                default:
                {
                    var to = BuildEmailRecipient();
                    if (string.IsNullOrEmpty(to))
                    {
                        MessageBox.Show(this, "Fill in recipient details first.",
                            "Send Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    var settings = BuildSmtpSettingsFromForm(to);
                    if (!settings.IsValid)
                    {
                        MessageBox.Show(this, "Fill in all SMTP fields before sending a test.",
                            "Send Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    result = await EmailSender.SendAsync(settings, "WPUService test",
                        "WPUService test message. If you can read this, alerts are working.");
                    break;
                }
            }

            if (result.ok)
                MessageBox.Show(this, "Test message sent.", "Send Test",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show(this, "Failed to send: " + result.err, "Send Test",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private EmailSettings BuildSmtpSettingsFromForm(string to)
    {
        return new EmailSettings
        {
            Host = _smtpHostBox.Text.Trim(),
            Port = (int)_smtpPortBox.Value,
            UseSsl = _smtpSslBox.Checked,
            Username = _smtpUserBox.Text.Trim(),
            Password = _smtpPasswordBox.Text,
            From = _smtpFromBox.Text.Trim(),
            To = to,
        };
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch { }
    }
}

internal sealed class SettingsActions
{
    public Action? ViewNotifications { get; init; }
    public Action? ShowStatus { get; init; }
    public Func<Task>? RequestNotificationAccess { get; init; }
    public Action? SimulateTeamsNotification { get; init; }
    public Func<Task>? SendTestAlertNow { get; init; }
    public Action? Uninstall { get; init; }
    public Func<bool>? IsActive { get; init; }
    public Icon? ActiveIcon { get; init; }
    public Icon? InactiveIcon { get; init; }
}
