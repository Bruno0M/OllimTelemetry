# Feature: MVP — Full Solution Scaffold + Implementation

**Scope:** Complex — 3 projects, NativeAOT binary, OS daemon, SQLite queue, 7 CLI commands
**Status:** Specifying

---

## Requirements

### Solution Structure

**REQ-01** The solution file `OllimTelemetry.sln` must reside at the repository root and reference all three projects.

**REQ-02** All projects must reside under `src/` and follow the naming pattern `OllimTelemetry.<Layer>`.

**REQ-03** Dependency graph must be enforced at project-reference level:
- `Cli` references `Core` and `Models`
- `Core` references `Models`
- `Models` has no project references

**REQ-04** `OllimTelemetry.Core` must never add a PackageReference to `Spectre.Console` or `Spectre.Console.Cli`.

---

### Models Layer (`OllimTelemetry.Models`)

**REQ-05** `TokenUsage` is a sealed record with fields: `Agent`, `InputTokens`, `OutputTokens`, `CacheReadTokens`, `CacheWriteTokens`, `Timestamp`.

**REQ-06** `SyncBatch` is a sealed record with fields: `Agent`, `InputTokens`, `OutputTokens`, `CacheReadTokens`, `CacheWriteTokens`, `PeriodStart` (ISO 8601 string), `PeriodEnd` (ISO 8601 string).

**REQ-07** `SubmitPayload` is a sealed record with fields: `UserId`, `Agent`, `InputTokens`, `OutputTokens`, `CacheReadTokens`, `CacheWriteTokens`, `PeriodStart`, `PeriodEnd`, `ClientVersion`.

---

### Config Layer (`OllimTelemetry.Core.Config`)

**REQ-08** `AppConfig` is a sealed record with all fields having `init` setters and defaults as specified in the brainstorm. Fields: `Version`, `UserId` (auto-generated UUID), `ShareGlobal` (false), `ShareRepoName` (false), `SyncIntervalMinutes` (5), `Agent` ("claude-code"), `BackendUrl` ("https://api.ollim.dev"), `CreatedAt` (UTC now ISO 8601), `LastSyncAt` (null).

**REQ-09** `ConfigJsonContext` is a partial class annotated with `[JsonSerializable(typeof(AppConfig))]` and `[JsonSerializable(typeof(SubmitPayload))]` using `JsonSourceGenerationOptions(WriteIndented = true)`. It must be `internal`.

**REQ-10** `ConfigManager` reads and writes `~/.ollim/config.json` using only `ConfigJsonContext` (no reflection-based serialization). It must expose at minimum: `LoadOrCreate()`, `Save(AppConfig)`.

**REQ-11** `ConfigManager` must create the `~/.ollim/` directory if it doesn't exist before writing.

---

### Log Parser (`OllimTelemetry.Core.Parsing`)

**REQ-12** `LogParser` reads `.jsonl` files in delta mode: it tracks the last byte offset read per file and on each invocation reads only new bytes from that offset to EOF. File must be opened with `FileShare.ReadWrite` to avoid blocking Claude Code.

**REQ-13** `LogParser` skips any line that does not contain a `usage` property. Lines with a `usage` property must emit exactly one `TokenUsage` record.

**REQ-14** `LogParser` reads ONLY two top-level properties per line: `usage` (for token counts) and `timestamp` (for the record's `Timestamp`). Every `JsonDocument` property access that touches these fields must be annotated:
- Usage fields: `// PRIVACY: usage only`
- Timestamp field: `// PRIVACY: timestamp only — no content`

**REQ-15** `LogParser` must never read, store, or log any other JSONL field (`role`, `content`, `model`, `messages`, etc.).

**REQ-16** Byte offsets are managed externally by `SyncQueue` (LogParser receives the starting offset and returns the new offset after parsing).

---

### SQLite Queue (`OllimTelemetry.Core.Queue`)

**REQ-17** `SyncQueue` uses SQLite (via `Microsoft.Data.Sqlite`) as its backing store at `~/.ollim/queue.db`.

**REQ-18** `SyncQueue` maintains two tables:
- `file_offsets (file_path TEXT PRIMARY KEY, byte_offset INTEGER NOT NULL)` — tracks parse position per log file
- `pending_batches (id INTEGER PRIMARY KEY AUTOINCREMENT, agent TEXT, input_tokens INTEGER, output_tokens INTEGER, cache_read_tokens INTEGER, cache_write_tokens INTEGER, period_start TEXT, period_end TEXT, retry_count INTEGER DEFAULT 0, next_retry_at TEXT)` — stores unsynced batches

**REQ-19** Exponential backoff: `next_retry_at` is set to `UtcNow + 2^retry_count minutes`, capped at 60 minutes.

**REQ-20** `SyncQueue` exposes: `GetOffset(string filePath)`, `SetOffset(string filePath, long offset)`, `Enqueue(SyncBatch)`, `Dequeue(int maxItems)`, `MarkSent(IEnumerable<long> ids)`, `MarkFailed(long id)`.

---

### File Watcher (`OllimTelemetry.Core.Watching`)

**REQ-21** `LogWatcher` wraps `FileSystemWatcher` configured with:
- `Path = ~/.claude/projects/`
- `Filter = *.jsonl`
- `IncludeSubdirectories = true`
- `NotifyFilter = LastWrite | Size`

**REQ-22** `LogWatcher` debounces file change events with a 500ms window per file path. Multiple rapid events for the same file collapse into one callback.

**REQ-23** `LogWatcher` exposes `Start()`, `Stop()`, and an event/callback `OnFileChanged(string filePath)`.

**REQ-24** `LogWatcher` must handle `~/.claude/projects/` not existing at startup gracefully (create directory or log warning, not crash).

---

### Sync Service (`OllimTelemetry.Core.Sync`)

**REQ-25** `SyncService` runs on a background timer with interval `AppConfig.SyncIntervalMinutes`.

**REQ-26** `SyncService` only submits if `AppConfig.ShareGlobal == true`. If false, it dequeues nothing and does not make HTTP requests.

**REQ-27** On each sync cycle: dequeue up to 50 pending batches → for each, POST to `{BackendUrl}/v1/submit` as `SubmitPayload` JSON → on HTTP 2xx: `MarkSent` → on failure: `MarkFailed` (backoff applies).

**REQ-28** `SyncService` must never throw from the background loop. All exceptions are caught, logged to `stderr`, and the loop continues.

**REQ-29** On success, `ConfigManager.Save` is called with `LastSyncAt = UtcNow`.

---

### Daemon Manager (`OllimTelemetry.Core.Daemon`)

**REQ-30** `DaemonManager` detects OS at runtime using `RuntimeInformation.IsOSPlatform`.

**REQ-31** On **macOS**: reads `assets/com.ollim.plist.template`, substitutes `{{BINARY_PATH}}`, writes to `~/Library/LaunchAgents/com.ollim.plist`, runs `launchctl load`.

**REQ-32** On **Linux**: reads `assets/ollim.service.template`, substitutes `{{BINARY_PATH}}`, writes to `~/.config/systemd/user/ollim.service`, runs `systemctl --user enable --now ollim`.

**REQ-33** On **Windows**: returns an error result with message "Ollim Telemetry is not supported on Windows in the MVP."

**REQ-34** `DaemonManager` exposes: `Register(string binaryPath)`, `Unregister()`, `IsRunning()`.

**REQ-35** The plist template must set `RunAtLoad = true` and `KeepAlive = true`.

---

### Graceful Shutdown

**REQ-36** The daemon process registers handlers for `Console.CancelKeyPress` and `AppDomain.CurrentDomain.ProcessExit`.

**REQ-37** On shutdown signal: stop `LogWatcher`, flush any in-memory token accumulation to `SyncQueue`, then exit.

---

### Onboarding Flow (`OllimTelemetry.Cli.Onboarding`)

**REQ-38** `OnboardingFlow` runs when `ollim start` is invoked and `~/.ollim/config.json` does not exist.

**REQ-39** Uses `AnsiConsole.Ask<bool>()` and `AnsiConsole.Prompt()` — no raw stdin reads.

**REQ-40** Prompts: (1) share token counts on leaderboard? → sets `ShareGlobal`; (2) sync interval (default 5 min) → sets `SyncIntervalMinutes`.

**REQ-41** After prompts: saves config, registers daemon, prints confirmation with `ollim status` hint.

---

### CLI Commands (`OllimTelemetry.Cli.Commands`)

Each command inherits `AsyncCommand<TSettings>` from Spectre.Console.Cli.

**REQ-42** `ollim start` — runs `OnboardingFlow` if no config; starts daemon via `DaemonManager.Register`; prints success or error.

**REQ-43** `ollim stop` — stops daemon via `DaemonManager.Unregister`; prints confirmation.

**REQ-44** `ollim status` — shows: daemon running (yes/no), `ShareGlobal` value, last 24h token counts from SQLite, `LastSyncAt`.

**REQ-45** `ollim config` — if `$EDITOR` is set, opens `~/.ollim/config.json` in it; otherwise prints the path.

**REQ-46** `ollim stats` — queries SQLite for last 7 days of data; prints a table (Spectre.Console `Table`) with columns: Date, Input, Output, CacheRead, CacheWrite, Total.

**REQ-47** `ollim unlink` — sets `ShareGlobal = false` in config; saves; prints confirmation. Does NOT delete UserId (D-09).

**REQ-48** `ollim uninstall` — stops daemon (`DaemonManager.Unregister`); deletes `~/.ollim/` directory; prints confirmation. Asks for `y/n` confirmation before proceeding.

---

### Entry Point (`OllimTelemetry.Cli/Program.cs`)

**REQ-49** Builds a `CommandApp` (Spectre.Console.Cli), registers all 7 commands, sets app name to `ollim`, sets description.

**REQ-50** `Program.cs` must be compatible with NativeAOT — no `Assembly.GetTypes()`, no runtime type discovery.

---

### Build & Distribution

**REQ-51** `scripts/build.sh` publishes `OllimTelemetry.Cli` for `osx-arm64`, `osx-x64`, `linux-x64`, `linux-arm64` with `--self-contained true` and `/p:PublishAot=true`. Output goes to `./dist/<RID>/ollim`.

**REQ-52** `assets/com.ollim.plist.template` contains a valid launchd plist with `RunAtLoad = true`, `KeepAlive = true`, and `{{BINARY_PATH}}` placeholder.

**REQ-53** `assets/ollim.service.template` contains a valid systemd user unit with `{{BINARY_PATH}}` placeholder and `WantedBy=default.target`.

---

## Acceptance Criteria

**AC-01** `dotnet build OllimTelemetry.sln` passes with zero errors and zero warnings.

**AC-02** `LogParser` unit test: given a JSONL file with 3 lines (1 with usage, 1 without, 1 with usage), returns exactly 2 `TokenUsage` records with correct token counts.

**AC-03** `ConfigManager` round-trip test: `LoadOrCreate()` on a non-existent path creates config with `ShareGlobal=false`; `Save` + `LoadOrCreate` returns identical values.

**AC-04** `SyncQueue` test: enqueue 3 batches → dequeue returns all 3 → MarkSent clears them → dequeue returns empty.

**AC-05** `ollim start` on a clean machine runs onboarding, creates `~/.ollim/config.json`, and registers the OS daemon without error.

**AC-06** `ollim unlink` sets `ShareGlobal=false` without changing `UserId`.

**AC-07** `dotnet publish` with `PublishAot=true` for `linux-x64` produces a binary at `dist/linux-x64/ollim` under 50 MB.
