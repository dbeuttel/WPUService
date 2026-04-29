namespace WPUService;

internal sealed class SettingsForm : Form
{
    private const string IPhoneVipUrl = "https://support.apple.com/en-us/104971";
    private const string AndroidPriorityUrl = "https://support.google.com/mail/answer/1075549?hl=en&co=GENIE.Platform%3DAndroid";
    private const string PushoverHomeUrl = "https://pushover.net/";
    private const string PushoverAppBuildUrl = "https://pushover.net/apps/build";

    private const int FormWidth = 480;

    private readonly Config _config;
    private readonly bool _outlookAvailable;

    private readonly FlowLayoutPanel _flow;

    private readonly GroupBox _transportGroup;
    private readonly RadioButton _outlookRadio;
    private readonly RadioButton _smtpRadio;
    private readonly RadioButton _pushoverRadio;

    private readonly GroupBox _recipientGroup;
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

    private readonly GroupBox _smtpGroup;
    private readonly TextBox _smtpHostBox;
    private readonly NumericUpDown _smtpPortBox;
    private readonly CheckBox _smtpSslBox;
    private readonly TextBox _smtpFromBox;
    private readonly TextBox _smtpUserBox;
    private readonly TextBox _smtpPasswordBox;

    private readonly GroupBox _pushoverGroup;
    private readonly TextBox _pushoverUserKeyBox;
    private readonly TextBox _pushoverTokenBox;

    private readonly Panel _buttonPanel;
    private readonly Button _testButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    private bool _initialized;

    public SettingsForm(Config config)
    {
        _config = config;
        _outlookAvailable = OutlookSender.IsAvailable();

        Text = "WPUService Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.None;

        _flow = new FlowLayoutPanel
        {
            Location = new Point(12, 12),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        Controls.Add(_flow);

        // ===== Send via group =====
        var transportHeight = _outlookAvailable ? 100 : 76;
        _transportGroup = new GroupBox
        {
            Text = "Send via",
            Size = new Size(FormWidth - 32, transportHeight),
            Margin = new Padding(0, 0, 0, 8),
        };

        int radioY = 22;
        _outlookRadio = new RadioButton
        {
            Text = "Outlook (logged-in profile)",
            Location = new Point(12, radioY),
            AutoSize = true,
            Enabled = _outlookAvailable,
            Visible = _outlookAvailable,
        };
        _outlookRadio.CheckedChanged += (_, _) => Relayout();
        _transportGroup.Controls.Add(_outlookRadio);
        if (_outlookAvailable) radioY += 24;

        _smtpRadio = new RadioButton
        {
            Text = "SMTP server",
            Location = new Point(12, radioY),
            AutoSize = true,
        };
        _smtpRadio.CheckedChanged += (_, _) => Relayout();
        _transportGroup.Controls.Add(_smtpRadio);
        radioY += 24;

        _pushoverRadio = new RadioButton
        {
            Text = "Pushover (mobile push, discreet)",
            Location = new Point(12, radioY),
            AutoSize = true,
        };
        _pushoverRadio.CheckedChanged += (_, _) => Relayout();
        _transportGroup.Controls.Add(_pushoverRadio);

        _flow.Controls.Add(_transportGroup);

        // ===== Recipient group =====
        _recipientGroup = new GroupBox
        {
            Text = "Recipient",
            Size = new Size(FormWidth - 32, 200),
            Margin = new Padding(0, 0, 0, 8),
        };

        _recipientEmailRadio = new RadioButton
        {
            Text = "Email address",
            Location = new Point(12, 22),
            AutoSize = true,
            Checked = _config.RecipientMode == RecipientMode.Email,
        };
        _recipientEmailRadio.CheckedChanged += (_, _) => UpdateRecipientFields();
        _recipientGroup.Controls.Add(_recipientEmailRadio);

        _recipientSmsRadio = new RadioButton
        {
            Text = "SMS via carrier gateway",
            Location = new Point(160, 22),
            AutoSize = true,
            Checked = _config.RecipientMode == RecipientMode.Sms,
        };
        _recipientSmsRadio.CheckedChanged += (_, _) => UpdateRecipientFields();
        _recipientGroup.Controls.Add(_recipientSmsRadio);

        _emailLabel = new Label { Text = "Email address:", Location = new Point(12, 56), AutoSize = true };
        _emailBox = new TextBox
        {
            Location = new Point(130, 53),
            Width = 300,
            Text = _config.RecipientEmail,
            PlaceholderText = "you@example.com",
        };
        _recipientGroup.Controls.Add(_emailLabel);
        _recipientGroup.Controls.Add(_emailBox);

        _emailHelpPanel = new Panel { Location = new Point(8, 84), Size = new Size(440, 24) };
        _recipientGroup.Controls.Add(_emailHelpPanel);

        var helpLabel = new Label
        {
            Text = "Make alerts pop on your phone:",
            Location = new Point(4, 4),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        };
        _emailHelpPanel.Controls.Add(helpLabel);

        var iphoneLink = new LinkLabel { Text = "iPhone (VIP)", Location = new Point(190, 4), AutoSize = true };
        iphoneLink.LinkClicked += (_, _) => OpenUrl(IPhoneVipUrl);
        _emailHelpPanel.Controls.Add(iphoneLink);

        var sepLabel = new Label
        {
            Text = "|",
            Location = new Point(265, 4),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        };
        _emailHelpPanel.Controls.Add(sepLabel);

        var androidLink = new LinkLabel { Text = "Android (Gmail)", Location = new Point(275, 4), AutoSize = true };
        androidLink.LinkClicked += (_, _) => OpenUrl(AndroidPriorityUrl);
        _emailHelpPanel.Controls.Add(androidLink);

        _phoneLabel = new Label { Text = "Phone number:", Location = new Point(12, 56), AutoSize = true };
        _phoneBox = new TextBox { Location = new Point(130, 53), Width = 300, Text = _config.PhoneNumber };
        _recipientGroup.Controls.Add(_phoneLabel);
        _recipientGroup.Controls.Add(_phoneBox);

        _carrierLabel = new Label { Text = "Carrier:", Location = new Point(12, 88), AutoSize = true };
        _carrierBox = new ComboBox
        {
            Location = new Point(130, 85),
            Width = 300,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        foreach (var c in Carriers.All)
            _carrierBox.Items.Add(c.DisplayName);
        SelectCarrier(_config.CarrierKey);
        _carrierBox.SelectedIndexChanged += (_, _) => UpdateCustomGatewayVisibility();
        _recipientGroup.Controls.Add(_carrierLabel);
        _recipientGroup.Controls.Add(_carrierBox);

        _customGatewayLabel = new Label { Text = "Custom gateway:", Location = new Point(12, 120), AutoSize = true };
        _customGatewayBox = new TextBox
        {
            Location = new Point(130, 117),
            Width = 300,
            Text = _config.CustomGateway,
            PlaceholderText = "e.g. tmomail.net",
        };
        _recipientGroup.Controls.Add(_customGatewayLabel);
        _recipientGroup.Controls.Add(_customGatewayBox);

        _smsHint = new Label
        {
            Text = "Note: carrier gateways may delay or silently drop messages.",
            Location = new Point(12, 152),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        };
        _recipientGroup.Controls.Add(_smsHint);

        _flow.Controls.Add(_recipientGroup);

        // ===== SMTP group =====
        _smtpGroup = new GroupBox
        {
            Text = "SMTP",
            Size = new Size(FormWidth - 32, 230),
            Margin = new Padding(0, 0, 0, 8),
        };

        var hostLabel = new Label { Text = "Server host:", Location = new Point(12, 28), AutoSize = true };
        _smtpHostBox = new TextBox { Location = new Point(130, 25), Width = 300, Text = _config.SmtpHost, PlaceholderText = "smtp.gmail.com" };
        _smtpGroup.Controls.Add(hostLabel);
        _smtpGroup.Controls.Add(_smtpHostBox);

        var portLabel = new Label { Text = "Port:", Location = new Point(12, 60), AutoSize = true };
        _smtpPortBox = new NumericUpDown
        {
            Location = new Point(130, 57),
            Width = 80,
            Minimum = 1,
            Maximum = 65535,
            Value = Math.Clamp(_config.SmtpPort, 1, 65535),
        };
        _smtpSslBox = new CheckBox
        {
            Text = "Use SSL/TLS",
            Location = new Point(230, 58),
            AutoSize = true,
            Checked = _config.SmtpUseSsl,
        };
        _smtpGroup.Controls.Add(portLabel);
        _smtpGroup.Controls.Add(_smtpPortBox);
        _smtpGroup.Controls.Add(_smtpSslBox);

        var fromLabel = new Label { Text = "From address:", Location = new Point(12, 92), AutoSize = true };
        _smtpFromBox = new TextBox { Location = new Point(130, 89), Width = 300, Text = _config.SmtpFromAddress };
        _smtpGroup.Controls.Add(fromLabel);
        _smtpGroup.Controls.Add(_smtpFromBox);

        var userLabel = new Label { Text = "Username:", Location = new Point(12, 124), AutoSize = true };
        _smtpUserBox = new TextBox { Location = new Point(130, 121), Width = 300, Text = _config.SmtpUsername };
        _smtpGroup.Controls.Add(userLabel);
        _smtpGroup.Controls.Add(_smtpUserBox);

        var passLabel = new Label { Text = "Password:", Location = new Point(12, 156), AutoSize = true };
        _smtpPasswordBox = new TextBox
        {
            Location = new Point(130, 153),
            Width = 300,
            UseSystemPasswordChar = true,
            Text = Config.UnprotectPassword(_config.SmtpPasswordEncrypted),
        };
        _smtpGroup.Controls.Add(passLabel);
        _smtpGroup.Controls.Add(_smtpPasswordBox);

        var smtpHint = new Label
        {
            Text = "Tip: Gmail requires a 16-character app password (with 2FA enabled).",
            Location = new Point(12, 188),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
        };
        _smtpGroup.Controls.Add(smtpHint);

        _flow.Controls.Add(_smtpGroup);

        // ===== Pushover group =====
        _pushoverGroup = new GroupBox
        {
            Text = "Pushover",
            Size = new Size(FormWidth - 32, 200),
            Margin = new Padding(0, 0, 0, 8),
        };

        var setupLabel = new Label { Text = "Setup steps:", Location = new Point(12, 22), AutoSize = true };
        _pushoverGroup.Controls.Add(setupLabel);

        var step1Label = new Label
        {
            Text = "1. Sign up & install the Pushover app:",
            Location = new Point(12, 44),
            AutoSize = true,
        };
        _pushoverGroup.Controls.Add(step1Label);
        var signupLink = new LinkLabel { Text = "pushover.net", Location = new Point(232, 44), AutoSize = true };
        signupLink.LinkClicked += (_, _) => OpenUrl(PushoverHomeUrl);
        _pushoverGroup.Controls.Add(signupLink);

        var step2Label = new Label
        {
            Text = "2. Copy your User Key from the dashboard.",
            Location = new Point(12, 64),
            AutoSize = true,
        };
        _pushoverGroup.Controls.Add(step2Label);

        var step3Label = new Label
        {
            Text = "3. Create an Application/API token:",
            Location = new Point(12, 84),
            AutoSize = true,
        };
        _pushoverGroup.Controls.Add(step3Label);
        var tokenLink = new LinkLabel { Text = "pushover.net/apps/build", Location = new Point(218, 84), AutoSize = true };
        tokenLink.LinkClicked += (_, _) => OpenUrl(PushoverAppBuildUrl);
        _pushoverGroup.Controls.Add(tokenLink);

        var userKeyLabel = new Label { Text = "User Key:", Location = new Point(12, 120), AutoSize = true };
        _pushoverUserKeyBox = new TextBox
        {
            Location = new Point(130, 117),
            Width = 300,
            Text = _config.PushoverUserKey,
        };
        _pushoverGroup.Controls.Add(userKeyLabel);
        _pushoverGroup.Controls.Add(_pushoverUserKeyBox);

        var tokenLabel = new Label { Text = "API Token:", Location = new Point(12, 152), AutoSize = true };
        _pushoverTokenBox = new TextBox
        {
            Location = new Point(130, 149),
            Width = 300,
            UseSystemPasswordChar = true,
            Text = Config.UnprotectPassword(_config.PushoverApiTokenEncrypted),
        };
        _pushoverGroup.Controls.Add(tokenLabel);
        _pushoverGroup.Controls.Add(_pushoverTokenBox);

        _flow.Controls.Add(_pushoverGroup);

        // ===== Buttons =====
        _buttonPanel = new Panel
        {
            Size = new Size(FormWidth - 32, 36),
            Margin = new Padding(0, 0, 0, 0),
        };

        _testButton = new Button { Text = "Send Test", Location = new Point(0, 6), Width = 100 };
        _testButton.Click += async (_, _) => await OnTestClickedAsync();
        _buttonPanel.Controls.Add(_testButton);

        _saveButton = new Button { Text = "Save", Location = new Point(FormWidth - 32 - 200, 6), Width = 90, DialogResult = DialogResult.OK };
        _saveButton.Click += (_, _) => OnSaveClicked();
        _buttonPanel.Controls.Add(_saveButton);

        _cancelButton = new Button { Text = "Cancel", Location = new Point(FormWidth - 32 - 100, 6), Width = 90, DialogResult = DialogResult.Cancel };
        _buttonPanel.Controls.Add(_cancelButton);

        _flow.Controls.Add(_buttonPanel);

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        // Set initial radio state AFTER all controls exist
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
    }

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
        _recipientGroup.Visible = sendMode != SendMode.Pushover;
        _smtpGroup.Visible = sendMode == SendMode.Smtp;
        _pushoverGroup.Visible = sendMode == SendMode.Pushover;
        ResizeFormToFlow();
    }

    private void ResizeFormToFlow()
    {
        // Force the flow panel to recalc its size based on visible children
        _flow.PerformLayout();
        ClientSize = new Size(_flow.Right + 12, _flow.Bottom + 12);
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
