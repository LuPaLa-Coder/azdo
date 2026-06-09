using System.Text.Json;

namespace DevOpsCli.Output;

public static class CompactTransformer
{
    private static readonly JsonSerializerOptions JsonOut = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Transform a raw ADO JsonElement into a compact representation, or return raw if compact=false.</summary>
    public static string Transform(JsonElement raw, bool compact, string kind)
    {
        if (!compact) return JsonSerializer.Serialize(raw, JsonOut);

        object result = kind switch
        {
            "wi_list" => CompactWiList(raw),
            "wi_single" => CompactWiSingle(raw),
            "repo_list" => CompactRepoList(raw),
            "pr_list" => CompactPrList(raw),
            "pr_single" => CompactPrSingle(raw),
            "build_list" => CompactBuildList(raw),
            "build_detail" => CompactBuildDetail(raw),
            _ => raw
        };

        return JsonSerializer.Serialize(result, JsonOut);
    }

    private static WorkItemListCompact CompactWiList(JsonElement raw)
    {
        var result = new WorkItemListCompact();
        if (raw.TryGetProperty("workItems", out var items))
        {
            foreach (var item in items.EnumerateArray())
                result.Items.Add(CompactWiSingle(item));
            result.Count = result.Items.Count;
        }
        else if (raw.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in raw.EnumerateArray())
                result.Items.Add(CompactWiSingle(item));
            result.Count = result.Items.Count;
        }
        else if (raw.TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in val.EnumerateArray())
                result.Items.Add(CompactWiSingle(item));
            result.Count = result.Items.Count;
        }
        return result;
    }

    private static WorkItemCompact CompactWiSingle(JsonElement item)
    {
        var wi = new WorkItemCompact();
        if (item.TryGetProperty("id", out var id) && id.TryGetInt32(out var i))
            wi.Id = i;
        if (item.TryGetProperty("rev", out var rev) && rev.TryGetInt32(out var r))
            wi.Rev = r;
        if (item.TryGetProperty("url", out var url))
            wi.Url = url.GetString();

        if (item.TryGetProperty("fields", out var fields))
        {
            wi.Type = Str(fields, "System.WorkItemType");
            wi.Title = Str(fields, "System.Title");
            wi.State = Str(fields, "System.State");
            wi.AssignedTo = AssignedToDisplay(fields);
            wi.CreatedDate = Date(fields, "System.CreatedDate");
            wi.ChangedDate = Date(fields, "System.ChangedDate");
        }
        return wi;
    }

    private static RepoListCompact CompactRepoList(JsonElement raw)
    {
        var result = new RepoListCompact();
        var items = raw.TryGetProperty("value", out var v) ? v : raw;
        if (items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                result.Repos.Add(new RepoCompact
                {
                    Id = Str(item, "id"),
                    Name = Str(item, "name"),
                    DefaultBranch = Str(item, "defaultBranch"),
                    Url = Str(item, "url"),
                    WebUrl = Str(item, "webUrl"),
                    SshUrl = Str(item, "sshUrl")
                });
            }
        }
        result.Count = result.Repos.Count;
        return result;
    }

    private static PrListCompact CompactPrList(JsonElement raw)
    {
        var result = new PrListCompact();
        var items = raw.TryGetProperty("value", out var v) ? v : raw;
        if (items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
                result.Prs.Add(CompactPrSingle(item));
        }
        result.Count = result.Prs.Count;
        return result;
    }

    private static PrCompact CompactPrSingle(JsonElement item) => new()
    {
        Id = Int(item, "pullRequestId") ?? 0,
        Title = Str(item, "title"),
        Status = Str(item, "status"),
        CreatedBy = Nested(item, "createdBy", "displayName"),
        CreationDate = Date(item, "creationDate"),
        SourceBranch = Str(item, "sourceRefName")?.Replace("refs/heads/", ""),
        TargetBranch = Str(item, "targetRefName")?.Replace("refs/heads/", ""),
        IsDraft = item.TryGetProperty("isDraft", out var d) && d.ValueKind == JsonValueKind.True,
        Url = Str(item, "url")
    };

    private static BuildListCompact CompactBuildList(JsonElement raw)
    {
        var result = new BuildListCompact();
        var items = raw.TryGetProperty("value", out var v) ? v : raw;
        if (items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                result.Builds.Add(new BuildCompact
                {
                    Id = Int(item, "id") ?? 0,
                    BuildNumber = Str(item, "buildNumber"),
                    Status = Str(item, "status"),
                    Result = Str(item, "result"),
                    Definition = Nested(item, "definition", "name"),
                    SourceBranch = Str(item, "sourceBranch")?.Replace("refs/heads/", ""),
                    QueueTime = Date(item, "queueTime"),
                    FinishTime = Date(item, "finishTime"),
                    Url = Str(item, "url")
                });
            }
        }
        result.Count = result.Builds.Count;
        return result;
    }

    private static BuildDetailCompact CompactBuildDetail(JsonElement raw)
    {
        JsonElement build = raw;
        if (raw.TryGetProperty("build", out var b))
            build = b;

        var detail = new BuildDetailCompact
        {
            Id = Int(build, "id") ?? 0,
            BuildNumber = Str(build, "buildNumber"),
            Status = Str(build, "status"),
            Result = Str(build, "result"),
            Definition = Nested(build, "definition", "name"),
            SourceBranch = Str(build, "sourceBranch")?.Replace("refs/heads/", ""),
            RequestedFor = Nested(build, "requestedFor", "displayName"),
            QueueTime = Date(build, "queueTime"),
            StartTime = Date(build, "startTime"),
            FinishTime = Date(build, "finishTime"),
            Url = Str(build, "url")
        };

        if (raw.TryGetProperty("timeline", out var tl) || build.TryGetProperty("timeline", out tl))
        {
            var records = tl.TryGetProperty("records", out var recs) ? recs : tl;
            if (records.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in records.EnumerateArray())
                {
                    detail.Timeline.Add(new TimelineRecordCompact
                    {
                        Id = Str(r, "id"),
                        Name = Str(r, "name"),
                        State = Str(r, "state"),
                        Result = Str(r, "result"),
                        StartTime = Date(r, "startTime"),
                        FinishTime = Date(r, "finishTime")
                    });
                }
            }
        }

        return detail;
    }

    // ── helpers ──────────────────────────────────────────

    private static string? Str(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.TryGetInt32(out var n) ? n : null;

    private static DateTime? Date(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.TryGetDateTime(out var dt) ? dt : null;

    private static string? Nested(JsonElement el, string parent, string child) =>
        el.TryGetProperty(parent, out var p) && p.TryGetProperty(child, out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString() : null;

    private static string? AssignedToDisplay(JsonElement fields)
    {
        if (fields.TryGetProperty("System.AssignedTo", out var at))
        {
            if (at.ValueKind == JsonValueKind.String) return at.GetString();
            if (at.TryGetProperty("displayName", out var dn)) return dn.GetString();
            if (at.TryGetProperty("uniqueName", out var un)) return un.GetString();
        }
        return null;
    }
}
