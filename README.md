# AzDo

Azure DevOps CLI and MCP Server.

Manage work items, pull requests, pipelines, repos, boards and wikis from the command line, or let AI agents (Claude, GitHub Copilot) do it through the Model Context Protocol. 69 MCP tools. Central configuration, no per-repo files.

## Install

Self-contained binaries (no .NET required) on [GitHub Releases](https://github.com/LuPaLa-Coder/azdo/releases/latest):

```bash
# Choose: azdo-macos-arm64 | azdo-linux-x64 | azdo-linux-arm64 | azdo-windows-x64.exe
curl -L -o azdo https://github.com/LuPaLa-Coder/azdo/releases/latest/download/azdo-macos-arm64
chmod +x azdo && sudo cp azdo /usr/local/bin/
azdo --version
```

Or as a .NET global tool:

```bash
dotnet tool install -g AzDo
```

Register your organization with a Personal Access Token (PAT):

```bash
azdo config add --org myorg --pat <token>
```

The organization and project are detected from the git remote, or from a `.azdo` file outside a git repo. Override them with `--org` and `--project`.

## CLI examples

```bash
# Work items: run a WIQL query and create a task
azdo wi query --wiql "SELECT [System.Id] FROM WorkItems WHERE [System.State] = 'Active'" --table
azdo wi create --type Task --title "Fix login bug" --assigned-to me@example.com

# Pull requests: list the active ones and open a new one
azdo pr list --status active --table
azdo pr create --title "Add caching" --source feature/cache --target main

# Pipelines: list them and trigger a run
azdo pipeline list --table
azdo build trigger --definition 5 --branch refs/heads/main
```

Every read command accepts `--table` for a terminal-friendly table instead of JSON.

## MCP server

Start it over stdio (default) or HTTP:

```bash
azdo mcp serve
azdo mcp serve --transport http --port 9287
```

Register the organization from a normal shell first: in MCP mode there is no TTY for prompts.

### Claude Desktop

`claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "azdo": { "command": "azdo", "args": ["mcp", "serve"] }
  }
}
```

### Claude Code

```bash
claude mcp add azdo -- azdo mcp serve
```

### GitHub Copilot CLI

`~/.config/github-copilot/mcp-servers.json`:

```json
{
  "mcpServers": {
    "azdo": { "command": "azdo", "args": ["mcp", "serve"] }
  }
}
```

## Documentation

Full command and MCP tool reference: [COMMANDS.md](https://github.com/LuPaLa-Coder/azdo/blob/main/COMMANDS.md).

## Related tools

- [JiraCli-Mcp](https://www.nuget.org/packages/JiraCli-Mcp) — Jira CLI and MCP server
- [SharpGuard](https://www.nuget.org/packages/SharpGuard) — .NET SAST/SCA security scanner

## License

MIT
