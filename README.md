<div align="center">
  <img src="assets/logo.svg" width="120" alt="ollim" />
  <h1>ollim-telemetry</h1>
  <p>Track token usage across your AI coding sessions — locally, privately, with opt-in leaderboard.</p>

  [![NuGet](https://img.shields.io/nuget/v/ollim-telemetry?label=NuGet&color=5c2d91)](https://www.nuget.org/packages/ollim-telemetry)
  [![npm](https://img.shields.io/npm/v/ollim-telemetry?label=npm&color=cb3837)](https://www.npmjs.com/package/ollim-telemetry)
  [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](./LICENSE)
  [![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20macOS-lightgrey)](https://github.com/Bruno0M/OllimTelemetry/releases)

  [Português](./README.pt-br.md)
</div>

---

A lightweight CLI that hooks into AI coding agents, reads only the `usage` field from their local logs, and (with opt-in) submits anonymized token counts to [ollim.dev](https://ollim.dev) for leaderboard comparison. No background daemon — everything runs via Stop hooks.

Built with NativeAOT — **~9 MB binary, ~5 ms startup, ~10 MB RAM**.

## Install

**macOS / Linux (recommended)**

```bash
curl -fsSL https://ollim.dev/install.sh | bash
```

**npm**

```bash
npm install -g ollim-telemetry
```

> Requires Node ≥ 18.

**NuGet**

```bash
dotnet tool install -g ollim-telemetry
```

> Requires .NET 10 SDK.

**Pre-built binaries**

Download from [Releases](https://github.com/Bruno0M/OllimTelemetry/releases):

| Platform | File |
|---|---|
| Linux x64 | `ollim-linux-x64.tar.gz` |
| Linux arm64 | `ollim-linux-arm64.tar.gz` |
| macOS arm64 | `ollim-osx-arm64.tar.gz` |

## Quick start

```bash
ollim start   # register hooks for all detected agents (runs opt-in flow on first use)
ollim login   # link your GitHub account — required to send data to the leaderboard
ollim status  # confirm hooks are active and sharing is enabled
```

> `ollim start` tracks your sessions locally right away. `ollim login` is only needed if you want to sync data and appear on the leaderboard.

## How it works

1. `ollim start` registers a **Stop hook** in `~/.claude/settings.json` (Claude Code) and `~/.codex/hooks.json` (Codex, if installed)
2. After every session, the hook reads only the `usage` field from the agent's log file — message content is never read
3. Token counts are stored in a local SQLite queue at `~/.local/share/ollim/queue.db`
4. The hook then attempts to flush the queue to `api.ollim.dev` if sharing is enabled
5. HTTP failures are retried with exponential backoff on the next invocation — nothing is lost

## Privacy

- Only token counts are collected: `input_tokens`, `output_tokens`, `cache_read_tokens`, `cache_write_tokens`
- Message content, prompts, and responses are **never read or transmitted**
- Sharing is **disabled by default** — the first-run flow asks for explicit consent
- Sharing requires a GitHub account — run `ollim login` after opting in
- You can share your repo name for leaderboard context, but it is also opt-in
- A random UUID is generated locally and linked to your GitHub account

## Commands

| Command | Description |
|---|---|
| `ollim start` | Register hooks for all detected agents (runs onboarding on first use, backfills history) |
| `ollim stop` | Unregister all hooks and flush any pending batches |
| `ollim status` | Show hook state, sharing config, and pending batch count |
| `ollim login` | Link a GitHub account (required to sync data) |
| `ollim logout` | Unlink GitHub account and disable leaderboard sharing |
| `ollim submit` | Manually flush all pending sessions to the backend (resets retry timers) |
| `ollim config` | Open `~/.config/ollim/config.json` in `$VISUAL`/`$EDITOR`/`vi` |
| `ollim uninstall` | Unregister the hook and delete all local data |

## Configuration

Config lives at `~/.config/ollim/config.json` (XDG Base Directory spec):

```json
{
  "ShareGlobal": false,
  "ShareRepoName": false,
  "SyncIntervalMinutes": 5,
  "Agent": "claude-code",
  "BackendUrl": "https://api.ollim.dev"
}
```

Run `ollim config` to open it directly, or edit by hand.

## Building from source

Requires .NET 10 SDK. Linux NativeAOT additionally requires `clang` and `zlib1g-dev`.

```bash
# Build and run tests
dotnet build
dotnet test

# Run the CLI without NativeAOT (fast iteration)
dotnet run --project src/OllimTelemetry.Cli --launch-profile dev -- status

# Publish a NativeAOT binary for a single RID
dotnet publish src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj \
  -c Release -r linux-x64 --self-contained true /p:PublishAot=true -o dist/linux-x64

# Build all platforms (requires dotnet-script)
dotnet script scripts/build.cs
```

## Supported agents

| Agent | Status |
|---|---|
| [Claude Code](https://claude.ai/code) | ✅ Supported |
| [Codex CLI](https://github.com/openai/codex) | ✅ Supported |
| [Gemini CLI](https://github.com/google-gemini/gemini-cli) | 🚧 Coming soon |
| [Cursor](https://cursor.com) | 🚧 Coming soon |
| [GitHub Copilot](https://github.com/features/copilot) | 🚧 Coming soon |

## License

[MIT](./LICENSE)
