using System.Net.Http;

namespace WPUService;

internal sealed class PushoverSettings
{
    public string UserKey { get; init; } = "";
    public string ApiToken { get; init; } = "";

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(UserKey) && !string.IsNullOrWhiteSpace(ApiToken);
}

internal static class PushoverSender
{
    private const string Endpoint = "https://api.pushover.net/1/messages.json";
    private const int MaxTitle = 250;
    private const int MaxMessage = 1024;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static async Task<(bool Ok, string Error)> SendAsync(PushoverSettings s, string title, string message)
    {
        if (!s.IsValid) return (false, "Pushover credentials are not configured.");

        var fields = new Dictionary<string, string>
        {
            ["token"] = s.ApiToken,
            ["user"] = s.UserKey,
            ["title"] = Truncate(title, MaxTitle),
            ["message"] = Truncate(message, MaxMessage),
        };

        try
        {
            using var content = new FormUrlEncodedContent(fields);
            using var resp = await Http.PostAsync(Endpoint, content).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (resp.IsSuccessStatusCode) return (true, "");
            return (false, $"HTTP {(int)resp.StatusCode}: {body}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string Truncate(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}
