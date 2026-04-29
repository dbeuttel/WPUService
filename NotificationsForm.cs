using System.Diagnostics;

namespace WPUService;

internal sealed class NotificationsForm : Form
{
    private readonly NotificationLog _log;
    private readonly ListView _list;
    private readonly Label _countLabel;
    private readonly TextBox _filterBox;
    private readonly CheckBox _teamsOnlyBox;

    public NotificationsForm(NotificationLog log)
    {
        _log = log;

        Text = "Captured notifications";
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        ClientSize = new Size(900, 520);
        MinimumSize = new Size(640, 320);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8) };
        Controls.Add(topPanel);

        _countLabel = new Label
        {
            Location = new Point(8, 12),
            AutoSize = true,
        };
        topPanel.Controls.Add(_countLabel);

        var filterLabel = new Label { Text = "Filter:", Location = new Point(180, 12), AutoSize = true };
        topPanel.Controls.Add(filterLabel);

        _filterBox = new TextBox
        {
            Location = new Point(220, 9),
            Width = 200,
            PlaceholderText = "search title / body / app",
        };
        _filterBox.TextChanged += (_, _) => Reload();
        topPanel.Controls.Add(_filterBox);

        _teamsOnlyBox = new CheckBox
        {
            Text = "Teams only",
            Location = new Point(430, 11),
            AutoSize = true,
        };
        _teamsOnlyBox.CheckedChanged += (_, _) => Reload();
        topPanel.Controls.Add(_teamsOnlyBox);

        var refreshBtn = new Button { Text = "Refresh", Location = new Point(540, 7), Width = 80 };
        refreshBtn.Click += (_, _) => Reload();
        topPanel.Controls.Add(refreshBtn);

        var clearBtn = new Button { Text = "Clear", Location = new Point(630, 7), Width = 80 };
        clearBtn.Click += (_, _) =>
        {
            if (MessageBox.Show(this,
                "Clear all captured notifications? This deletes the log file.",
                "Confirm clear", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                _log.Clear();
                Reload();
            }
        };
        topPanel.Controls.Add(clearBtn);

        var openBtn = new Button { Text = "Open file location", Location = new Point(720, 7), Width = 150 };
        openBtn.Click += (_, _) => OpenFileLocation();
        topPanel.Controls.Add(openBtn);

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HideSelection = false,
        };
        _list.Columns.Add("Time", 130);
        _list.Columns.Add("App", 180);
        _list.Columns.Add("Title", 200);
        _list.Columns.Add("Body", 360);
        _list.Columns.Add("Teams", 50);
        Controls.Add(_list);

        // ListView fills before TopPanel in z-order; ensure topPanel is on top
        _list.SendToBack();

        Reload();
    }

    private void Reload()
    {
        var snapshot = _log.Snapshot();
        var filter = _filterBox.Text.Trim();
        var teamsOnly = _teamsOnlyBox.Checked;

        var rows = snapshot
            .Where(e => !teamsOnly || e.IsTeams)
            .Where(e => string.IsNullOrEmpty(filter)
                || (e.Title?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || (e.Body?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || (e.AppName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(e => e.Timestamp)
            .ToList();

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var e in rows)
        {
            var item = new ListViewItem(e.Timestamp.ToString("g"));
            item.SubItems.Add(e.AppName);
            item.SubItems.Add(e.Title);
            item.SubItems.Add(e.Body?.Replace("\n", "  ") ?? "");
            item.SubItems.Add(e.IsTeams ? "yes" : "");
            _list.Items.Add(item);
        }
        _list.EndUpdate();

        _countLabel.Text = $"{rows.Count} of {snapshot.Count} entries";
    }

    private void OpenFileLocation()
    {
        try
        {
            var path = NotificationLog.LogPath;
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true,
                });
            }
            else
            {
                var dir = Path.GetDirectoryName(path)!;
                Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open: " + ex.Message, "WPUService",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
