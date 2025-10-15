namespace OElite.Restme.RateLimiting.Models;

/// <summary>
/// Token bucket for rate limiting
/// </summary>
public class TokenBucket
{
    public string Key { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public double Tokens { get; set; }
    public DateTime LastRefillTime { get; set; }
    public double RefillRate { get; set; }
}
