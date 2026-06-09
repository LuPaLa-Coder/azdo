using System.Text.Json;
using DevOpsCli.Output;
using DevOpsCli.Services;

namespace DevOpsCli.Mcp;

public delegate Task<string> ToolHandler(JsonElement args, CancellationToken ct);

public sealed record ToolDef(string Name, string Description, object InputSchema, ToolHandler Handler)
{
    public object ToManifest() => new
    {
        name = Name,
        description = Description,
        inputSchema = InputSchema
    };
}

public static class McpTools
{
    private static readonly JsonSerializerOptions JsonOut = new() { WriteIndented = true };

    private static IAzdoService _svc = null!;

    /// <summary>Set the service to use for all tool handlers. Must be called before RunAsync.</summary>
    public static void SetService(IAzdoService service) => _svc = service;

    /// <summary>All registered tools. Requires SetService() to be called first.</summary>
    public static ToolDef[] All => ToolDefs(_svc ?? throw new InvalidOperationException(
        "McpTools.SetService() must be called before accessing All[]"));

    /// <summary>Tool definitions without handlers (for inspection/testing).</summary>
    public static ToolDef[] AllMetadata
    {
        get
        {
            var noop = Task.FromResult("");
            ToolHandler nop = (_, _) => noop;
            return
            [
                new("azdo_context", "Detect Azure DevOps org/project/repo from the current git remote", ObjSchema(), nop),
                new("azdo_wi_query", "Run a WIQL query against work items", ObjSchema(
                    ("wiql", true, Str("WIQL query")),
                    ("top", false, Int("Max results to return")),
                    ("compact", false, Bool("Return simplified output (default true)")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
                new("azdo_wi_get", "Get one or more work items by ID", ObjSchema(
                    ("ids", true, Str("Comma-separated work item IDs")),
                    ("fields", false, Str("Comma-separated fields, e.g. System.Title,System.State")),
                    ("compact", false, Bool("Return simplified output (default true)")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
                new("azdo_wi_create", "Create a new work item (Task, Bug, User Story, Feature, Epic)", ObjSchema(
                    ("type", true, Str("Work item type")),
                    ("title", true, Str("Title")),
                    ("description", false, Str("Description")),
                    ("assignedTo", false, Str("Assignee email")),
                    ("parent", false, Int("Parent work item ID")),
                    ("iteration", false, Str("Iteration path")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
                new("azdo_wi_update", "Update an existing work item", ObjSchema(
                    ("id", true, Int("Work item ID")),
                    ("state", false, Str("New state")),
                    ("title", false, Str("New title")),
                    ("assignedTo", false, Str("Assignee email")),
                    ("parent", false, Int("New parent work item ID")),
                    ("iteration", false, Str("Iteration path")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
                new("azdo_wi_comment", "Add a comment to a work item", ObjSchema(
                    ("id", true, Int("Work item ID")),
                    ("text", true, Str("Comment text")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
                new("azdo_repo_list", "List repositories in the project", ObjSchema(
                    ("top", false, Int("Max number of results")),
                    ("skip", false, Int("Number of results to skip")),
                    ("compact", false, Bool("Return simplified output (default true)")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
                new("azdo_pr_list", "List pull requests (org/project, optionally scoped to a repo)", ObjSchema(
                    ("repo", false, Str("Repository ID or name")),
                    ("status", false, Str("active | abandoned | completed | all")),
                    ("creator", false, Str("Creator descriptor or email")),
                    ("top", false, Int("Max number of results")),
                    ("skip", false, Int("Number of results to skip")),
                    ("compact", false, Bool("Return simplified output (default true)")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
                new("azdo_pr_create", "Create a pull request. Defaults: repo = detected from git remote, source = current git branch, target = main.", ObjSchema(
                    ("title", true, Str("Pull request title")),
                    ("repo", false, Str("Repository ID or name (default: detected)")),
                    ("source", false, Str("Source branch (default: current git branch)")),
                    ("target", false, Str("Target branch (default: main)")),
                    ("description", false, Str("PR description")),
                    ("draft", false, Bool("Create as draft (default false)")),
                    ("workItems", false, IntArr("Work item IDs to link")),
                    ("reviewers", false, StrArr("Reviewer identity GUIDs (email not resolved server-side)")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
                new("azdo_build_list", "List recent builds", ObjSchema(
                    ("top", false, Int("Max number of results")),
                    ("definition", false, Int("Pipeline definition ID filter")),
                    ("compact", false, Bool("Return simplified output (default true)")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
                new("azdo_build_status", "Get build status and timeline", ObjSchema(
                    ("id", true, Int("Build ID")),
                    ("compact", false, Bool("Return simplified output (default true)")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
                new("azdo_build_trigger", "Trigger a pipeline run", ObjSchema(
                    ("definition", true, Int("Pipeline definition ID")),
                    ("branch", false, Str("Source branch (e.g. refs/heads/main)")),
                    ("org", false, Str("Override detected org")),
                    ("project", false, Str("Override detected project"))), nop),
            ];
        }
    }

    private static ToolDef[] ToolDefs(IAzdoService svc) =>
    [
        ContextTool(svc),
        WiQuery(svc),
        WiGet(svc),
        WiCreate(svc),
        WiUpdate(svc),
        WiComment(svc),
        RepoList(svc),
        PrList(svc),
        PrCreate(svc),
        BuildList(svc),
        BuildStatus(svc),
        BuildTrigger(svc),
    ];

    // ── Schema helpers ──────────────────────────────────

    private static object Str(string desc) => new { type = "string", description = desc };
    private static object Int(string desc) => new { type = "integer", description = desc };
    private static object Bool(string desc) => new { type = "boolean", description = desc };
    private static object StrArr(string desc) => new { type = "array", items = new { type = "string" }, description = desc };
    private static object IntArr(string desc) => new { type = "array", items = new { type = "integer" }, description = desc };

    private static object ObjSchema(params (string name, bool required, object schema)[] fields)
    {
        var props = new Dictionary<string, object>();
        var required = new List<string>();
        foreach (var (name, req, schema) in fields)
        {
            props[name] = schema;
            if (req) required.Add(name);
        }
        return new
        {
            type = "object",
            properties = props,
            required = required.ToArray()
        };
    }

    // ── Arg parsers ─────────────────────────────────────

    private static string? S(JsonElement args, string key) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static int? I(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
        if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var ns)) return ns;
        return null;
    }

    private static bool? B(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.True) return true;
        if (v.ValueKind == JsonValueKind.False) return false;
        if (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var bs)) return bs;
        return null;
    }

    private static string[] SA(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Array)
            return [];
        var list = new List<string>();
        foreach (var item in v.EnumerateArray())
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
                list.Add(s);
        return list.ToArray();
    }

    private static int[] IA(JsonElement args, string key)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Array)
            return [];
        var list = new List<int>();
        foreach (var item in v.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var n)) list.Add(n);
            else if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out var ns)) list.Add(ns);
        }
        return list.ToArray();
    }

    // ── Output helpers ──────────────────────────────────

    private static string Format(JsonElement el, bool compact, string kind) =>
        CompactTransformer.Transform(el, compact, kind);

    private static bool IsCompact(JsonElement args) => B(args, "compact") ?? true;

    // ── Tools ───────────────────────────────────────────

    private static ToolDef ContextTool(IAzdoService svc) => new(
        "azdo_context",
        "Detect Azure DevOps org/project/repo from the current git remote",
        ObjSchema(),
        (_, _) =>
        {
            var d = global::DevOpsCli.Context.OrgDetector.Detect();
            if (d is null) return Task.FromResult("No Azure DevOps context detected.");
            var registered = Config.ConfigStore.Load().Organizations.ContainsKey(d.Org);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                org = d.Org,
                orgUrl = d.OrgUrl,
                project = d.Project,
                repo = d.Repo,
                source = d.Source,
                registered
            }, JsonOut));
        });

    private static ToolDef WiQuery(IAzdoService svc) => new(
        "azdo_wi_query",
        "Run a WIQL query against work items",
        ObjSchema(
            ("wiql", true, Str("WIQL query")),
            ("top", false, Int("Max results to return")),
            ("compact", false, Bool("Return simplified output (default true)")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var wiql = S(args, "wiql") ?? throw new ArgumentException("wiql is required");
            var result = await svc.QueryWiAsync(wiql, S(args, "org"), S(args, "project"), ct);
            return Format(result, IsCompact(args), "wi_list");
        });

    private static ToolDef WiGet(IAzdoService svc) => new(
        "azdo_wi_get",
        "Get one or more work items by ID",
        ObjSchema(
            ("ids", true, Str("Comma-separated work item IDs")),
            ("fields", false, Str("Comma-separated fields, e.g. System.Title,System.State")),
            ("compact", false, Bool("Return simplified output (default true)")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var ids = S(args, "ids") ?? throw new ArgumentException("ids is required");
            var fields = S(args, "fields");
            var idArr = ids.Split(',').Select(s => int.TryParse(s.Trim(), out var n) ? n : 0).Where(n => n > 0).ToArray();
            if (idArr.Length == 0) throw new ArgumentException("Invalid IDs");
            var fieldsArr = fields?.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray() ?? null;
            var result = await svc.GetWiAsync(idArr, fieldsArr, S(args, "org"), S(args, "project"), ct);
            return Format(result, IsCompact(args), "wi_list");
        });

    private static ToolDef WiCreate(IAzdoService svc) => new(
        "azdo_wi_create",
        "Create a new work item (Task, Bug, User Story, Feature, Epic)",
        ObjSchema(
            ("type", true, Str("Work item type")),
            ("title", true, Str("Title")),
            ("description", false, Str("Description")),
            ("assignedTo", false, Str("Assignee email")),
            ("parent", false, Int("Parent work item ID")),
            ("iteration", false, Str("Iteration path")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var type = S(args, "type") ?? throw new ArgumentException("type is required");
            var title = S(args, "title") ?? throw new ArgumentException("title is required");
            var result = await svc.CreateWiAsync(type, title, S(args, "description"),
                S(args, "assignedTo"), I(args, "parent"), S(args, "iteration"),
                S(args, "org"), S(args, "project"), ct);
            return CompactTransformer.Transform(result, false, "wi_single");
        });

    private static ToolDef WiUpdate(IAzdoService svc) => new(
        "azdo_wi_update",
        "Update an existing work item",
        ObjSchema(
            ("id", true, Int("Work item ID")),
            ("state", false, Str("New state")),
            ("title", false, Str("New title")),
            ("assignedTo", false, Str("Assignee email")),
            ("parent", false, Int("New parent work item ID")),
            ("iteration", false, Str("Iteration path")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var id = I(args, "id") ?? throw new ArgumentException("id is required");
            var result = await svc.UpdateWiAsync(id, S(args, "state"), S(args, "title"),
                S(args, "assignedTo"), I(args, "parent"), S(args, "iteration"),
                S(args, "org"), S(args, "project"), ct);
            return CompactTransformer.Transform(result, false, "wi_single");
        });

    private static ToolDef WiComment(IAzdoService svc) => new(
        "azdo_wi_comment",
        "Add a comment to a work item",
        ObjSchema(
            ("id", true, Int("Work item ID")),
            ("text", true, Str("Comment text")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var id = I(args, "id") ?? throw new ArgumentException("id is required");
            var text = S(args, "text") ?? throw new ArgumentException("text is required");
            var result = await svc.CommentWiAsync(id, text, S(args, "org"), S(args, "project"), ct);
            return JsonSerializer.Serialize(result, JsonOut);
        });

    private static ToolDef RepoList(IAzdoService svc) => new(
        "azdo_repo_list",
        "List repositories in the project",
        ObjSchema(
            ("top", false, Int("Max number of results")),
            ("skip", false, Int("Number of results to skip")),
            ("compact", false, Bool("Return simplified output (default true)")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var result = await svc.ListReposAsync(S(args, "org"), S(args, "project"),
                I(args, "top"), I(args, "skip"), ct);
            return Format(result, IsCompact(args), "repo_list");
        });

    private static ToolDef PrList(IAzdoService svc) => new(
        "azdo_pr_list",
        "List pull requests (org/project, optionally scoped to a repo)",
        ObjSchema(
            ("repo", false, Str("Repository ID or name")),
            ("status", false, Str("active | abandoned | completed | all")),
            ("creator", false, Str("Creator descriptor or email")),
            ("top", false, Int("Max number of results")),
            ("skip", false, Int("Number of results to skip")),
            ("compact", false, Bool("Return simplified output (default true)")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var result = await svc.ListPrsAsync(S(args, "repo"), S(args, "status"), S(args, "creator"),
                S(args, "org"), S(args, "project"), I(args, "top"), I(args, "skip"), ct);
            return Format(result, IsCompact(args), "pr_list");
        });

    private static ToolDef PrCreate(IAzdoService svc) => new(
        "azdo_pr_create",
        "Create a pull request. Defaults: repo = detected from git remote, source = current git branch, target = main.",
        ObjSchema(
            ("title", true, Str("Pull request title")),
            ("repo", false, Str("Repository ID or name (default: detected)")),
            ("source", false, Str("Source branch (default: current git branch)")),
            ("target", false, Str("Target branch (default: main)")),
            ("description", false, Str("PR description")),
            ("draft", false, Bool("Create as draft (default false)")),
            ("workItems", false, IntArr("Work item IDs to link")),
            ("reviewers", false, StrArr("Reviewer identity GUIDs (email not resolved server-side)")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var title = S(args, "title") ?? throw new ArgumentException("title is required");
            var result = await svc.CreatePrAsync(title, S(args, "repo"), S(args, "source"),
                S(args, "target"), S(args, "description"), B(args, "draft") ?? false,
                IA(args, "workItems"), SA(args, "reviewers"), S(args, "org"), S(args, "project"), ct);
            return CompactTransformer.Transform(result, false, "pr_single");
        });

    private static ToolDef BuildList(IAzdoService svc) => new(
        "azdo_build_list",
        "List recent builds",
        ObjSchema(
            ("top", false, Int("Max number of results")),
            ("definition", false, Int("Pipeline definition ID filter")),
            ("compact", false, Bool("Return simplified output (default true)")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var result = await svc.ListBuildsAsync(I(args, "top"), I(args, "definition"),
                S(args, "org"), S(args, "project"), ct);
            return Format(result, IsCompact(args), "build_list");
        });

    private static ToolDef BuildStatus(IAzdoService svc) => new(
        "azdo_build_status",
        "Get build status and timeline",
        ObjSchema(
            ("id", true, Int("Build ID")),
            ("compact", false, Bool("Return simplified output (default true)")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var id = I(args, "id") ?? throw new ArgumentException("id is required");
            var result = await svc.GetBuildStatusAsync(id, S(args, "org"), S(args, "project"), ct);
            return Format(result, IsCompact(args), "build_detail");
        });

    private static ToolDef BuildTrigger(IAzdoService svc) => new(
        "azdo_build_trigger",
        "Trigger a pipeline run",
        ObjSchema(
            ("definition", true, Int("Pipeline definition ID")),
            ("branch", false, Str("Source branch (e.g. refs/heads/main)")),
            ("org", false, Str("Override detected org")),
            ("project", false, Str("Override detected project"))),
        async (args, ct) =>
        {
            var def = I(args, "definition") ?? throw new ArgumentException("definition is required");
            var result = await svc.TriggerBuildAsync(def, S(args, "branch"),
                S(args, "org"), S(args, "project"), ct);
            return JsonSerializer.Serialize(result, JsonOut);
        });
}
