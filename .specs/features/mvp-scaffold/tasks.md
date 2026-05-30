# Tasks: MVP — Full Solution Scaffold + Implementation

**Feature:** mvp-scaffold
**Status:** Ready to execute

---

## Task Index

| ID | Title | Status | Deps |
|----|-------|--------|------|
| T-01 | Solution scaffold | Todo | — |
| T-02 | Models layer | Todo | T-01 |
| T-03 | Config layer | Todo | T-02 |
| T-04 | Log parser | Todo | T-02, T-03 |
| T-05 | SQLite queue | Todo | T-02 |
| T-06 | File watcher | Todo | T-05 |
| T-07 | Sync service | Todo | T-03, T-05 |
| T-08 | Daemon manager + OS templates | Todo | — |
| T-09 | Graceful shutdown | Todo | T-06, T-07, T-08 |
| T-10 | Onboarding flow | Todo | T-03, T-08 |
| T-11 | CLI commands | Todo | T-03, T-05, T-08, T-10 |
| T-12 | Program.cs wiring | Todo | T-11 |
| T-13 | Build script | Todo | T-12 |

---

## T-01 — Solution scaffold

**What:** Create the full directory tree, solution file, three `.csproj` files, project references, and NuGet package references exactly as specified.

**Where:**
- `/home/bruno/dev/OllimTelemetry/OllimTelemetry.sln`
- `src/OllimTelemetry.Models/OllimTelemetry.Models.csproj`
- `src/OllimTelemetry.Core/OllimTelemetry.Core.csproj`
- `src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj`
- `src/OllimTelemetry.Cli/TrimmerRoots.xml` (empty roots placeholder for NativeAOT)

**Depends on:** —

**Done when:**
- `dotnet restore OllimTelemetry.sln` succeeds
- `dotnet build OllimTelemetry.sln` succeeds (will have compile errors from missing source, but structural errors = fail)
- All 3 projects visible in `dotnet sln list`

**Gate:** `dotnet build OllimTelemetry.sln --no-restore` — zero structural errors

**Notes:**
- Use exact `.csproj` XML from brainstorm
- `TrimmerRoots.xml` must exist even if empty (Cli.csproj references it)
- Create `assets/` and `scripts/` directories (empty for now)

---

## T-02 — Models layer

**What:** Implement the 3 model records.

**Where:**
- `src/OllimTelemetry.Models/TokenUsage.cs`
- `src/OllimTelemetry.Models/SyncBatch.cs`
- `src/OllimTelemetry.Models/SubmitPayload.cs`

**Depends on:** T-01

**Done when:**
- All 3 files exist with correct namespace `OllimTelemetry.Models`
- All fields match REQ-05, REQ-06, REQ-07 exactly
- `dotnet build` passes for `OllimTelemetry.Models`

**Gate:** `dotnet build src/OllimTelemetry.Models/OllimTelemetry.Models.csproj`

---

## T-03 — Config layer

**What:** Implement `AppConfig`, `ConfigJsonContext`, and `ConfigManager`.

**Where:**
- `src/OllimTelemetry.Core/Config/AppConfig.cs`
- `src/OllimTelemetry.Core/Config/ConfigJsonContext.cs`
- `src/OllimTelemetry.Core/Config/ConfigManager.cs`

**Depends on:** T-02

**Reuses:** `System.Text.Json` source gen pattern (no reflection)

**Done when:**
- `AppConfig` matches REQ-08 exactly (all fields, all defaults)
- `ConfigJsonContext` registers both `AppConfig` and `SubmitPayload` (REQ-09)
- `ConfigManager.LoadOrCreate()` creates `~/.ollim/config.json` if not present (REQ-10, REQ-11)
- `ConfigManager.Save(AppConfig)` round-trips without data loss
- Builds without errors

**Gate:** `dotnet build src/OllimTelemetry.Core/OllimTelemetry.Core.csproj`

**Tests:**
- Unit: `LoadOrCreate` on non-existent path → creates file with `ShareGlobal=false` (AC-03)
- Unit: `Save` → `LoadOrCreate` returns equal record

---

## T-04 — Log parser

**What:** Implement `LogParser` with delta-read, privacy guards, and `TokenUsage` emission.

**Where:**
- `src/OllimTelemetry.Core/Parsing/LogParser.cs`

**Depends on:** T-02, T-03 (for `TokenUsage` type and namespace context)

**Done when:**
- Opens file with `FileShare.ReadWrite` (REQ-12)
- Reads only from provided byte offset to EOF
- Skips lines without `usage` key (REQ-13)
- Emits one `TokenUsage` per qualifying line
- Every `JsonDocument` property access annotated per REQ-14
- No other JSONL fields accessed (REQ-15)
- Byte offset managed via input/output parameter (REQ-16)

**Gate:** `dotnet build src/OllimTelemetry.Core/OllimTelemetry.Core.csproj`

**Tests:**
- Unit: 3-line JSONL (1 with usage, 1 without, 1 with usage) → 2 records with correct counts (AC-02)
- Unit: empty file → 0 records, offset = 0
- Unit: delta read — parse first 2 lines, then append 1 line, re-parse from offset → 1 new record

---

## T-05 — SQLite queue

**What:** Implement `SyncQueue` with file offsets and pending batches tables.

**Where:**
- `src/OllimTelemetry.Core/Queue/SyncQueue.cs`

**Depends on:** T-02 (for `SyncBatch`)

**Done when:**
- Creates `~/.ollim/queue.db` and both tables on first use (REQ-17, REQ-18)
- `GetOffset` / `SetOffset` round-trip for any file path
- `Enqueue` persists a `SyncBatch`
- `Dequeue(n)` returns up to n items with their DB ids
- `MarkSent` deletes rows by id
- `MarkFailed` increments `retry_count` and sets `next_retry_at` with exponential backoff capped at 60 min (REQ-19)
- `Dequeue` only returns rows where `next_retry_at <= UtcNow` (respects backoff)

**Gate:** `dotnet build src/OllimTelemetry.Core/OllimTelemetry.Core.csproj`

**Tests:**
- Unit: enqueue 3 → dequeue → mark sent → dequeue = empty (AC-04)
- Unit: mark failed → next_retry_at is in future → dequeue returns 0

---

## T-06 — File watcher

**What:** Implement `LogWatcher` wrapping `FileSystemWatcher` with debounce.

**Where:**
- `src/OllimTelemetry.Core/Watching/LogWatcher.cs`

**Depends on:** T-05 (debounce timer interacts with queue pattern)

**Done when:**
- Watches `~/.claude/projects/` with `IncludeSubdirectories=true` and filter `*.jsonl` (REQ-21)
- 500ms debounce per file path (REQ-22)
- `Start()`, `Stop()`, `OnFileChanged` callback exposed (REQ-23)
- Graceful handling when `~/.claude/projects/` doesn't exist (REQ-24) — creates directory
- `IDisposable` to properly dispose `FileSystemWatcher`

**Gate:** `dotnet build src/OllimTelemetry.Core/OllimTelemetry.Core.csproj`

---

## T-07 — Sync service

**What:** Implement `SyncService` background loop posting batches to the backend.

**Where:**
- `src/OllimTelemetry.Core/Sync/SyncService.cs`

**Depends on:** T-03 (config for interval + ShareGlobal), T-05 (queue)

**Done when:**
- Background loop runs with `Task.Delay(SyncIntervalMinutes)` (REQ-25)
- No HTTP calls when `ShareGlobal=false` (REQ-26)
- Posts batches as `SubmitPayload` JSON to `{BackendUrl}/v1/submit` (REQ-27)
- `MarkSent` on 2xx; `MarkFailed` on any error (REQ-27)
- Never throws from background loop (REQ-28)
- Updates `LastSyncAt` on success (REQ-29)
- `HttpClient` injected (not `new`-ed inside the service)
- `CancellationToken` support for clean stop

**Gate:** `dotnet build src/OllimTelemetry.Core/OllimTelemetry.Core.csproj`

---

## T-08 — Daemon manager + OS templates

**What:** Implement `DaemonManager` and create the OS service template files.

**Where:**
- `src/OllimTelemetry.Core/Daemon/DaemonManager.cs`
- `assets/com.ollim.plist.template`
- `assets/ollim.service.template`

**Depends on:** — (no code dependencies, can run parallel with T-01 cascade if needed)

**Done when:**
- OS detection via `RuntimeInformation.IsOSPlatform` (REQ-30)
- macOS: plist written to `~/Library/LaunchAgents/com.ollim.plist`, `launchctl load` invoked (REQ-31)
- Linux: unit written to `~/.config/systemd/user/ollim.service`, `systemctl --user enable --now ollim` invoked (REQ-32)
- Windows: returns error with message per REQ-33
- `Register`, `Unregister`, `IsRunning` exposed (REQ-34)
- plist template has `RunAtLoad = true`, `KeepAlive = true` (REQ-35)
- systemd template has `WantedBy=default.target` (REQ-53)
- `{{BINARY_PATH}}` placeholder in both templates

**Gate:** `dotnet build src/OllimTelemetry.Core/OllimTelemetry.Core.csproj`

---

## T-09 — Graceful shutdown

**What:** Wire SIGTERM / SIGINT handlers that flush queue before exit.

**Where:**
- Logic lives in `OllimTelemetry.Cli/Program.cs` (referenced in T-12, but shutdown handler setup is standalone)
- `src/OllimTelemetry.Core/Sync/SyncService.cs` must expose `StopAsync()`

**Depends on:** T-06, T-07, T-08

**Done when:**
- `Console.CancelKeyPress` handler stops `LogWatcher` and calls `SyncService.StopAsync()` (REQ-36)
- `AppDomain.CurrentDomain.ProcessExit` handler does the same (REQ-36)
- In-memory token accumulation (if any) is flushed to `SyncQueue` before exit (REQ-37)
- No crash on clean `Ctrl+C`

**Gate:** Manual test: run daemon in terminal, press Ctrl+C → process exits cleanly

---

## T-10 — Onboarding flow

**What:** Implement `OnboardingFlow` interactive first-run prompts.

**Where:**
- `src/OllimTelemetry.Cli/Onboarding/OnboardingFlow.cs`

**Depends on:** T-03 (ConfigManager), T-08 (DaemonManager)

**Done when:**
- Runs only when config does not exist (REQ-38)
- Uses `AnsiConsole.Ask<bool>()` and `AnsiConsole.Prompt()` (REQ-39)
- Prompts for `ShareGlobal` and `SyncIntervalMinutes` (REQ-40)
- Saves config and registers daemon (REQ-41)
- Prints styled confirmation per brainstorm design
- Prints "Run `ollim status` to confirm it's running."

**Gate:** `dotnet build src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj`

---

## T-11 — CLI commands

**What:** Implement all 7 command classes.

**Where:**
- `src/OllimTelemetry.Cli/Commands/StartCommand.cs`
- `src/OllimTelemetry.Cli/Commands/StopCommand.cs`
- `src/OllimTelemetry.Cli/Commands/StatusCommand.cs`
- `src/OllimTelemetry.Cli/Commands/ConfigCommand.cs`
- `src/OllimTelemetry.Cli/Commands/StatsCommand.cs`
- `src/OllimTelemetry.Cli/Commands/UnlinkCommand.cs`
- `src/OllimTelemetry.Cli/Commands/UninstallCommand.cs`

**Depends on:** T-03, T-05, T-08, T-10

**Done when:**
- REQ-42 through REQ-48 met for each command
- `UninstallCommand` prompts `y/n` before deleting (REQ-48)
- `StatsCommand` renders a `Spectre.Console.Table` (REQ-46)
- All inherit `AsyncCommand<TSettings>`
- NativeAOT compatible (no dynamic type resolution)

**Gate:** `dotnet build src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj`

---

## T-12 — Program.cs wiring

**What:** Wire the `CommandApp` and register all commands.

**Where:**
- `src/OllimTelemetry.Cli/Program.cs`

**Depends on:** T-11, T-09

**Done when:**
- `CommandApp` built with all 7 commands registered (REQ-49)
- App name = `ollim`
- Shutdown handlers wired (REQ-36)
- NativeAOT compatible (REQ-50)
- `dotnet run --project src/OllimTelemetry.Cli -- --help` shows all commands

**Gate:** `dotnet build OllimTelemetry.sln` — zero errors

---

## T-13 — Build script

**What:** Write `scripts/build.sh` for NativeAOT multi-RID publish.

**Where:**
- `scripts/build.sh`

**Depends on:** T-12 (compilable project)

**Done when:**
- Script is executable (`chmod +x`)
- Publishes for `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64` (REQ-51)
- Output binary named `ollim` in `./dist/<RID>/`
- Falls back to version `0.1.0` if `dotnet-gitversion` is not installed
- `set -e` at top

**Gate:** `bash scripts/build.sh` (on linux-x64) produces `dist/linux-x64/ollim`

---

## Parallel execution plan

```
T-01 (scaffold)
  ├── T-02 (models)
  │     ├── T-03 (config)
  │     │     ├── T-04 (parser)        [P: can run with T-05]
  │     │     └── T-07 (sync svc)      [P: after T-05]
  │     └── T-05 (queue)
  │           └── T-06 (watcher)
  └── T-08 (daemon + templates)        [P: can start after T-01]

T-03 + T-05 + T-06 + T-07 + T-08 complete →
  T-09 (shutdown)
  T-10 (onboarding)

T-09 + T-10 complete →
  T-11 (commands)

T-11 complete →
  T-12 (Program.cs)

T-12 complete →
  T-13 (build script)
```

Parallel groups:
- **Group A** (after T-01): T-02 and T-08 in parallel
- **Group B** (after T-02): T-03 and T-05 in parallel
- **Group C** (after T-03 + T-05): T-04, T-06, T-07 in parallel
- **Group D** (after T-03 + T-06 + T-07 + T-08): T-09, T-10 in parallel
- **Group E** (after T-09 + T-10): T-11
- **Sequential**: T-12 → T-13
