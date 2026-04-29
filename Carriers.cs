namespace WPUService;

internal sealed record Carrier(string Key, string DisplayName, string Domain);

internal static class Carriers
{
    public const string CustomKey = "custom";

    public static readonly Carrier[] All =
    {
        new("verizon-sms",     "Verizon (SMS)",        "vtext.com"),
        new("verizon-mms",     "Verizon (MMS)",        "vzwpix.com"),
        new("att-sms",         "AT&T (SMS)",           "txt.att.net"),
        new("att-mms",         "AT&T (MMS)",           "mms.att.net"),
        new("tmobile",         "T-Mobile (SMS/MMS)",   "tmomail.net"),
        new("uscellular-sms",  "US Cellular (SMS)",    "email.uscc.net"),
        new("uscellular-mms",  "US Cellular (MMS)",    "mms.uscc.net"),
        new("cricket-sms",     "Cricket (SMS)",        "sms.cricketwireless.net"),
        new("cricket-mms",     "Cricket (MMS)",        "mms.cricketwireless.net"),
        new("boost",           "Boost",                "sms.myboostmobile.com"),
        new("metropcs",        "Metro by T-Mobile",    "mymetropcs.com"),
        new("googlefi",        "Google Fi",            "msg.fi.google.com"),
        new("xfinity",         "Xfinity Mobile",       "vtext.com"),
        new(CustomKey,         "Custom...",            ""),
    };

    public static Carrier? Find(string key)
    {
        foreach (var c in All)
        {
            if (string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    public static string ResolveGateway(string carrierKey, string customGateway)
    {
        if (string.Equals(carrierKey, CustomKey, StringComparison.OrdinalIgnoreCase))
            return customGateway?.TrimStart('@') ?? "";
        return Find(carrierKey)?.Domain ?? "";
    }

    public static string BuildRecipient(string phoneNumber, string carrierKey, string customGateway)
    {
        var digits = new string((phoneNumber ?? "").Where(char.IsDigit).ToArray());
        var domain = ResolveGateway(carrierKey, customGateway);
        if (string.IsNullOrEmpty(digits) || string.IsNullOrEmpty(domain))
            return "";
        return $"{digits}@{domain}";
    }
}
