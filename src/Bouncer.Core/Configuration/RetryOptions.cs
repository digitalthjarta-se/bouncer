namespace Bouncer.Core.Configuration;

public sealed class RetryOptions
{
    public int InitialBackoffSeconds { get; set; } = 60;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int MaxBackoffSeconds { get; set; } = 3600;
}
