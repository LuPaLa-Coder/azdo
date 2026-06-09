using System.CommandLine;
using System.Text.Json;
using DevOpsCli.Output;
using DevOpsCli.Services;

namespace DevOpsCli.Commands;

public static class WorkItemCommand
{
    public static Command Build()
    {
        var root = new Command("wi", "Work item operations");
        root.AddAlias("workitem");

        root.AddCommand(BuildQuery());
        root.AddCommand(BuildGet());
        root.AddCommand(BuildCreate());
        root.AddCommand(BuildUpdate());
        root.AddCommand(BuildComment());
        return root;
    }

    private static readonly Option<string?> OrgOpt = new("--org", "Override detected org");
    private static readonly Option<string?> ProjectOpt = new("--project", "Override detected project");

    private static Command BuildQuery()
    {
        var wiqlOpt = new Option<string>("--wiql", "WIQL query") { IsRequired = true };
        var compactOpt = new Option<bool>("--compact", () => false, "Return simplified output");
        var cmd = new Command("query", "Run a WIQL query") { wiqlOpt, compactOpt, OrgOpt, ProjectOpt };
        cmd.SetHandler(async (string wiql, bool compact, string? org, string? project) =>
        {
            var result = await AppServices.Azdo.QueryWiAsync(wiql, org, project);
            Print(CompactTransformer.Transform(result, compact, "wi_list"));
        }, wiqlOpt, compactOpt, OrgOpt, ProjectOpt);
        return cmd;
    }

    private static Command BuildGet()
    {
        var idsOpt = new Option<string>("--ids", "Comma-separated work item IDs") { IsRequired = true };
        var fieldsOpt = new Option<string?>("--fields", "Comma-separated fields (e.g. System.Title,System.State)");
        var compactOpt = new Option<bool>("--compact", () => false, "Return simplified output");
        var cmd = new Command("get", "Get one or more work items by ID") { idsOpt, fieldsOpt, compactOpt, OrgOpt, ProjectOpt };
        cmd.SetHandler(async (string ids, string? fields, bool compact, string? org, string? project) =>
        {
            var idArr = ids.Split(',').Select(s => int.TryParse(s.Trim(), out var n) ? n : 0).Where(n => n > 0).ToArray();
            var fieldsArr = fields?.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray() ?? null;
            var result = await AppServices.Azdo.GetWiAsync(idArr, fieldsArr, org, project);
            Print(CompactTransformer.Transform(result, compact, "wi_list"));
        }, idsOpt, fieldsOpt, compactOpt, OrgOpt, ProjectOpt);
        return cmd;
    }

    private static Command BuildCreate()
    {
        var typeOpt = new Option<string>("--type", "Work item type (Task, Bug, User Story, Feature, Epic)") { IsRequired = true };
        var titleOpt = new Option<string>("--title", "Title") { IsRequired = true };
        var descOpt = new Option<string?>("--description", "Description");
        var assignOpt = new Option<string?>("--assigned-to", "Assignee email");
        var parentOpt = new Option<int?>("--parent", "Parent work item ID");
        var iterOpt = new Option<string?>("--iteration", "Iteration path");
        var cmd = new Command("create", "Create a new work item")
        {
            typeOpt, titleOpt, descOpt, assignOpt, parentOpt, iterOpt, OrgOpt, ProjectOpt
        };
        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var result = await AppServices.Azdo.CreateWiAsync(
                ctx.ParseResult.GetValueForOption(typeOpt)!,
                ctx.ParseResult.GetValueForOption(titleOpt)!,
                ctx.ParseResult.GetValueForOption(descOpt),
                ctx.ParseResult.GetValueForOption(assignOpt),
                ctx.ParseResult.GetValueForOption(parentOpt),
                ctx.ParseResult.GetValueForOption(iterOpt),
                ctx.ParseResult.GetValueForOption(OrgOpt),
                ctx.ParseResult.GetValueForOption(ProjectOpt));
            Print(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        });
        return cmd;
    }

    private static Command BuildUpdate()
    {
        var idOpt = new Option<int>("--id", "Work item ID") { IsRequired = true };
        var stateOpt = new Option<string?>("--state", "New state");
        var titleOpt = new Option<string?>("--title", "New title");
        var assignOpt = new Option<string?>("--assigned-to", "Assignee email");
        var parentOpt = new Option<int?>("--parent", "New parent work item ID");
        var iterOpt = new Option<string?>("--iteration", "Iteration path");
        var cmd = new Command("update", "Update an existing work item")
        {
            idOpt, stateOpt, titleOpt, assignOpt, parentOpt, iterOpt, OrgOpt, ProjectOpt
        };
        cmd.SetHandler(async (System.CommandLine.Invocation.InvocationContext ctx) =>
        {
            var result = await AppServices.Azdo.UpdateWiAsync(
                ctx.ParseResult.GetValueForOption(idOpt),
                ctx.ParseResult.GetValueForOption(stateOpt),
                ctx.ParseResult.GetValueForOption(titleOpt),
                ctx.ParseResult.GetValueForOption(assignOpt),
                ctx.ParseResult.GetValueForOption(parentOpt),
                ctx.ParseResult.GetValueForOption(iterOpt),
                ctx.ParseResult.GetValueForOption(OrgOpt),
                ctx.ParseResult.GetValueForOption(ProjectOpt));
            Print(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        });
        return cmd;
    }

    private static Command BuildComment()
    {
        var idOpt = new Option<int>("--id", "Work item ID") { IsRequired = true };
        var textOpt = new Option<string>("--text", "Comment text") { IsRequired = true };
        var cmd = new Command("comment", "Add a comment to a work item") { idOpt, textOpt, OrgOpt, ProjectOpt };
        cmd.SetHandler(async (int id, string text, string? org, string? project) =>
        {
            var result = await AppServices.Azdo.CommentWiAsync(id, text, org, project);
            Print(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }, idOpt, textOpt, OrgOpt, ProjectOpt);
        return cmd;
    }

    private static void Print(string json) => Console.WriteLine(json);
}
