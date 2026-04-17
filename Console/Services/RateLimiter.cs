namespace Console.Services;

public class RateLimiter
{
    private readonly SemaphoreSlim Semaphore = new(1, 1);
    private readonly TimeSpan Delay;
    private DateTime LastRelease = DateTime.MinValue;

    public RateLimiter(int requestsPerMinute)
    {
        Delay = TimeSpan.FromMinutes(1.0 / requestsPerMinute);
    }

    public async Task WaitAsync(CancellationToken ct = default)
    {
        await Semaphore.WaitAsync(ct);
        try
        {
            var elapsed = DateTime.Now - LastRelease;
            if (elapsed < Delay)
            {
                await Task.Delay(Delay - elapsed, ct);
            }
        }
        finally
        {
            LastRelease = DateTime.Now;
            Semaphore.Release();
        }
    }
}