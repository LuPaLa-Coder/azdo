using System.CommandLine;
using DevOpsCli.Mcp;
using DevOpsCli.Transport;

namespace DevOpsCli.Commands;

public static class McpCommand
{
    public static Command Build()
    {
        var root = new Command("mcp", "Model Context Protocol server mode (for GitHub Copilot CLI, Claude, etc.)");

        root.AddCommand(BuildServe());
        root.AddCommand(BuildListTools());
        return root;
    }

    private static Command BuildServe()
    {
        var transportOpt = new Option<string>("--transport", () => "stdio", "Transport: stdio | http");
        transportOpt.FromAmong("stdio", "http");
        var portOpt = new Option<int>("--port", () => 9287, "HTTP listen port");

        var serve = new Command("serve", "Run as MCP server (JSON-RPC 2.0)")
        {
            transportOpt, portOpt
        };

        serve.SetHandler(async (string transport, int port) =>
        {
            IMcpTransport t = transport switch
            {
                "http" => new HttpTransport($"http://localhost:{port}"),
                _ => new StdioTransport()
            };

            McpTools.SetService(AppServices.Azdo);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            var server = new McpServer(t, AppServices.McpPipeline);
            await server.RunAsync(cts.Token);
        }, transportOpt, portOpt);

        return serve;
    }

    private static Command BuildListTools()
    {
        var cmd = new Command("list-tools", "Print the tool manifest (debug / inspection)");
        cmd.SetHandler(() =>
        {
            McpTools.SetService(AppServices.Azdo);
            var manifest = McpTools.All.Select(t => t.ToManifest()).ToArray();
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new { tools = manifest },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        });
        return cmd;
    }
}
