# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Ollim Telemetry** is a NativeAOT CLI tool (`ollim`) that hooks into AI coding agents (Claude Code, Codex CLI) to extract token usage from local JSONL logs and (with explicit opt-in) submits anonymized data to `api.ollim.dev`. There is no background daemon — everything runs via Stop hooks. Config lives in `~/.config/ollim/` and the SQLite queue in `~/.local/share/ollim/` (XDG Base Directory spec).

## Commands

```bash
# Build & test
dotnet build                              # build all projects
dotnet test                               # run all tests (xunit)
dotnet test --filter "FullyQualifiedName~LogParser"  # run a single test class

# Run the CLI directly (no NativeAOT, fast iteration)
# launchSettings.json auto-sets OLLIM_ENV=dev, OLLIM_BACKEND_URL=http://localhost:5000, and isolated XDG paths (/tmp/ollim-dev)
dotnet run --project src/OllimTelemetry.Cli --launch-profile dev -- status
dotnet run --project src/OllimTelemetry.Cli --launch-profile dev -- start   # registers the Stop hook + backfills
echo '{"session_id":"test","transcript_path":"/path/to/file.jsonl"}' | \
  dotnet run --project src/OllimTelemetry.Cli --launch-profile dev -- hook  # simulate a Stop hook invocation

# NativeAOT publish (single RID)
dotnet publish src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj \
  -c Release -r linux-x64 --self-contained true /p:PublishAot=true -o dist/linux-x64

# Build all RIDs (C# script runner)
dotnet script scripts/build.cs
```

Linux NativeAOT requires `clang` and `zlib1g-dev`.

## Architecture

```
Cli → Core → Models
Cli → Models
```

- **Models** — pure data records (`TokenUsage`, `SyncBatch`, `SubmitPayload`, `AppConfig`). No dependencies.
- **Core** — all engine logic. Must never reference Spectre.Console.
  - `Config/` — `AppConfig` + `ConfigManager` (reads/writes `~/.config/ollim/config.json` via source-gen JSON; `OLLIM_BACKEND_URL` env var overrides `BackendUrl` at runtime)
  - `Parsing/LogParser` — JSONL delta reader; takes a byte offset, returns new records and updated offset
  - `Queue/SyncQueue` — SQLite-backed queue (`~/.local/share/ollim/queue.db`); stores file offsets and pending batches
  - `Sync/SyncService` — single-pass flush (`FlushOnceAsync`) of `SyncQueue` to `POST /v1/submit` with exponential backoff
  - `Hook/ClaudeHookManager` — reads/writes `~/.claude/settings.json` to register/unregister the Stop hook
  - `Hook/CodexHookManager` — reads/writes `~/.codex/hooks.json` to register/unregister the Codex hook
  - `Parsing/CodexLogParser` — parses Codex session JSONL files (cumulative token model, unlike Claude Code's delta model)
  - `Ingestion/LogIngester` — processes a single JSONL file delta or backfills all files; has Codex-specific `BackfillCodex` / `ProcessCodexFile` methods
- **Cli** — entry point, ConsoleAppFramework routing, all terminal I/O
  - `Program.cs` — runs `XdgMigration`, schedules update check, routes verbs via ConsoleAppFramework, prints update notice on exit
  - `Commands/` — one static class per verb (`start`, `stop`, `status`, `config`, `stats`, `leaderboard`, `login`, `logout`, `uninstall`, `hook`)
  - `Commands/HookCommand` — invoked by agent Stop hooks; routes via `--agent` flag (claude-code | codex); reads stdin JSON for Claude Code, scans Codex sessions directory for Codex
  - `Commands/SubmitCommand` — hidden command for manual batch submission
  - `Auth/AuthDtos.cs` — `CliInitResponse`, `CliPollResponse` for GitHub OAuth flow
  - `Onboarding/OnboardingFlow` — first-run consent prompt and hook registration
  - `Update/UpdateChecker` — background version check; prints notice after command completes

## Key constraints

- **Privacy** — `LogParser` reads only the `usage` field. Mark every new property access on a `JsonDocument` with `// PRIVACY: usage only`.
- **NativeAOT** — no runtime reflection. All JSON serialization must go through source-gen contexts (`ConfigJsonContext`, `CliJsonContext`). Do not add `JsonSerializer` calls without a matching `[JsonSerializable]` attribute.
- **Layer isolation** — `Core` has no reference to Spectre.Console or ConsoleAppFramework. Keep all terminal I/O in `Cli`.
- **Offline-safe** — HTTP failures must never crash the hook process; failed batches stay in SQLite with retry state.
- **Codex delta model** — Codex logs are cumulative (each entry is a running total), unlike Claude Code's per-message deltas. `SyncQueue` stores `last_input_tokens`, `last_output_tokens`, `last_cache_tokens` baselines per file to compute the correct delta. Never use `LogParser` on Codex files.
