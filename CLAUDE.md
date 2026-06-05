# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

**Ollim Telemetry** is a NativeAOT CLI daemon (`ollim`) that watches Claude Code's local JSONL logs (`~/.claude/projects/**/*.jsonl`), extracts token usage counts, and (with explicit opt-in) submits anonymized data to `api.ollim.dev`. Config and SQLite queue live in `~/.ollim/`.

## Commands

```bash
# Build & test
dotnet build                              # build all projects
dotnet test                               # run all tests (xunit)
dotnet test --filter "FullyQualifiedName~LogParser"  # run a single test class

# Run the CLI directly (no NativeAOT, fast iteration)
dotnet run --project src/OllimTelemetry.Cli -- status
dotnet run --project src/OllimTelemetry.Cli -- --run-daemon  # run daemon in foreground

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
  - `Config/` — `AppConfig` + `ConfigManager` (reads/writes `~/.ollim/config.json` via source-gen JSON)
  - `Parsing/LogParser` — JSONL delta reader; takes a byte offset, returns new records and updated offset
  - `Queue/SyncQueue` — SQLite-backed queue (`~/.ollim/queue.db`); stores file offsets and pending batches
  - `Watching/LogWatcher` — wraps `FileSystemWatcher` with 500 ms debounce on `~/.claude/projects/`
  - `Sync/SyncService` — flushes `SyncQueue` to `POST /v1/submit` on a configurable interval with exponential backoff
  - `Daemon/DaemonManager` — installs/removes launchd (macOS) or systemd --user (Linux) service
- **Cli** — entry point, ConsoleAppFramework routing, all terminal I/O
  - `Program.cs` — `--run-daemon` flag triggers `DaemonRunner.RunAsync`; otherwise routes to commands
  - `Commands/` — one static class per verb (`start`, `stop`, `status`, `config`, `stats`, `leaderboard`, `unlink`, `uninstall`)
  - `Daemon/DaemonRunner` — backfills existing JSONL files on startup, starts watcher + sync service

## Key constraints

- **Privacy** — `LogParser` reads only the `usage` field. Mark every new property access on a `JsonDocument` with `// PRIVACY: usage only`.
- **NativeAOT** — no runtime reflection. All JSON serialization must go through source-gen contexts (`ConfigJsonContext`, `CliJsonContext`). Do not add `JsonSerializer` calls without a matching `[JsonSerializable]` attribute.
- **Layer isolation** — `Core` has no reference to Spectre.Console or ConsoleAppFramework. Keep all terminal I/O in `Cli`.
- **Offline-safe** — HTTP failures must never crash the daemon; failed batches stay in SQLite with retry state.
