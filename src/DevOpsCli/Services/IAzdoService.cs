using System.Text.Json;

namespace DevOpsCli.Services;

public interface IAzdoService
{
    // Work items
    Task<JsonElement> QueryWiAsync(string wiql, string? org, string? project, CancellationToken ct = default);
    Task<JsonElement> GetWiAsync(int[] ids, string[]? fields, string? org, string? project, CancellationToken ct = default);
    Task<JsonElement> CreateWiAsync(string type, string title, string? description, string? assignedTo,
        int? parent, string? iteration, string? org, string? project, CancellationToken ct = default);
    Task<JsonElement> UpdateWiAsync(int id, string? state, string? title, string? assignedTo,
        int? parent, string? iteration, string? org, string? project, CancellationToken ct = default);
    Task<JsonElement> CommentWiAsync(int id, string text, string? org, string? project, CancellationToken ct = default);

    // Repositories
    Task<JsonElement> ListReposAsync(string? org, string? project, int? top = null, int? skip = null,
        CancellationToken ct = default);

    // Pull requests
    Task<JsonElement> ListPrsAsync(string? repo, string? status, string? creator,
        string? org, string? project, int? top = null, int? skip = null, CancellationToken ct = default);
    Task<JsonElement> CreatePrAsync(string title, string? repo, string? source, string? target,
        string? description, bool draft, int[] workItems, string[] reviewers,
        string? org, string? project, CancellationToken ct = default);

    // Builds
    Task<JsonElement> ListBuildsAsync(int? top, int? definition, string? org, string? project,
        CancellationToken ct = default);
    Task<JsonElement> GetBuildStatusAsync(int id, string? org, string? project, CancellationToken ct = default);
    Task<JsonElement> TriggerBuildAsync(int definition, string? branch, string? org, string? project,
        CancellationToken ct = default);

    // Context
    string? DetectOrgUrl(string org);
}
