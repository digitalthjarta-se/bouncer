using Bouncer.Core.Configuration;

namespace Bouncer.Core.Retry;

public static class BackoffCalculator
{
    public static TimeSpan Compute(int failureCount, int initialBackoffSeconds, double backoffMultiplier, int maxBackoffSeconds)
    {
        if (failureCount <= 0)
            return TimeSpan.Zero;

        var seconds = initialBackoffSeconds * Math.Pow(backoffMultiplier, failureCount - 1);
        seconds = Math.Min(seconds, maxBackoffSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    public static TimeSpan Compute(int failureCount, RetryOptions options) =>
        Compute(failureCount, options.InitialBackoffSeconds, options.BackoffMultiplier, options.MaxBackoffSeconds);
}
