using Console.Services;

namespace Test;

public class RateLimiterTests
{
    [Test]
    public async Task WaitAsync_EnforcesDelay()
    {
        var limiter = new RateLimiter(requestsPerMinute: 60); // 1 per second
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await limiter.WaitAsync();
        await limiter.WaitAsync();
        sw.Stop();
        Assert.That(sw.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(900));
    }

    [Test]
    public async Task WaitAsync_FirstRequestIsNearInstant()
    {
        var limiter = new RateLimiter(requestsPerMinute: 60);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await limiter.WaitAsync();
        sw.Stop();
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(100));
    }
}