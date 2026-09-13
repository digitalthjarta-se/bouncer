using Bouncer.Core.Retry;

namespace Bouncer.Tests;

public class BackoffCalculatorTests
{
    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 120)]
    [InlineData(3, 240)]
    [InlineData(4, 480)]
    [InlineData(5, 960)]
    [InlineData(6, 1920)]
    [InlineData(7, 3600)]
    [InlineData(20, 3600)]
    public void Compute_FollowsExponentialBackoffCappedAtMax(int failureCount, int expectedSeconds)
    {
        var delay = BackoffCalculator.Compute(failureCount, initialBackoffSeconds: 60, backoffMultiplier: 2.0, maxBackoffSeconds: 3600);

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), delay);
    }

    [Fact]
    public void Compute_ZeroFailures_ReturnsZero()
    {
        var delay = BackoffCalculator.Compute(0, 60, 2.0, 3600);

        Assert.Equal(TimeSpan.Zero, delay);
    }
}
