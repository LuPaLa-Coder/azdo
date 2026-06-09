using System.CommandLine;
using DevOpsCli.Output;
using DevOpsCli.Services;

namespace DevOpsCli.Commands;

public static class RepoCommand
{
    public static Command Build()
    {
        var orgOpt = new Option<string?>("--org", "Override detected org");
        var projectOpt = new Option<string?>("--project", "Override detected project");

        var root = new Command("repo", "Repository operations");

        var topOpt = new Option<int?>("--top", "Max number of results");
        var skipOpt = new Option<int?>("--skip", "Number of results to skip");
        var compactOpt = new Option<bool>("--compact", () => false, "Return simplified output");

        var list = new Command("list", "List repositories in the current project")
        {
            orgOpt, projectOpt, topOpt, skipOpt, compactOpt
        };
        list.SetHandler(async (string? org, string? project, int? top, int? skip, bool compact) =>
        {
            var result = await AppServices.Azdo.ListReposAsync(org, project, top, skip);
            Console.WriteLine(CompactTransformer.Transform(result, compact, "repo_list"));
        }, orgOpt, projectOpt, topOpt, skipOpt, compactOpt);
        root.AddCommand(list);
        return root;
    }
}
