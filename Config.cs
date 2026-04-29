using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WPUService;

internal enum TeamsFilterMode
{
    Any,
    CallsOnly,
}

internal enum RecipientMode
{
    Email,
    Sms,
}

internal enum SendMode
{
    Outlook,
    Smtp,
    Pushover,
}

internal sealed class Config
{
    public bool Enabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool PauseOnTeamsCall { get; set; } = true;
    public int IdleThresholdSeconds { get; set; } = 120;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TeamsFilterMode TeamsFilter { get; set; } = TeamsFilterMode.Any;

    public int AlertDelaySeconds { get; set; } = 300;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RecipientMode RecipientMode { get; set; } = RecipientMode.Email;

    public string RecipientEmail { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string CarrierKey { get; set; } = "";
    public string CustomGateway { get; set; } = "";

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SendMode SendMode { get; set; } = SendMode.Outlook;

    public bool UseOutlook { get; set; } = true;

    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    public string SmtpUsername { get; set; } = "";
    public string SmtpPasswordEncrypted { get; set; } = "";
    public string SmtpFromAddress { get; set; } = "";

    public string PushoverUserKey { get; set; } = "";
    public string PushoverApiTokenEncrypted { get; set; } = "";

    private static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WPUService");

    private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    public static Config Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<Config>(json);
                if (loaded != null)
                {
                    if (!json.Contains("\"SendMode\""))
                        loaded.SendMode = loaded.UseOutlook ? SendMode.Outlook : SendMode.Smtp;
                    return loaded;
                }
            }
        }
        catch { }
        return new Config();
    }

    public void Save()
    {
        try
        {
            UseOutlook = SendMode == SendMode.Outlook;
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    public static string ProtectPassword(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return "";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var encrypted = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }
        catch { return ""; }
    }

    public static string UnprotectPassword(string protectedB64)
    {
        if (string.IsNullOrEmpty(protectedB64)) return "";
        try
        {
            var bytes = Convert.FromBase64String(protectedB64);
            var plain = ProtectedData.Unprotect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return ""; }
    }
}
