namespace WPUService;

internal static class AppIdentity
{
    public const string Aumid = "WPUService.WorkstationPresence";
    public const string DisplayName = "Workstation Presence Utility";

    public static string ShortcutPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs",
            "WPUService.lnk");
}
