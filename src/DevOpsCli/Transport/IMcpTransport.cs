namespace DevOpsCli.Transport;

public interface IMcpTransport
{
    Task RunAsync(Func<string, CancellationToken, Task<string?>> onRequest, CancellationToken ct);
    Task WriteAsync(string json, CancellationToken ct);
}
