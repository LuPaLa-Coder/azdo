namespace DevOpsCli.Transport;

public sealed class StdioTransport : IMcpTransport
{
    public async Task RunAsync(Func<string, CancellationToken, Task<string?>> onRequest, CancellationToken ct)
    {
        var reader = Console.In;
        Console.Error.WriteLine("[azdo-mcp] stdio transport started");

        string? line;
        while (!ct.IsCancellationRequested && (line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var response = await onRequest(line, ct);
            if (response is not null)
                await WriteAsync(response, ct);
        }

        Console.Error.WriteLine("[azdo-mcp] stdin closed, exiting");
    }

    public async Task WriteAsync(string json, CancellationToken ct)
    {
        await Console.Out.WriteLineAsync(json.AsMemory(), ct);
        await Console.Out.FlushAsync(ct);
    }
}
