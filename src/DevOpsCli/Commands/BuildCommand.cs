using System.CommandLine;
using DevOpsCli.Output;
using DevOpsCli.Services;

namespace DevOpsCli.Commands;

public static class BuildCommand
{
    private static readonly Option<string?> OrgOpt = new("--org", "Override detected org");
    private static readonly Option<string?> ProjectOpt = new("--project", "Override detected project");

    public static Command Build()
    {
        var root = new Command("build", "Build and pipeline operations");
        root.AddCommand(BuildList());
        root.AddCommand(BuildStatus());
        root.AddCommand(BuildTrigger());
        return root;
    }

    private static Command BuildList()
    {
        var topOpt = new Option<int?>("--top", "Limit");
        var defOpt = new Option<int?>("--definition", "Definition ID filter");
        var compactOpt = new Option<bool>("--compact", () => false, "Return simplified output");
        var cmd = new Command("list", "List recent builds") { topOpt, defOpt, compactOpt, OrgOpt, ProjectOpt };
        cmd.SetHandler(async (int? top, int? definition, bool compact, string? org, string? project) =>
        {
            var result = await AppServices.Azdo.ListBuildsAsync(top, definition, org, project);
            Console.WriteLine(CompactTransformer.Transform(result, compact, "build_list"));
        }, topOpt, defOpt, compactOpt, OrgOpt, ProjectOpt);
        return cmd;
    }

    private static Command BuildStatus()
    {
        var idOpt = new Option<int>("--id", "Build ID") { IsRequired = true };
        var compactOpt = new Option<bool>("--compact", () => false, "Return simplified output");
        var cmd = new Command("status", "Get build status and timeline") { idOpt, compactOpt, OrgOpt, ProjectOpt };
        cmd.SetHandler(async (int id, bool compact, string? org, string? project) =>
        {
            var result = await AppServices.Azdo.GetBuildStatusAsync(id, org, project);
            if (!compact)
            {
                using var doc = System.Text.Json.JsonDocument.Parse(
                    System.Text.Json.JsonSerializer.Serialize(result));
                var root = doc.RootElement;
                Console.WriteLine("=== Build ===");
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                    root.TryGetProperty("build", out var b) ? b : root,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                if (root.TryGetProperty("timeline", out var tl))
                {
                    Console.WriteLine("=== Timeline ===");
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(tl,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
            }
            else
            {
                Console.WriteLine(CompactTransformer.Transform(result, true, "build_detail"));
            }
        }, idOpt, compactOpt, OrgOpt, ProjectOpt);
        return cmd;
    }

    private static Command BuildTrigger()
    {
        var defOpt = new Option<int>("--definition", "Pipeline definition ID") { IsRequired = true };
        var branchOpt = new Option<string?>("--branch", "Source branch (e.g. refs/heads/main)");
        var cmd = new Command("trigger", "Trigger a pipeline run") { defOpt, branchOpt, OrgOpt, ProjectOpt };
        cmd.SetHandler(async (int definition, string? branch, string? org, string? project) =>
        {
            var result = await AppServices.Azdo.TriggerBuildAsync(definition, branch, org, project);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }, defOpt, branchOpt, OrgOpt, ProjectOpt);
        return cmd;
    }
}
