using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using DevOpsCli.Commands;
using DevOpsCli.Mcp;
using DevOpsCli.Services;
using DevOpsCli.Transport;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using DevOpsCli.Caching;

// ── DI setup ────────────────────────────────────────────
var services = new ServiceCollection();
services.AddSingleton<IAzdoService, AzdoService>();
services.Decorate<IAzdoService, CachedAzdoService>();
services.AddSingleton<IMemoryCache>(new MemoryCache(new MemoryCacheOptions()));
services.AddSingleton<IMcpTransport>(_ => new StdioTransport());
services.AddSingleton<McpPipeline>();

var sp = services.BuildServiceProvider();
AppServices.Provider = sp;

// ── CLI root ────────────────────────────────────────────
var root = new RootCommand("Azure DevOps CLI — central config, git-remote org detection")
{
    ContextCommand.Build(),
    ConfigCommand.Build(),
    WorkItemCommand.Build(),
    RepoCommand.Build(),
    PullRequestCommand.Build(),
    BuildCommand.Build(),
    McpCommand.Build(),
};

var builder = new CommandLineBuilder(root)
    .UseDefaults()
    .UseExceptionHandler((ex, ctx) =>
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        ctx.ExitCode = 1;
    });

return await builder.Build().InvokeAsync(args);

/// <summary>Static service locator for CLI commands.</summary>
public static class AppServices
{
    public static IServiceProvider Provider { get; set; } = null!;
    public static IAzdoService Azdo => Provider.GetRequiredService<IAzdoService>();
    public static IMcpTransport McpTransport => Provider.GetRequiredService<IMcpTransport>();
    public static McpPipeline McpPipeline => Provider.GetRequiredService<McpPipeline>();
    public static IMemoryCache MemoryCache => Provider.GetRequiredService<IMemoryCache>();
}

/// <summary>Simple decorator registration for IServiceCollection.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection Decorate<TInterface, TDecorator>(this IServiceCollection services)
        where TInterface : class
        where TDecorator : class, TInterface
    {
        var wrappedDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(TInterface));
        if (wrappedDescriptor is null)
            throw new InvalidOperationException($"{typeof(TInterface).Name} is not registered");

        var factory = wrappedDescriptor.ImplementationFactory
            ?? (sp => ActivatorUtilities.CreateInstance(sp, wrappedDescriptor.ImplementationType!));

        TInterface DecoratorFactory(IServiceProvider sp)
        {
            var inner = (TInterface)factory(sp);
            return (TInterface)ActivatorUtilities.CreateInstance(sp, typeof(TDecorator), inner);
        }

        services.Remove(wrappedDescriptor);
        services.AddSingleton<TInterface>(DecoratorFactory);
        return services;
    }
}
