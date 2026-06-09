using System.Collections.Concurrent;

namespace DevOpsCli.Mcp;

public interface IMcpMiddleware
{
    Task<object?> InvokeAsync(string method, object? args, Func<string, object?, Task<object?>> next);
}

public sealed class McpPipeline
{
    private readonly List<IMcpMiddleware> _middlewares = [];

    public McpPipeline Add(IMcpMiddleware middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    public Func<string, object?, Task<object?>> Build(Func<string, object?, Task<object?>> handler)
    {
        Func<string, object?, Task<object?>> chain = handler;
        for (var i = _middlewares.Count - 1; i >= 0; i--)
        {
            var mw = _middlewares[i];
            var next = chain;
            chain = (method, args) => mw.InvokeAsync(method, args, (m, a) => next(m, a));
        }
        return chain;
    }
}

// ── Built-in middleware ─────────────────────────────────

/// <summary>Logs every request/response to stderr.</summary>
public sealed class LoggingMiddleware : IMcpMiddleware
{
    public async Task<object?> InvokeAsync(string method, object? args,
        Func<string, object?, Task<object?>> next)
    {
        var start = DateTime.UtcNow;
        Console.Error.WriteLine($"[azdo-mcp] → {method}");
        try
        {
            var result = await next(method, args);
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            Console.Error.WriteLine($"[azdo-mcp] ← {method} ({elapsed:F0}ms)");
            return result;
        }
        catch (Exception ex)
        {
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            Console.Error.WriteLine($"[azdo-mcp] ✗ {method} ({elapsed:F0}ms): {ex.Message}");
            throw;
        }
    }
}

/// <summary>Rate limiter: max N concurrent requests.</summary>
public sealed class ConcurrencyMiddleware : IMcpMiddleware
{
    private readonly SemaphoreSlim _sem;

    public ConcurrencyMiddleware(int maxConcurrency = 4) => _sem = new(maxConcurrency);

    public async Task<object?> InvokeAsync(string method, object? args,
        Func<string, object?, Task<object?>> next)
    {
        await _sem.WaitAsync();
        try
        {
            return await next(method, args);
        }
        finally
        {
            _sem.Release();
        }
    }
}
