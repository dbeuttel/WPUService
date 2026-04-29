namespace WPUService;

internal static class Program
{
    private const string AppTitle = "Workstation Presence Utility";

    [STAThread]
    private static int Main(string[] args)
    {
        try { Installer.EnsureAumidRegistration(); } catch { }
        try { NativeMethods.SetCurrentProcessExplicitAppUserModelID(AppIdentity.Aumid); } catch { }
        ApplicationConfiguration.Initialize();

        if (args.Length > 0)
        {
            var cmd = args[0].TrimStart('/', '-').ToLowerInvariant();
            if (cmd is "install")
            {
                var ok = Installer.Install();
                MessageBox.Show(
                    ok ? $"{AppTitle} installed.\n\nIt is now running in your system tray and will start automatically with Windows."
                       : $"{AppTitle} could not be installed.",
                    AppTitle,
                    MessageBoxButtons.OK,
                    ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                return ok ? 0 : 1;
            }
            if (cmd is "uninstall")
            {
                var ok = Installer.Uninstall();
                MessageBox.Show(
                    ok ? $"{AppTitle} has been removed from this computer."
                       : $"{AppTitle} could not be fully removed.",
                    AppTitle,
                    MessageBoxButtons.OK,
                    ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                return ok ? 0 : 1;
            }
        }

        if (!Installer.IsRunningFromInstallLocation())
        {
            var choice = MessageBox.Show(
                $"Install {AppTitle} to your user profile so it starts with Windows?" +
                "\n\nYes  – Install and start" +
                "\nNo   – Run once from the current location" +
                "\nCancel – Exit",
                AppTitle,
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (choice == DialogResult.Cancel) return 0;
            if (choice == DialogResult.Yes)
            {
                var ok = Installer.Install();
                MessageBox.Show(
                    ok ? $"{AppTitle} installed and started in your system tray."
                       : $"{AppTitle} could not be installed.",
                    AppTitle,
                    MessageBoxButtons.OK,
                    ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                return ok ? 0 : 1;
            }
        }

        using var mutex = new Mutex(initiallyOwned: true, name: @"Global\WPUService.SingleInstance", out bool createdNew);
        if (!createdNew) return 0;

        Application.Run(new TrayContext());
        return 0;
    }
}
