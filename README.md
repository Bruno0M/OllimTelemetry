# ollim-telemetry

A lightweight CLI tool that hooks into Claude Code's Stop event, parses your local token-usage logs, and (with opt-in) submits anonymized counts to [ollim.dev](https://ollim.dev) for leaderboard comparison.

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
ollim start      # first run triggers opt-in flow and registers the Claude Code Stop hook
ollim status     # show hook state, sharing settings, and pending sync queue
```

## How it works

1. `ollim start` registers itself as a **Stop hook** in `~/.claude/settings.json`
2. After every Claude Code session, the hook reads only the `usage` field from the JSONL file (input/output/cache tokens) — message content is never read
4. Counts are stored in a local SQLite queue at `~/.local/share/ollim/queue.db`
5. The hook then attempts to flush the queue to `api.ollim.dev` if sharing is enabled
6. HTTP failures are retried with exponential backoff on the next hook invocation — nothing is lost

## Privacy

- Only token counts are collected: `input_tokens`, `output_tokens`, `cache_read_tokens`, `cache_write_tokens`
- Message content, prompts, and responses are never read or transmitted
- Sharing is **disabled by default** — the first-run flow asks for explicit consent
- Sharing requires a GitHub account — run `ollim login` after opting in
- You can share your repo name for leaderboard context, but it is also opt-in
- A random `UserId` (UUID) is generated locally and linked to your GitHub account

## Commands

| Command | Description |
|---|---|
| `ollim start` | Register the Claude Code Stop hook (runs onboarding on first use, backfills history) |
| `ollim stop` | Unregister the hook and flush any pending batches |
| `ollim status` | Show hook state, sharing config, and pending batch count |
| `ollim login` | Link a GitHub account (required to sync data) |
| `ollim logout` | Unlink GitHub account and disable leaderboard sharing |
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

Run `ollim config` to open it directly, or edit it by hand.

## Building from source

Requires .NET 10 SDK. Linux NativeAOT additionally requires `clang` and `zlib1g-dev`.

```bash
# Build and run tests
dotnet build
dotnet test

# Run the CLI without NativeAOT (fast iteration)
# launchSettings.json auto-sets OLLIM_ENV=dev, OLLIM_BACKEND_URL=http://localhost:5000, and isolated XDG paths (/tmp/ollim-dev)
dotnet run --project src/OllimTelemetry.Cli --launch-profile dev -- status
dotnet run --project src/OllimTelemetry.Cli --launch-profile dev -- start

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
