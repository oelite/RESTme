namespace OElite.Restme.RateLimiting.Models;

/// <summary>
/// Leaky bucket for rate limiting
/// </summary>
public class LeakyBucket
{
    public string Key { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public double Level { get; set; }
    public DateTime LastLeakTime { get; set; }
    public double LeakRate { get; set; }
}
