namespace OElite.Restme.RateLimiting.Models;

/// <summary>
/// Fixed window for rate limiting
/// </summary>
public class FixedWindow
{
    public string Key { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public int Count { get; set; }
}
