# DIAGNOSI — Iterazione 1

## Sommario

**20 errori CS1674**, nessun warning. Unica causa radice.

---

## Errore CS1674 — `Session` non implementa `IDisposable`

**Messaggio**: `'Session': type used in a using statement must implement 'System.IDisposable'.`

### File colpiti

| File | Righe |
|------|-------|
| `Commands/WorkItemCommand.cs` | 31, 47, 80, 136, 179 |
| `Commands/BuildCommand.cs` | 28, 46, 65 |
| `Commands/RepoCommand.cs` | 19 |
| `Commands/PullRequestCommand.cs` | 26 |
| `Mcp/McpTools.cs` | 105, 124, 148, 194, 229, 244, 263, 300, 320, 339 |

### Causa esatta

In `AzureDevOps/ClientFactory.cs` riga 6, `Session` è dichiarato come `sealed record`:

```csharp
public sealed record Session(AzureDevOpsClient Client, string Org, string? Project, DetectedContext? Detected);
```

I `record` in C# **non implementano automaticamente `IDisposable`**.  
Tutti i siti di chiamata usano `using var session = ClientFactory.OpenSession(...)`,
che richiede che il tipo implementi `System.IDisposable`.

`AzureDevOpsClient` (campo `Client`) implementa già `IDisposable` (dispone l'`HttpClient` interno),
ma `Session` non delega la dispose.

### Riferimento

- [CS1674 — MSDN](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/cs1674)
- [IDisposable pattern](https://learn.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose)
