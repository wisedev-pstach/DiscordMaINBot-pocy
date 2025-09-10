public static class ImageThrottler
{
    private static readonly List<DateTime> _requests = new();
    private static readonly Dictionary<ulong, string> _prompts = new();
    private const int MaxPerHour = 20;

    public static bool CanGenerate()
    {
        var now = DateTime.UtcNow;
        _requests.RemoveAll(time => now - time > TimeSpan.FromHours(1));
        return _requests.Count < MaxPerHour;
    }

    public static void RecordRequest() => _requests.Add(DateTime.UtcNow);

    public static int GetRemaining()
    {
        var now = DateTime.UtcNow;
        _requests.RemoveAll(time => now - time > TimeSpan.FromHours(1));
        return MaxPerHour - _requests.Count;
    }

    public static void StorePrompt(ulong messageId, string prompt) => _prompts[messageId] = prompt;

    public static string GetPrompt(ulong messageId) => _prompts.GetValueOrDefault(messageId, "");
}