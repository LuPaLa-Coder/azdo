# 1. smoke test offline
./azdo mcp serve < integration/smoke-test.jsonl

# 2. config Copilot CLI
copilot --help | grep -i mcp   # trova il path del config
# Copia il blocco mcpServers da integration/copilot-cli-mcp.json

# 3. setup PAT (una sola volta)
./azdo config add --org <tua-org> --pat <token>

# 4. da dentro Copilot CLI in una repo Azure DevOps:
#    "lista le PR attive di questo repo"