using System.Text;
using System.Text.Json;
using DevOpsCli.AzureDevOps;
using DevOpsCli.Config;
using DevOpsCli.Context;

namespace DevOpsCli.Services;

public sealed class AzdoService : IAzdoService, IDisposable
{
    private readonly HttpClient _http;
    private readonly CentralConfig _cfg;
    private bool _disposed;

    public AzdoService()
    {
        _cfg = ConfigStore.Load();
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(_cfg.TimeoutSeconds) };
        _http.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void Dispose()
    {
        if (!_disposed) { _http.Dispose(); _disposed = true; }
    }

    // ── Work Items ──────────────────────────────────────

    public async Task<JsonElement> QueryWiAsync(string wiql, string? org, string? project, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        RequireProject(s);
        var body = JsonBody(new { query = wiql });
        return await PostAsync(s, $"{s.Project}/_apis/wit/wiql", body, ct);
    }

    public async Task<JsonElement> GetWiAsync(int[] ids, string[]? fields, string? org, string? project, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        var idStr = string.Join(",", ids);
        var path = $"_apis/wit/workitems?ids={Uri.EscapeDataString(idStr)}";
        if (fields is { Length: > 0 })
            path += $"&fields={Uri.EscapeDataString(string.Join(",", fields))}";
        return await GetAsync(s, path, ct);
    }

    public async Task<JsonElement> CreateWiAsync(string type, string title, string? description, string? assignedTo,
        int? parent, string? iteration, string? org, string? project, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        RequireProject(s);
        var ops = new List<object> { new { op = "add", path = "/fields/System.Title", value = title } };
        if (description is not null) ops.Add(new { op = "add", path = "/fields/System.Description", value = description });
        if (assignedTo is not null) ops.Add(new { op = "add", path = "/fields/System.AssignedTo", value = assignedTo });
        if (iteration is not null) ops.Add(new { op = "add", path = "/fields/System.IterationPath", value = iteration });
        if (parent.HasValue)
            ops.Add(new { op = "add", path = "/relations/-", value = new { rel = "System.LinkTypes.Hierarchy-Reverse", url = $"{s.OrgUrl}/_apis/wit/workItems/{parent.Value}" } });

        var body = JsonPatchBody(ops);
        return await PostAsync(s, $"{s.Project}/_apis/wit/workitems/${Uri.EscapeDataString(type)}", body, ct);
    }

    public async Task<JsonElement> UpdateWiAsync(int id, string? state, string? title, string? assignedTo,
        int? parent, string? iteration, string? org, string? project, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        var ops = new List<object>();
        if (state is not null) ops.Add(new { op = "add", path = "/fields/System.State", value = state });
        if (title is not null) ops.Add(new { op = "add", path = "/fields/System.Title", value = title });
        if (assignedTo is not null) ops.Add(new { op = "add", path = "/fields/System.AssignedTo", value = assignedTo });
        if (iteration is not null) ops.Add(new { op = "add", path = "/fields/System.IterationPath", value = iteration });
        if (parent.HasValue)
            ops.Add(new { op = "add", path = "/relations/-", value = new { rel = "System.LinkTypes.Hierarchy-Reverse", url = $"{s.OrgUrl}/_apis/wit/workItems/{parent.Value}" } });
        if (ops.Count == 0) throw new ArgumentException("No fields to update.");

        var body = JsonPatchBody(ops);
        return await PatchAsync(s, $"_apis/wit/workitems/{id}", body, ct);
    }

    public async Task<JsonElement> CommentWiAsync(int id, string text, string? org, string? project, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        RequireProject(s);
        var body = JsonBody(new { text });
        return await PostAsync(s, $"{s.Project}/_apis/wit/workItems/{id}/comments", body, ct);
    }

    // ── Repositories ────────────────────────────────────

    public async Task<JsonElement> ListReposAsync(string? org, string? project, int? top = null, int? skip = null, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        var path = string.IsNullOrEmpty(s.Project)
            ? "_apis/git/repositories"
            : $"{s.Project}/_apis/git/repositories";
        path = AppendOData(path, top, skip);
        return await GetAsync(s, path, ct);
    }

    // ── Pull Requests ───────────────────────────────────

    public async Task<JsonElement> ListPrsAsync(string? repo, string? status, string? creator,
        string? org, string? project, int? top = null, int? skip = null, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        var qs = new StringBuilder();
        void Add(string k, string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return;
            qs.Append(qs.Length == 0 ? '?' : '&');
            qs.Append(k).Append('=').Append(Uri.EscapeDataString(v));
        }
        Add("searchCriteria.status", status);
        Add("searchCriteria.creatorId", creator);
        if (top.HasValue) Add("$top", top.Value.ToString());
        if (skip.HasValue) Add("$skip", skip.Value.ToString());

        string path;
        if (!string.IsNullOrWhiteSpace(repo))
        {
            var scope = string.IsNullOrEmpty(s.Project) ? "" : s.Project + "/";
            path = $"{scope}_apis/git/repositories/{Uri.EscapeDataString(repo)}/pullrequests{qs}";
        }
        else if (!string.IsNullOrEmpty(s.Project))
            path = $"{s.Project}/_apis/git/pullrequests{qs}";
        else
            path = $"_apis/git/pullrequests{qs}";

        return await GetAsync(s, path, ct);
    }

    public async Task<JsonElement> CreatePrAsync(string title, string? repo, string? source, string? target,
        string? description, bool draft, int[] workItems, string[] reviewers,
        string? org, string? project, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        var finalRepo = repo ?? s.Detected?.Repo
            ?? throw new InvalidOperationException("Repository not specified and not detected from git remote.");
        var finalSource = source ?? OrgDetector.CurrentBranch()
            ?? throw new InvalidOperationException("Source branch not specified and current git branch not detected.");
        var finalTarget = target ?? "main";

        var dict = new Dictionary<string, object>
        {
            ["sourceRefName"] = NormalizeRef(finalSource),
            ["targetRefName"] = NormalizeRef(finalTarget),
            ["title"] = title,
            ["isDraft"] = draft,
        };
        if (!string.IsNullOrWhiteSpace(description)) dict["description"] = description;
        if (workItems is { Length: > 0 }) dict["workItemRefs"] = workItems.Select(id => new { id = id.ToString() }).ToArray();
        if (reviewers is { Length: > 0 }) dict["reviewers"] = reviewers.Select(id => new { id }).ToArray();

        var scope = string.IsNullOrEmpty(s.Project) ? "" : s.Project + "/";
        var path = $"{scope}_apis/git/repositories/{Uri.EscapeDataString(finalRepo)}/pullrequests";
        return await PostAsync(s, path, JsonBody(dict), ct);
    }

    // ── Builds ──────────────────────────────────────────

    public async Task<JsonElement> ListBuildsAsync(int? top, int? definition, string? org, string? project, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        RequireProject(s);
        var qs = new List<string>();
        if (top.HasValue) qs.Add($"$top={top}");
        if (definition.HasValue) qs.Add($"definitions={definition}");
        var query = qs.Count > 0 ? "?" + string.Join('&', qs) : "";
        return await GetAsync(s, $"{s.Project}/_apis/build/builds{query}", ct);
    }

    public async Task<JsonElement> GetBuildStatusAsync(int id, string? org, string? project, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        RequireProject(s);
        var build = await GetAsync(s, $"{s.Project}/_apis/build/builds/{id}", ct);
        var timeline = await GetAsync(s, $"{s.Project}/_apis/build/builds/{id}/timeline", ct);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { build, timeline }));
        return doc.RootElement.Clone();
    }

    public async Task<JsonElement> TriggerBuildAsync(int definition, string? branch, string? org, string? project, CancellationToken ct = default)
    {
        using var s = OpenSession(org, project);
        RequireProject(s);
        object payload = string.IsNullOrWhiteSpace(branch)
            ? new { definition = new { id = definition } }
            : new { definition = new { id = definition }, sourceBranch = branch };
        return await PostAsync(s, $"{s.Project}/_apis/build/builds", JsonBody(payload), ct);
    }

    // ── Context ─────────────────────────────────────────

    public string? DetectOrgUrl(string org)
    {
        var cfg = ConfigStore.Load();
        if (cfg.Organizations.TryGetValue(org, out var entry) && !string.IsNullOrWhiteSpace(entry.OrganizationUrl))
            return entry.OrganizationUrl;
        return $"https://dev.azure.com/{org}";
    }

    // ── Private helpers ─────────────────────────────────

    private sealed record SessionInfo(string Org, string? Project, string OrgUrl, DetectedContext? Detected) : IDisposable
    {
        public void Dispose() { }
    }

    private SessionInfo OpenSession(string? orgOverride, string? projectOverride)
    {
        string org;
        string? project;
        DetectedContext? detected = null;
        string? detectedUrl = null;

        if (!string.IsNullOrWhiteSpace(orgOverride))
        {
            org = orgOverride!;
            project = projectOverride;
        }
        else
        {
            detected = OrgDetector.Detect()
                ?? throw new InvalidOperationException(
                    "Could not detect Azure DevOps org from git remote. Run inside a repo with an Azure DevOps remote, or pass --org.");
            org = detected.Org;
            project = projectOverride ?? detected.Project;
            detectedUrl = detected.OrgUrl;
        }

        var entry = PatPrompt.EnsureOrgRegistered(_cfg, org, detectedUrl);

        if (string.IsNullOrWhiteSpace(project))
            project = entry.DefaultProject;

        // Set auth for this call
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{entry.Pat}"));
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

        return new SessionInfo(org, project, entry.OrganizationUrl.TrimEnd('/'), detected);
    }

    private string BuildUrl(SessionInfo s, string path)
    {
        var sep = path.Contains('?') ? "&" : "?";
        return $"{s.OrgUrl}/{path.TrimStart('/')}{sep}api-version={_cfg.DefaultApiVersion}";
    }

    private async Task<JsonElement> GetAsync(SessionInfo s, string path, CancellationToken ct)
    {
        var url = BuildUrl(s, path);
        using var resp = await _http.GetAsync(url, ct);
        return await ReadJsonAsync(resp, ct);
    }

    private async Task<JsonElement> PostAsync(SessionInfo s, string path, HttpContent body, CancellationToken ct)
    {
        var url = BuildUrl(s, path);
        using var resp = await _http.PostAsync(url, body, ct);
        return await ReadJsonAsync(resp, ct);
    }

    private async Task<JsonElement> PatchAsync(SessionInfo s, string path, HttpContent body, CancellationToken ct)
    {
        var url = BuildUrl(s, path);
        var req = new HttpRequestMessage(HttpMethod.Patch, url) { Content = body };
        using var resp = await _http.SendAsync(req, ct);
        return await ReadJsonAsync(resp, ct);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var content = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Azure DevOps API {(int)resp.StatusCode} {resp.ReasonPhrase}: {Truncate(content, 500)}");
        if (string.IsNullOrWhiteSpace(content)) return default;
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.Clone();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
    private static string NormalizeRef(string branch) =>
        branch.StartsWith("refs/", StringComparison.OrdinalIgnoreCase) ? branch : $"refs/heads/{branch}";

    private static StringContent JsonBody(object obj, string mediaType = "application/json") =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, mediaType);

    private static StringContent JsonPatchBody(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json-patch+json");

    private static string AppendOData(string path, int? top, int? skip)
    {
        var parts = new List<string>();
        if (top.HasValue) parts.Add($"$top={top.Value}");
        if (skip.HasValue) parts.Add($"$skip={skip.Value}");
        if (parts.Count == 0) return path;
        var sep = path.Contains('?') ? "&" : "?";
        return path + sep + string.Join('&', parts);
    }

    private static void RequireProject(SessionInfo s)
    {
        if (string.IsNullOrWhiteSpace(s.Project))
            throw new InvalidOperationException("Project is required for this operation. Pass 'project' or set a defaultProject on the org.");
    }
}
