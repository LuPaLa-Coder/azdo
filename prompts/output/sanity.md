# SANITY CHECK FINALE

## ✅ dotnet build -c Release

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Entrambi i progetti (`DevOpsCli`, `DevOpsCli.Tests`) compilano senza errori né warning nuovi.

---

## ✅ dotnet test -c Release --nologo

```
Test summary: total: 35, failed: 0, succeeded: 35, skipped: 0, duration: 1.1s
Build succeeded in 3.8s
```

35 test passati, 0 falliti:

| Suite | Test |
|-------|------|
| `OrgDetectorTests` | 9 test (4 formati remote, user@, DefaultCollection, malformed ×4, URL-encode, no-git) |
| `ConfigStoreTests` | 5 test (default, round-trip, atomic write, empty JSON, Unix perms) |
| `CentralConfigTests` | 4 test (case-insensitive lookup, round-trip JSON, JsonPropertyNames, default values) |
| `McpToolsTests` | 8 test (count 11, nomi attesi, no null, no empty desc, type:object, required consistent, handler non-null, nomi univoci) |
| `AzureDevOpsClientTests` | 6 test (Basic auth, ASCII encoding, no trailing newline, trim slash, OrgUrl unchanged, Accept header) |

---

## ✅ azdo mcp list-tools → 11 tool

```
azdo_context
azdo_wi_query
azdo_wi_get
azdo_wi_create
azdo_wi_update
azdo_wi_comment
azdo_repo_list
azdo_pr_list
azdo_build_list
azdo_build_status
azdo_build_trigger
```

---

## Modifiche apportate al codebase

| File | Tipo | Motivo |
|------|------|--------|
| `src/DevOpsCli/AzureDevOps/ClientFactory.cs` | Fix | CS1674: `Session` implementa ora `IDisposable` con commento di guardia `with` |
| `Directory.Build.props` | Infra | Redirect obj/bin su Linux filesystem per NTFS/WSL (MSB3374, MSB4018) |
| `src/DevOpsCli.Tests/DevOpsCli.Tests.csproj` | Nuovo | Progetto xUnit + TestSdk + coverlet |
| `src/DevOpsCli.Tests/OrgDetectorTests.cs` | Nuovo | 9 test per tutti i formati di remote |
| `src/DevOpsCli.Tests/ConfigStoreTests.cs` | Nuovo | 5 test per load/save/perms |
| `src/DevOpsCli.Tests/CentralConfigTests.cs` | Nuovo | 4 test per config model |
| `src/DevOpsCli.Tests/McpToolsTests.cs` | Nuovo | 8 test per il registry MCP |
| `src/DevOpsCli.Tests/AzureDevOpsClientTests.cs` | Nuovo | 6 test per auth header |
| `DevOpsCli.sln` | Aggiornato | Aggiunto progetto DevOpsCli.Tests |

## Finding aperti (non bloccanti)

- **[HIGH]** `HttpClient` per-request nel loop MCP → socket exhaustion — issue separata
- **[HIGH]** PAT in chiaro su `config.json` (CWE-312) — issue separata
- **[MEDIUM]** `record Session` → `class Session` per semantica IDisposable corretta — tech debt
