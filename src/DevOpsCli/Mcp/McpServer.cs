using System.Text.Json;
using System.Text.Json.Serialization;
using DevOpsCli.Transport;

namespace DevOpsCli.Mcp;

public sealed class JsonRpcException : Exception
{
    public int Code { get; }
    public JsonRpcException(int code, string message) : base(message) { Code = code; }
}

public sealed class McpServer
{
    private const string ProtocolVersion = "2024-11-05";
    private const string ServerName = "azdo";
    private const string ServerVersion = "0.2.0";

    private static readonly JsonSerializerOptions WireOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IMcpTransport _transport;
    private readonly Func<string, object?, Task<object?>> _dispatchPipeline;

    public McpServer(IMcpTransport transport, McpPipeline? pipeline = null)
    {
        _transport = transport;
        _dispatchPipeline = (pipeline ?? new McpPipeline())
            .Add(new LoggingMiddleware())
            .Add(new ConcurrencyMiddleware(4))
            .Build(DispatchAsync);
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        Console.Error.WriteLine($"[azdo-mcp] starting (protocol {ProtocolVersion}, version {ServerVersion})");

        await _transport.RunAsync(async (line, innerCt) =>
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(line); }
            catch (JsonException ex)
            {
                Console.Error.WriteLine($"[azdo-mcp] parse error: {ex.Message}");
                return null;
            }

            using (doc)
            {
                var root = doc.RootElement;
                var method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
                var hasId = root.TryGetProperty("id", out var idEl);
                var paramsEl = root.TryGetProperty("params", out var p) ? (JsonElement?)p : null;

                if (!hasId)
                {
                    if (method == "notifications/initialized")
                        Console.Error.WriteLine("[azdo-mcp] client initialized");
                    return null;
                }

                object response;
                try
                {
                    var result = await _dispatchPipeline(method!, (object?)paramsEl);
                    response = new { jsonrpc = "2.0", id = idEl, result };
                }
                catch (JsonRpcException jre)
                {
                    response = new { jsonrpc = "2.0", id = idEl, error = new { code = jre.Code, message = jre.Message } };
                }
                catch (Exception ex)
                {
                    response = new { jsonrpc = "2.0", id = idEl, error = new { code = -32603, message = ex.Message } };
                }

                return JsonSerializer.Serialize(response, WireOpts);
            }
        }, ct);

        Console.Error.WriteLine("[azdo-mcp] transport closed, exiting");
    }

    private static async Task<object?> DispatchAsync(string method, object? args)
    {
        return method switch
        {
            "initialize" => new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new { tools = new { } },
                serverInfo = new { name = ServerName, version = ServerVersion }
            },
            "tools/list" => new { tools = McpTools.All.Select(t => t.ToManifest()).ToArray() },
            "tools/call" => await CallToolAsync(args),
            "ping" => new { },
            _ => throw new JsonRpcException(-32601, $"Method not found: {method}")
        };
    }

    private static async Task<object> CallToolAsync(object? args)
    {
        if (args is not JsonElement je) throw new JsonRpcException(-32602, "missing params");
        var nameEl = je.TryGetProperty("name", out var n) ? n : default;
        if (nameEl.ValueKind != JsonValueKind.String) throw new JsonRpcException(-32602, "tool name required");
        var name = nameEl.GetString()!;
        var argsObj = je.TryGetProperty("arguments", out var a) ? a : default;

        var tool = McpTools.All.FirstOrDefault(t => t.Name == name)
            ?? throw new JsonRpcException(-32602, $"Unknown tool: {name}");

        try
        {
            var text = await tool.Handler(argsObj, CancellationToken.None);
            return new
            {
                content = new[] { new { type = "text", text } },
                isError = false
            };
        }
        catch (Exception ex)
        {
            return new
            {
                content = new[] { new { type = "text", text = ex.Message } },
                isError = true
            };
        }
    }
}
