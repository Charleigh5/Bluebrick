# Command Execution Recovery

This document is the repo-local recovery path for command execution while the Codex desktop PowerShell bridge remains unreliable.

## Why This Exists

The current blocker is the Codex desktop PowerShell host failing before command execution. The safest recovery pattern is:

1. Use PowerShell 7 in Visual Studio Code for manual build, run, and debug work.
2. Add official MCP servers for capabilities Codex can consume safely.
3. Keep skills for workflow packaging only, not for command-execution privileges.

This matches the official sources that support the chosen pattern:

- The official VS Code PowerShell extension is `ms-vscode.PowerShell`: [PowerShell/vscode-powershell](https://github.com/PowerShell/vscode-powershell)
- The official filesystem MCP package is `@modelcontextprotocol/server-filesystem`: [npm package](https://www.npmjs.com/package/@modelcontextprotocol/server-filesystem)
- The official servers collection includes `MCPShell`: [modelcontextprotocol/servers](https://github.com/modelcontextprotocol/servers)

## What Has Already Been Applied

### 1. VS Code workspace recommendations

The workspace now recommends the official PowerShell extension:

- [extensions.json](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/.vscode/extensions.json)

The workspace also prefers PowerShell 7 for the integrated terminal if it is installed at the standard Windows location:

- [settings.json](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/.vscode/settings.json)

### 2. Codex MCP filesystem server

The Codex user config was updated to add the official filesystem MCP server pointed at:

- `C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick`
- `C:\Users\cweir\.codex`

That change lives in:

- [config.toml](/C:/Users/cweir/.codex/config.toml)

Codex must be restarted before that MCP server is available to new sessions.

## What Still Must Be Done Manually

### Install PowerShell 7

Install PowerShell 7 from Microsoft and confirm `pwsh` works outside Codex.

Recommended validation:

```powershell
pwsh -NoLogo -NoProfile -Command "$PSVersionTable.PSVersion"
```

### Install the VS Code PowerShell extension

Open this workspace in VS Code and install the recommended extension, or install it directly:

```powershell
code --install-extension ms-vscode.PowerShell
```

After installation:

1. Open the BlueBrick workspace in VS Code.
2. Accept the recommended extension.
3. Confirm the integrated terminal launches `pwsh.exe`.

### Optional: Add an MCP shell server

Do not enable a shell MCP server until the manual PowerShell 7 path is working.

When you are ready, use the template in:

- [mcp-shell.template.toml](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/docs/mcp-shell.template.toml)

Copy the block into:

- [config.toml](/C:/Users/cweir/.codex/config.toml)

Then restart Codex and validate the shell server with a harmless command first.

## Validation Order

Use this order only:

1. Confirm `pwsh` works outside Codex.
2. Confirm VS Code uses `pwsh`.
3. Confirm the filesystem MCP server appears after restarting Codex.
4. Only then consider enabling the shell MCP server template.

## Why The Shell Server Was Not Enabled Automatically

The shell server was intentionally left as a template instead of being activated directly because:

- the current command host is already failing in this environment
- a shell MCP server should not be enabled blindly without verifying the executable and trust boundaries
- the filesystem MCP server already provides the safer first step for repo operations

## Expected Outcome

If the above steps succeed, you get:

- a stable manual execution path in VS Code through PowerShell 7
- safe Codex-side file access through the official MCP filesystem server
- a staged path to agent-routable shell execution only after the environment is stable
