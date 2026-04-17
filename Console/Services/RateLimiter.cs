namespace RenshuuMnemonicExtractor.Services;

public class RateLimiter
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly TimeSpan _delay;
    private DateTime _lastRelease = DateTime.MinValue;

    public RateLimiter(int requestsPerMinute)
    {
        _delay = TimeSpan.FromMinutes(1.0 / requestsPerMinute);
    }

    public async Task WaitAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            var elapsed = DateTime.Now - _lastRelease;
            if (elapsed < _delay)
            {
                await Task.Delay(_delay - elapsed, ct);
            }
        }
        finally
        {
            _lastRelease = DateTime.Now;
            _semaphore.Release();
        }
    }
}