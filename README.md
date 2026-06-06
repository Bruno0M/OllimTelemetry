# ollim-telemetry

A lightweight background daemon that watches Claude Code's local logs, tracks your token usage, and (with opt-in) submits anonymized counts to [ollim.dev](https://ollim.dev) for leaderboard comparison.

Built with NativeAOT — ~9 MB binary, 23 ms startup, ~10 MB steady-state RAM.

## Install

**MacOS/Linux (Recommended):**

```bash
curl -fsSL https://ollim.dev/install.sh | bash
```

**Npm:**

```bash
npm install -g ollim-telemetry
```
> Requires Node ≥ 18.

**Nuget:**

```bash
dotnet tool install -g ollim-telemetry
```
> Requires .NET 10 SDK.

**Pre-built Binaries**

Download from [releases](https://github.com/Bruno0M/OllimTelemetry/releases):

- Linux: `ollim-linux-x64.tar.gz` / `ollim-linux-arm64.tar.gz`
- macOS: `ollim-osx-arm64.tar.gz`

## Quick start

```bash
ollim start      # first run triggers opt-in flow, then registers as a system service
ollim status     # show daemon state, sharing settings, pending queue
ollim stats      # token usage breakdown for the last 7 days
ollim leaderboard  # community token leaderboard (coming soon)
```

The daemon registers itself as a **launchd** service on macOS or a **systemd --user** service on Linux, so it survives reboots without any extra configuration.

## How it works

1. Watches `~/.claude/projects/**/*.jsonl` for new Claude Code log entries
2. Parses only the `usage` field (input/output/cache tokens) — message content is never read
3. Accumulates token counts in a local SQLite queue at `~/.local/share/ollim/queue.db`
4. Periodically (default: every 5 minutes) flushes the queue to `api.ollim.dev` if sharing is enabled
5. HTTP failures are retried with exponential backoff; the daemon never crashes on network issues

## Privacy

- Only token counts are collected: `input_tokens`, `output_tokens`, `cache_read_tokens`, `cache_write_tokens`
- Message content, prompts, and responses are never read or transmitted
- Sharing is **disabled by default** — the first-run flow asks for explicit consent
- You can share your repo name for leaderboard context, but it is also opt-in
- A random `UserId` (UUID) is generated locally; no account or email required

## Commands

| Command | Description |
|---|---|
| `ollim start` | Register daemon as a system service (runs onboarding on first use) |
| `ollim stop` | Unregister and stop the daemon |
| `ollim status` | Show daemon state, sharing config, and pending batch count |
| `ollim stats` | Token usage table for the last 7 days |
| `ollim leaderboard` | Community leaderboard |
| `ollim config` | Open `~/.config/ollim/config.json` in `$VISUAL`/`$EDITOR`/`vi` |
| `ollim unlink` | Disable sharing while keeping local data and UserId |
| `ollim uninstall` | Stop daemon and delete all local data |

## Configuration

Config lives at `~/.config/ollim/config.json`:

```json
{
  "ShareGlobal": false,
  "ShareRepoName": false,
  "SyncIntervalMinutes": 5,
  "Agent": "claude-code",
  "BackendUrl": "https://api.ollim.dev"
}
```

Run `ollim config` to open it directly, or edit it by hand.

## Building from source

Requires .NET 10 SDK. Linux NativeAOT additionally requires `clang` and `zlib1g-dev`.

```bash
# Build and run tests
dotnet build
dotnet test

# Run the CLI without NativeAOT (fast iteration)
dotnet run --project src/OllimTelemetry.Cli -- status
dotnet run --project src/OllimTelemetry.Cli -- --run-daemon

# Publish a NativeAOT binary for a single RID
dotnet publish src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj \
  -c Release -r linux-x64 --self-contained true /p:PublishAot=true -o dist/linux-x64

# Build all platforms (requires dotnet-script)
dotnet script scripts/build.cs
```

## Supported Agents

| Agent | Status |
|---|---|
| [Claude Code](https://claude.ai/code) | ✅ Supported |
| [Codex CLI](https://github.com/openai/codex) | 🚧 Coming soon |
| [Gemini CLI](https://github.com/google-gemini/gemini-cli) | 🚧 Coming soon |
| [Cursor](https://cursor.com) | 🚧 Coming soon |
| [GitHub Copilot](https://github.com/features/copilot) | 🚧 Coming soon |

## License

[MIT](./LICENSE)
