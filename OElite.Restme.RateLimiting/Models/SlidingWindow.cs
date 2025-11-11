namespace OElite.Restme.RateLimiting.Models;

/// <summary>
/// Sliding window for rate limiting
/// </summary>
public class SlidingWindow
{
    private readonly List<DateTime> _requests = new();
    private readonly int _windowSizeSeconds;

    public SlidingWindow(int windowSizeSeconds)
    {
        _windowSizeSeconds = windowSizeSeconds;
    }

    public int RequestCount => _requests.Count;

    public void AddRequest(DateTime timestamp)
    {
        _requests.Add(timestamp);
    }

    public void RemoveOldRequests(DateTime cutoff)
    {
        _requests.RemoveAll(r => r < cutoff);
    }

    public int GetRequestCount(DateTime now)
    {
        var cutoff = now.AddSeconds(-_windowSizeSeconds);
        return _requests.Count(r => r >= cutoff);
    }
}
