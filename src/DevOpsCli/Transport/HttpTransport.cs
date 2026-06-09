using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace DevOpsCli.Transport;

/// <summary>
/// Streamable HTTP transport using raw HttpListener (zero extra dependencies).
/// Implements the MCP Streamable HTTP spec:
///   POST /mcp  — JSON-RPC request/response, optional SSE streaming
///   GET  /sse  — Server-Sent Events for server→client notifications
///   DELETE /mcp — session teardown
/// Session state is tracked via the Mcp-Session-Id header.
/// </summary>
public sealed class HttpTransport : IMcpTransport, IDisposable
{
    private readonly HttpListener _listener;
    private readonly string _url;
    private readonly ConcurrentDictionary<string, SseClient> _sessions = new();

    public HttpTransport(string url = "http://localhost:9287")
    {
        _url = url.TrimEnd('/');
        _listener = new HttpListener();
        _listener.Prefixes.Add(_url + "/");
    }

    public void Dispose() => _listener.Stop();

    public async Task RunAsync(Func<string, CancellationToken, Task<string?>> onRequest, CancellationToken ct)
    {
        _listener.Start();
        Console.Error.WriteLine($"[azdo-mcp] HTTP transport listening on {_url}");

        using var reg = ct.Register(() => _listener.Stop());

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().WaitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            _ = HandleRequestAsync(ctx, onRequest, ct);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx,
        Func<string, CancellationToken, Task<string?>> onRequest, CancellationToken ct)
    {
        try
        {
            var path = ctx.Request.Url!.AbsolutePath.TrimEnd('/');
            var sessionId = ctx.Request.Headers["Mcp-Session-Id"];

            switch (ctx.Request.HttpMethod.ToUpperInvariant())
            {
                case "POST" when path == "/mcp" || path == _url + "/mcp":
                    await HandlePostAsync(ctx, onRequest, sessionId, ct);
                    break;

                case "GET" when path == "/sse" || path == _url + "/sse":
                    await HandleSseAsync(ctx, sessionId, ct);
                    break;

                case "DELETE" when path == "/mcp" || path == _url + "/mcp":
                    HandleDelete(ctx, sessionId);
                    break;

                default:
                    ctx.Response.StatusCode = 404;
                    ctx.Response.Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[azdo-mcp] HTTP error: {ex.Message}");
            try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { }
        }
    }

    private async Task HandlePostAsync(HttpListenerContext ctx,
        Func<string, CancellationToken, Task<string?>> onRequest, string? sessionId, CancellationToken ct)
    {
        using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(body))
        {
            ctx.Response.StatusCode = 400;
            ctx.Response.Close();
            return;
        }

        var response = await onRequest(body, ct);

        if (response is null)
        {
            // Notification — no response needed
            ctx.Response.StatusCode = 202;
            ctx.Response.Close();
            return;
        }

        // Check if SSE session exists — if so, stream response via SSE
        if (sessionId is not null && _sessions.TryGetValue(sessionId, out var sse))
        {
            sse.Enqueue(response);
            ctx.Response.StatusCode = 202;
            ctx.Response.Close();
        }
        else
        {
            // Direct JSON response
            ctx.Response.ContentType = "application/json";
            var buf = Encoding.UTF8.GetBytes(response);
            ctx.Response.ContentLength64 = buf.Length;

            // Generate session ID for new sessions
            if (sessionId is null)
            {
                var newId = Guid.NewGuid().ToString("N");
                ctx.Response.Headers["Mcp-Session-Id"] = newId;
            }

            await ctx.Response.OutputStream.WriteAsync(buf, ct);
            ctx.Response.Close();
        }
    }

    private async Task HandleSseAsync(HttpListenerContext ctx, string? sessionId, CancellationToken ct)
    {
        if (sessionId is null)
        {
            ctx.Response.StatusCode = 400;
            ctx.Response.Close();
            return;
        }

        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers["Cache-Control"] = "no-cache";
        ctx.Response.Headers["Connection"] = "keep-alive";

        var sse = new SseClient(ctx, sessionId,
            () => _sessions.TryRemove(sessionId, out _));

        if (!_sessions.TryAdd(sessionId, sse))
        {
            ctx.Response.StatusCode = 409; // Conflict — session already exists
            ctx.Response.Close();
            return;
        }

        await sse.RunAsync(ct);
    }

    private void HandleDelete(HttpListenerContext ctx, string? sessionId)
    {
        if (sessionId is not null)
            _sessions.TryRemove(sessionId, out _);
        ctx.Response.StatusCode = 204;
        ctx.Response.Close();
    }

    public Task WriteAsync(string json, CancellationToken ct)
    {
        // For server-initiated notifications, broadcast to all SSE sessions
        foreach (var (_, sse) in _sessions)
            sse.Enqueue(json);
        return Task.CompletedTask;
    }

    private sealed class SseClient
    {
        private readonly HttpListenerContext _ctx;
        private readonly string _sessionId;
        private readonly Action _onDisconnect;
        private readonly ConcurrentQueue<string> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public SseClient(HttpListenerContext ctx, string sessionId, Action onDisconnect)
        {
            _ctx = ctx;
            _sessionId = sessionId;
            _onDisconnect = onDisconnect;
        }

        public void Enqueue(string json)
        {
            _queue.Enqueue(json);
            try { _signal.Release(); } catch (SemaphoreFullException) { }
        }

        public async Task RunAsync(CancellationToken ct)
        {
            try
            {
                // Send endpoint event with session ID
                var endpointData = $"event: endpoint\ndata: /mcp?session={_sessionId}\n\n";
                var buf = Encoding.UTF8.GetBytes(endpointData);
                await _ctx.Response.OutputStream.WriteAsync(buf, ct);
                await _ctx.Response.OutputStream.FlushAsync(ct);

                // Stream events
                while (!ct.IsCancellationRequested)
                {
                    await _signal.WaitAsync(ct);
                    while (_queue.TryDequeue(out var msg))
                    {
                        var data = $"event: message\ndata: {msg}\n\n";
                        buf = Encoding.UTF8.GetBytes(data);
                        await _ctx.Response.OutputStream.WriteAsync(buf, ct);
                        await _ctx.Response.OutputStream.FlushAsync(ct);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[azdo-mcp] SSE error for {_sessionId}: {ex.Message}");
            }
            finally
            {
                _onDisconnect();
                try { _ctx.Response.Close(); } catch { }
            }
        }
    }
}
