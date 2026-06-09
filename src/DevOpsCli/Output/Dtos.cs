using System.Text.Json.Serialization;

namespace DevOpsCli.Output;

// ── Work Items ──────────────────────────────────────────

public sealed class WorkItemCompact
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("rev")]
    public int Rev { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("assignedTo")]
    public string? AssignedTo { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime? CreatedDate { get; set; }

    [JsonPropertyName("changedDate")]
    public DateTime? ChangedDate { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class WorkItemListCompact
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("items")]
    public List<WorkItemCompact> Items { get; set; } = [];

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; set; }
}

// ── Repositories ────────────────────────────────────────

public sealed class RepoCompact
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("defaultBranch")]
    public string? DefaultBranch { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("webUrl")]
    public string? WebUrl { get; set; }

    [JsonPropertyName("sshUrl")]
    public string? SshUrl { get; set; }
}

public sealed class RepoListCompact
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("repos")]
    public List<RepoCompact> Repos { get; set; } = [];
}

// ── Pull Requests ───────────────────────────────────────

public sealed class PrCompact
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("createdBy")]
    public string? CreatedBy { get; set; }

    [JsonPropertyName("creationDate")]
    public DateTime? CreationDate { get; set; }

    [JsonPropertyName("sourceBranch")]
    public string? SourceBranch { get; set; }

    [JsonPropertyName("targetBranch")]
    public string? TargetBranch { get; set; }

    [JsonPropertyName("isDraft")]
    public bool IsDraft { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class PrListCompact
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("prs")]
    public List<PrCompact> Prs { get; set; } = [];

    [JsonPropertyName("continuationToken")]
    public string? ContinuationToken { get; set; }
}

// ── Builds ──────────────────────────────────────────────

public sealed class BuildCompact
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("buildNumber")]
    public string? BuildNumber { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("definition")]
    public string? Definition { get; set; }

    [JsonPropertyName("sourceBranch")]
    public string? SourceBranch { get; set; }

    [JsonPropertyName("queueTime")]
    public DateTime? QueueTime { get; set; }

    [JsonPropertyName("finishTime")]
    public DateTime? FinishTime { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class BuildListCompact
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("builds")]
    public List<BuildCompact> Builds { get; set; } = [];
}

// ── Build Details (build + timeline) ────────────────────

public sealed class BuildDetailCompact
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("buildNumber")]
    public string? BuildNumber { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("definition")]
    public string? Definition { get; set; }

    [JsonPropertyName("sourceBranch")]
    public string? SourceBranch { get; set; }

    [JsonPropertyName("requestedFor")]
    public string? RequestedFor { get; set; }

    [JsonPropertyName("queueTime")]
    public DateTime? QueueTime { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("finishTime")]
    public DateTime? FinishTime { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("timeline")]
    public List<TimelineRecordCompact> Timeline { get; set; } = [];
}

public sealed class TimelineRecordCompact
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("startTime")]
    public DateTime? StartTime { get; set; }

    [JsonPropertyName("finishTime")]
    public DateTime? FinishTime { get; set; }
}

// ── Generic wrappers for full-mode responses ────────────

public sealed class CountedResult
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("value")]
    public object Value { get; set; } = new();
}

public sealed class SingleResult
{
    [JsonPropertyName("value")]
    public object Value { get; set; } = new();
}
