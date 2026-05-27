# Prompt — Build loop con revisione critica preliminare e unit test

> Copia/incolla questo prompt nel tuo agente di coding (Copilot CLI, Claude, Cursor, …).
> L'agente esegue **direttamente** `dotnet build` nella working copy: non incollare log a mano.
>
> **Tutti gli artefatti generati dall'agente (log di build, diff proposti, report di revisione,
> output di `dotnet test`, sanity check) vanno scritti sotto `prompts/output/`.**
> La cartella `prompts/` è esclusa da git (vedi `.gitignore`), quindi non inquina il repo.

---

## Contesto

Sto sviluppando un CLI in **C# / .NET 8** chiamato `azdo`, basato su `System.CommandLine 2.0.0-beta4.22272.1`.
Lo scaffold completo è in `output/devops-cli/` ed è strutturato così:

```
src/DevOpsCli/
├── Program.cs
├── Config/        (CentralConfig, ConfigStore, PatPrompt)
├── Context/       (OrgDetector — parsing git remote → org)
├── AzureDevOps/   (AzureDevOpsClient HttpClient+PAT, ClientFactory)
├── Commands/      (Context, Config, WorkItem, Repo, PullRequest, Build, Mcp)
└── Mcp/           (McpServer loop JSON-RPC stdio, McpTools registry 12 tool)
```

Il binario fa due cose nello stesso eseguibile:

1. **CLI standalone** (`azdo wi query …`, `azdo build trigger …`, ecc.)
2. **MCP server su stdio** (`azdo mcp serve`) consumato da GitHub Copilot CLI / Claude Desktop

Modello di config: file centrale `~/.config/devops-cli/config.json` (perms 600),
org dedotta dal `git remote` della cwd, PAT chiesto interattivamente solo in modalità TTY.

## Task

### 1. Build
Esegui `dotnet build -c Release` nella working copy. **Non chiedere all'utente di incollare nulla** — leggi tu l'output del comando.

### 2. Diagnosi
Se la build fallisce, identifica **tutti** gli errori (CS####) e i warning rilevanti. Per ciascuno:
- File:riga
- Causa esatta (API mancante, type mismatch, overload ambiguo, namespace, nullable, package version, ecc.)
- Riferimento alla doc ufficiale se serve (link MSDN / GitHub)

### 3. Proposta di fix
Per ogni errore, indica la modifica **minima** che lo risolve. Mostra il diff in formato unified
(`--- old`, `+++ new`, hunk con contesto).

### 4. ⛔ Revisione critica — BLOCCANTE

**Ma prima di applicarla voglio una revisione critica da un secondo modello. Analizza rischi, alternative e impatti.**

Non scrivere/modificare alcun file finché non hai completato questa sezione.
Per **ogni** modifica proposta produci:

| Sezione        | Cosa deve contenere |
|----------------|---------------------|
| **Rischi**     | Cosa può rompersi a runtime, in altri file, in scenari edge (Unix vs Windows, MCP vs CLI, repo senza `.git`, stdin redirected, PAT scaduto, Azure DevOps `*.visualstudio.com` legacy, ecc.). Cita la riga di codice impattata. |
| **Alternative** | Almeno **una** strada diversa con tradeoff espliciti (es. bump versione package vs adattare API; refactor vs workaround; nuova dipendenza vs codice inline). Spiega perché la tua scelta è migliore. |
| **Impatti**    | File toccati, contratto pubblico modificato, backward compatibility (CLI args / config schema / MCP tool schema), dimensione binario, nuove dipendenze transitive, security surface (perms file, auth headers, logging segreti). |
| **Verifica**   | Comando o test concreto che dimostra che il fix funziona (`dotnet build`, smoke test MCP via `integration/smoke-test.jsonl`, chiamata reale con PAT di test, ecc.). |

### 5. Applicazione e loop
Applica le modifiche, poi **ri-esegui** `dotnet build -c Release`:
- ❌ Se ci sono ancora errori → **ricomincia dal punto 2** (Diagnosi → Proposta → Revisione critica → Applicazione). Itera finché la build non è pulita.
- ✅ Se 0 errori e 0 warning *nuovi* introdotti → passa al punto 6.

### 6. Unit test
Build pulita. Adesso:
1. Crea un progetto di unit test (`src/DevOpsCli.Tests/`) con **xUnit** + `Microsoft.NET.Test.Sdk` + `coverlet.collector`, referenziando `DevOpsCli.csproj`.
2. Aggiungilo alla solution (`DevOpsCli.sln`).
3. Scrivi unit test mirati ai punti critici, almeno:
   - `OrgDetector` — parsing di tutti e 4 i formati di remote (`dev.azure.com` https/ssh, `*.visualstudio.com` https/ssh), repo senza `.git`, remote malformato.
   - `ConfigStore` — load/save round-trip, file inesistente → default, atomic write (`.tmp` + rename), perms 600 su Unix (test condizionato a `OperatingSystem.IsLinux() || IsMacOS()`).
   - `CentralConfig` — case-insensitive lookup org, serializzazione JSON stabile.
   - `McpTools` — registry contiene tutti i 12 tool attesi, ogni schema ha `type:"object"` e i `required` consistenti.
   - `AzureDevOpsClient` — costruzione header `Authorization: Basic base64(":{PAT}")`, encoding ASCII, niente trailing newline.
4. Esegui `dotnet test -c Release --nologo`. Tutti i test devono passare.
5. Se un test fallisce: decidi se il bug è nel test o nel codice di produzione e **ricomincia dal punto 2** per il fix appropriato.

### 7. Sanity check finale
- `dotnet build -c Release` → 0 errori, 0 warning nuovi.
- `dotnet test -c Release --nologo` → tutti pass.
- `azdo mcp list-tools` → 12 tool elencati.

## Vincoli

- ❌ **Nessuna dipendenza nuova** in `DevOpsCli.csproj` senza giustificarla nella revisione (per il progetto di test xUnit + Microsoft.NET.Test.Sdk + coverlet.collector sono OK by default).
- ❌ **Nessun refactor opportunistico**: cambia solo ciò che serve per buildare/testare.
- ❌ **Niente downgrade silenzioso** di .NET 8 o di `System.CommandLine 2.0-beta4`.
- ✅ Preserva la separazione di cartelle `Mcp/`, `Config/`, `Commands/`, `AzureDevOps/`, `Context/`.
- ✅ Mantieni il contratto MCP wire format (line-delimited JSON-RPC 2.0 su stdio).
- ✅ Tutto il logging diagnostico resta su `stderr` — `stdout` riservato a JSON-RPC.

## Output atteso (in quest'ordine, niente skip)

In chat mostra le sezioni qui sotto; **in parallelo salva i file corrispondenti sotto `prompts/output/`**
(cartella esclusa da git via `.gitignore` — non viene committata).

| Sezione chat            | File salvato                              |
|-------------------------|-------------------------------------------|
| `## BUILD`              | `prompts/output/build-<N>.log`            |
| `## DIAGNOSI`           | `prompts/output/diagnosi-<N>.md`          |
| `## PROPOSTE`           | `prompts/output/proposte-<N>.diff`        |
| `## REVISIONE CRITICA`  | `prompts/output/revisione-<N>.md`         |
| `## APPLICAZIONE`       | `prompts/output/applicazione-<N>.log`     |
| `## LOOP`               | (incrementa `<N>` e ricomincia da BUILD)  |
| `## UNIT TEST`          | `prompts/output/test.log`                 |
| `## SANITY CHECK`       | `prompts/output/sanity.md`                |

`<N>` è il numero dell'iterazione del loop (1, 2, 3, …): ogni passaggio di fix produce un set di
file numerato, così resta tracciabile come si è arrivati alla build pulita.
