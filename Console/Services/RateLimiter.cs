namespace Console.Services;

public class RateLimiter(int requestsPerMinute)
{
    private readonly SemaphoreSlim Semaphore = new(1, 1);
    private readonly TimeSpan Delay = TimeSpan.FromMinutes(1.0 / requestsPerMinute);
    private DateTime LastRelease = DateTime.MinValue;

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