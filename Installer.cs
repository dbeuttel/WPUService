using System.Diagnostics;
using Microsoft.Win32;

namespace WPUService;

internal static class Installer
{
    public static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WPUService");

    public static string InstalledExePath => Path.Combine(InstallDir, "WPUService.exe");

    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WPUService");

    public static bool IsRunningFromInstallLocation()
    {
        var current = Environment.ProcessPath;
        if (string.IsNullOrEmpty(current)) return false;
        try
        {
            return string.Equals(
                Path.GetFullPath(current),
                Path.GetFullPath(InstalledExePath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool Install()
    {
        try
        {
            KillOtherInstances();

            Directory.CreateDirectory(InstallDir);

            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe)) return false;

            if (!IsRunningFromInstallLocation())
            {
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        File.Copy(currentExe, InstalledExePath, overwrite: true);
                        break;
                    }
                    catch (IOException) when (attempt < 4)
                    {
                        Thread.Sleep(300);
                    }
                }
            }

            Autostart.Enable(InstalledExePath);
            EnsureShortcut();
            EnsureAumidRegistration();

            Process.Start(new ProcessStartInfo
            {
                FileName = InstalledExePath,
                UseShellExecute = false,
                WorkingDirectory = InstallDir,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool Uninstall()
    {
        try
        {
            KillOtherInstances();
            Autostart.Disable();
            ShortcutHelper.DeleteIfExists(AppIdentity.ShortcutPath);
            RemoveAumidRegistration();

            if (Directory.Exists(ConfigDir))
            {
                try { Directory.Delete(ConfigDir, recursive: true); } catch { }
            }

            if (Directory.Exists(InstallDir))
            {
                if (IsRunningFromInstallLocation())
                {
                    ScheduleDelayedDelete(InstallDir);
                }
                else
                {
                    try { Directory.Delete(InstallDir, recursive: true); } catch { }
                }
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool EnsureShortcut()
    {
        return ShortcutHelper.CreateShortcut(
            AppIdentity.ShortcutPath,
            InstalledExePath,
            AppIdentity.DisplayName,
            AppIdentity.Aumid,
            InstalledExePath);
    }

    public static void EnsureAumidRegistration()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(
                @"Software\Classes\AppUserModelId\" + AppIdentity.Aumid);
            if (key == null) return;
            key.SetValue("DisplayName", AppIdentity.DisplayName, RegistryValueKind.ExpandString);
            key.SetValue("IconUri", InstalledExePath, RegistryValueKind.ExpandString);
            key.SetValue("ShowInSettings", 1, RegistryValueKind.DWord);
        }
        catch { }
    }

    public static void RemoveAumidRegistration()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                @"Software\Classes\AppUserModelId\" + AppIdentity.Aumid,
                throwOnMissingSubKey: false);
        }
        catch { }
    }

    private static void KillOtherInstances()
    {
        var selfId = Process.GetCurrentProcess().Id;
        foreach (var p in Process.GetProcessesByName("WPUService"))
        {
            try
            {
                if (p.Id == selfId) continue;
                p.Kill(entireProcessTree: false);
                p.WaitForExit(3000);
            }
            catch { }
        }
    }

    private static void ScheduleDelayedDelete(string dir)
    {
        var script = $"timeout /t 3 /nobreak > nul & rmdir /s /q \"{dir}\"";
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + script,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System),
        });
    }
}
