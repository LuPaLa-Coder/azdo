using System.Text.Json;
using DevOpsCli.Services;
using Microsoft.Extensions.Caching.Memory;

namespace DevOpsCli.Caching;

/// <summary>Decorator that caches GET responses from IAzdoService for a configurable TTL.</summary>
public sealed class CachedAzdoService : IAzdoService, IDisposable
{
    private readonly IAzdoService _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    public CachedAzdoService(IAzdoService inner, IMemoryCache? cache = null, int ttlSeconds = 60)
    {
        _inner = inner;
        _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
        _ttl = TimeSpan.FromSeconds(ttlSeconds);
    }

    public void Dispose() => (_inner as IDisposable)?.Dispose();

    private async Task<JsonElement> GetOrSet(string key, Func<Task<JsonElement>> factory, CancellationToken ct)
    {
        if (_cache.TryGetValue(key, out JsonElement cached))
            return cached;

        var result = await factory();
        using var doc = JsonDocument.Parse(result.GetRawText());
        var clone = doc.RootElement.Clone();
        _cache.Set(key, clone, _ttl);
        return clone;
    }

    // ── Cached read operations ──────────────────────────

    public Task<JsonElement> QueryWiAsync(string wiql, string? org, string? project, CancellationToken ct = default)
    {
        // Don't cache WIQL queries — they're dynamic and vary widely
        return _inner.QueryWiAsync(wiql, org, project, ct);
    }

    public Task<JsonElement> GetWiAsync(int[] ids, string[]? fields, string? org, string? project, CancellationToken ct = default)
    {
        var key = $"wi:{string.Join(",", ids)}:{org}:{project}";
        return GetOrSet(key, () => _inner.GetWiAsync(ids, fields, org, project, ct), ct);
    }

    public Task<JsonElement> ListReposAsync(string? org, string? project, int? top = null, int? skip = null, CancellationToken ct = default)
    {
        var key = $"repos:{org}:{project}:{top}:{skip}";
        return GetOrSet(key, () => _inner.ListReposAsync(org, project, top, skip, ct), ct);
    }

    public Task<JsonElement> ListPrsAsync(string? repo, string? status, string? creator, string? org, string? project,
        int? top = null, int? skip = null, CancellationToken ct = default)
    {
        var key = $"prs:{repo}:{status}:{creator}:{org}:{project}:{top}:{skip}";
        return GetOrSet(key, () => _inner.ListPrsAsync(repo, status, creator, org, project, top, skip, ct), ct);
    }

    public Task<JsonElement> ListBuildsAsync(int? top, int? definition, string? org, string? project, CancellationToken ct = default)
    {
        var key = $"builds:{top}:{definition}:{org}:{project}";
        return GetOrSet(key, () => _inner.ListBuildsAsync(top, definition, org, project, ct), ct);
    }

    public Task<JsonElement> GetBuildStatusAsync(int id, string? org, string? project, CancellationToken ct = default)
    {
        var key = $"build:{id}:{org}:{project}";
        return GetOrSet(key, () => _inner.GetBuildStatusAsync(id, org, project, ct), ct);
    }

    // ── Passthrough write operations ────────────────────

    public Task<JsonElement> CreateWiAsync(string type, string title, string? description, string? assignedTo,
        int? parent, string? iteration, string? org, string? project, CancellationToken ct = default)
        => _inner.CreateWiAsync(type, title, description, assignedTo, parent, iteration, org, project, ct);

    public Task<JsonElement> UpdateWiAsync(int id, string? state, string? title, string? assignedTo,
        int? parent, string? iteration, string? org, string? project, CancellationToken ct = default)
        => _inner.UpdateWiAsync(id, state, title, assignedTo, parent, iteration, org, project, ct);

    public Task<JsonElement> CommentWiAsync(int id, string text, string? org, string? project, CancellationToken ct = default)
        => _inner.CommentWiAsync(id, text, org, project, ct);

    public Task<JsonElement> CreatePrAsync(string title, string? repo, string? source, string? target,
        string? description, bool draft, int[] workItems, string[] reviewers,
        string? org, string? project, CancellationToken ct = default)
        => _inner.CreatePrAsync(title, repo, source, target, description, draft, workItems, reviewers, org, project, ct);

    public Task<JsonElement> TriggerBuildAsync(int definition, string? branch, string? org, string? project, CancellationToken ct = default)
        => _inner.TriggerBuildAsync(definition, branch, org, project, ct);

    public string? DetectOrgUrl(string org) => _inner.DetectOrgUrl(org);
}
