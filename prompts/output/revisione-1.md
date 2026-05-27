# REVISIONE CRITICA — Iterazione 1

## Verdict

✅ **Fix approvato** — corretto, minimale, zero breaking change.
Aggiunta raccomandata: commento di guardia sul rischio `with` clone trap.

---

## Rischi

| # | Rischio | Severity | Blocca fix? |
|---|---------|----------|-------------|
| R1 | `record` + `with` clone trap: la copia condivide `Client`, dispose-arla invalida l'altra | MEDIUM | No — mitigabile con commento XML doc |
| R2 | `new HttpClient` per-request in MCP long-running → socket exhaustion TIME_WAIT su Linux in ~30 min | HIGH | No — finding separato |
| R3 | Console.IsInputRedirected: eccezione prima della costruzione del client, nessun leak | ✅ Safe | N/A |
| R4 | PAT in chiaro su disco in `CentralConfig.json` (CWE-312) | High | No — finding separato |

**Riga impattata R1:** `ClientFactory.cs:6` — dichiarazione del record.  
**Riga impattata R2:** `AzureDevOpsClient.cs:21` — `new HttpClient { ... }`.

La catena dispose è corretta:
```
using var session → session.Dispose() → Client.Dispose() → _http.Dispose()  // idempotente
```

`HttpClient.Dispose()` è idempotente su .NET 8 (flag `_disposed` interno), nessun double-dispose a runtime.

---

## Alternative

| Alternativa | Pro | Contro | Scelta |
|---|---|---|---|
| **Alt A — fix + XML doc comment** (raccomandata) | Zero costo, documenta il contratto | Solo documentazione, non impedisce `with` | ✅ Adottata |
| Alt B — `record → class` primary constructor | Semantica corretta per IDisposable, elimina `with` trap ed equality sorprendente | Diff più ampio (~10 righe), refactoring separato | Debt separato |
| Alt C — rimuovere tutti i 20 `using` | Build passa | Leak socket e memoria nel path MCP long-lived | ❌ Scartata |
| Alt D — `IAsyncDisposable` | Corretto per pattern async | `HttpClient.DisposeAsync` è wrapper sincrono, nessun beneficio; richiederebbe 20 `await using` | Skip |

---

## Impatti

- **File modificato:** `ClientFactory.cs` (unico)
- **File invariati:** tutti i 20 call site, `AzureDevOpsClient.cs`, `McpTools.cs`
- **Contratto pubblico:** cambiamento additivo (`Session` acquisisce `IDisposable`) — zero breaking change
- **Binario:** trascurabile (1 entry vtable + ~4 byte IL)
- **Sicurezza:** PAT in `_http.DefaultRequestHeaders` viene rilasciato correttamente allo scope exit, riducendo residenza in heap

---

## Verifica

```bash
# Deve terminare con 0 Error(s)
dotnet build src/DevOpsCli/DevOpsCli.csproj -c Release

# Conferma 20 call site coperti
grep -rn "using var" src/ | grep -c "ClientFactory"

# Smoke test MCP context
echo '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"azdo_context","arguments":{}}}' \
  | dotnet run --project src/DevOpsCli/DevOpsCli.csproj -- mcp
```

---

## Codice finale raccomandato

```csharp
/// <summary>
/// Represents an active Azure DevOps session. Owns the lifetime of <see cref="Client"/>.
/// </summary>
/// <remarks>
/// ⚠️ Do NOT use the <c>with</c> expression to clone a Session.
/// The copy would share the same <see cref="AzureDevOpsClient"/> instance;
/// disposing either copy would invalidate the other's HttpClient.
/// </remarks>
public sealed record Session(AzureDevOpsClient Client, string Org, string? Project, DetectedContext? Detected)
    : IDisposable
{
    public void Dispose() => Client.Dispose();
}
```

---

## Finding separati (non bloccanti per questo fix)

- **[HIGH]** `HttpClient` per-request in MCP loop → socket exhaustion — aprire issue separata
- **[HIGH]** PAT in chiaro su `CentralConfig.json` (CWE-312) — aprire issue separata
- **[MEDIUM]** `record → class` refactoring per semantica corretta — tech debt
