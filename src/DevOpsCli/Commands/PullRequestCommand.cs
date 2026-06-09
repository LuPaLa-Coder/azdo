using System.CommandLine;
using DevOpsCli.Output;
using DevOpsCli.Services;

namespace DevOpsCli.Commands;

public static class PullRequestCommand
{
    public static Command Build()
    {
        var orgOpt = new Option<string?>("--org", "Override detected org");
        var projectOpt = new Option<string?>("--project", "Override detected project");

        var root = new Command("pr", "Pull request operations");
        root.AddCommand(BuildList(orgOpt, projectOpt));
        root.AddCommand(BuildCreate(orgOpt, projectOpt));
        return root;
    }

    private static Command BuildList(Option<string?> orgOpt, Option<string?> projectOpt)
    {
        var repoOpt = new Option<string?>("--repo", "Repository ID or name (defaults to all)");
        var statusOpt = new Option<string?>("--status", "active | abandoned | completed | all");
        var creatorOpt = new Option<string?>("--creator", "Creator descriptor or email");
        var topOpt = new Option<int?>("--top", "Max number of results");
        var skipOpt = new Option<int?>("--skip", "Number of results to skip");
        var compactOpt = new Option<bool>("--compact", () => false, "Return simplified output");

        var list = new Command("list", "List pull requests")
        {
            orgOpt, projectOpt, repoOpt, statusOpt, creatorOpt, topOpt, skipOpt, compactOpt
        };
        list.SetHandler(async (string? org, string? project, string? repo, string? status, string? creator,
            int? top, int? skip, bool compact) =>
        {
            var result = await AppServices.Azdo.ListPrsAsync(repo, status, creator, org, project, top, skip);
            Console.WriteLine(CompactTransformer.Transform(result, compact, "pr_list"));
        }, orgOpt, projectOpt, repoOpt, statusOpt, creatorOpt, topOpt, skipOpt, compactOpt);
        return list;
    }

    private static Command BuildCreate(Option<string?> orgOpt, Option<string?> projectOpt)
    {
        var repoOpt = new Option<string?>("--repo", "Repository ID or name (default: detected from git remote)");
        var sourceOpt = new Option<string?>("--source", "Source branch (default: current git branch)");
        var targetOpt = new Option<string>("--target", () => "main", "Target branch (default: main)");
        var titleOpt = new Option<string>("--title", "PR title") { IsRequired = true };
        var descOpt = new Option<string?>("--description", "PR description");
        var draftOpt = new Option<bool>("--draft", () => false, "Create as draft");
        var workItemOpt = new Option<int[]>("--work-item", "Work item IDs to link (repeatable)")
        {
            AllowMultipleArgumentsPerToken = true
        };
        var reviewerOpt = new Option<string[]>("--reviewer", "Reviewer identity GUID (repeatable)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var create = new Command("create", "Create a pull request")
        {
            orgOpt, projectOpt, repoOpt, sourceOpt, targetOpt,
            titleOpt, descOpt, draftOpt, workItemOpt, reviewerOpt
        };

        create.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var result = await AppServices.Azdo.CreatePrAsync(
                ctx.ParseResult.GetValueForOption(titleOpt)!,
                ctx.ParseResult.GetValueForOption(repoOpt),
                ctx.ParseResult.GetValueForOption(sourceOpt),
                ctx.ParseResult.GetValueForOption(targetOpt),
                ctx.ParseResult.GetValueForOption(descOpt),
                ctx.ParseResult.GetValueForOption(draftOpt),
                ctx.ParseResult.GetValueForOption(workItemOpt) ?? [],
                ctx.ParseResult.GetValueForOption(reviewerOpt) ?? [],
                ctx.ParseResult.GetValueForOption(orgOpt),
                ctx.ParseResult.GetValueForOption(projectOpt));
            Console.WriteLine(CompactTransformer.Transform(result, false, "pr_single"));
        });
        return create;
    }
}
