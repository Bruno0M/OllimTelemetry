# Search Strategy — OllimTelemetry Codebase Navigation

Efficient search patterns for the OllimTelemetry C# codebase.

## Priority Order

1. **Grep** (exact pattern, fast) → for known symbols/strings
2. **Glob** (file discovery) → for finding classes/files by name
3. **Read** (full file) → only after locating the right file
4. **Explore agent** (broad research) → last resort for >3 queries

Never use Bash for search (`find`, `grep`, `rg`) — use dedicated tools.

## Module Map

```
src/
├── OllimTelemetry.Models/         ← Pure data records; no dependencies
│   ├── TokenUsage.cs              ← Per-line parsed usage
│   ├── SyncBatch.cs               ← Aggregated batch for submission
│   ├── SubmitPayload.cs           ← HTTP POST body to api.ollim.dev
│   ├── LeaderboardEntry.cs        ← Leaderboard row model
│   └── LeaderboardResponse.cs     ← Leaderboard API response
│
├── OllimTelemetry.Core/           ← Engine; never references Spectre.Console
│   ├── Config/
│   │   ├── AppConfig.cs           ← Config record (~/.ollim/config.json)
│   │   ├── ConfigJsonContext.cs   ← Source-gen JSON context for AppConfig
│   │   └── ConfigManager.cs      ← Load/save config file
│   ├── Parsing/
│   │   ├── LogParser.cs           ← JSONL delta reader; byte-offset based
│   │   └── ProjectPathResolver.cs ← Derives project name from JSONL file path
│   ├── Queue/
│   │   └── SyncQueue.cs           ← SQLite queue (~/.ollim/queue.db)
│   ├── Sync/
│   │   └── SyncService.cs         ← Flush queue → POST /v1/submit (interval + backoff)
│   ├── Watching/
│   │   └── LogWatcher.cs          ← FileSystemWatcher + 500ms debounce
│   └── Daemon/
│       └── DaemonManager.cs       ← launchd (macOS) / systemd --user (Linux)
│
└── OllimTelemetry.Cli/            ← Entry point; all terminal I/O lives here
    ├── Program.cs                 ← --run-daemon flag + ConsoleAppFramework routing
    ├── Daemon/
    │   └── DaemonRunner.cs        ← Backfill + watcher + sync orchestration
    ├── Commands/
    │   ├── StartCommand.cs        ← ollim start
    │   ├── StopCommand.cs         ← ollim stop
    │   ├── StatusCommand.cs       ← ollim status
    │   ├── ConfigCommand.cs       ← ollim config
    │   ├── StatsCommand.cs        ← ollim stats
    │   ├── LeaderboardCommand.cs  ← ollim leaderboard
    │   ├── UnlinkCommand.cs       ← ollim unlink
    │   └── UninstallCommand.cs    ← ollim uninstall
    ├── Onboarding/                ← First-run opt-in flow
    ├── CliJsonContext.cs          ← Source-gen JSON context for CLI serialization
    └── TrimmerRoots.xml           ← NativeAOT trimmer roots

tests/
└── OllimTelemetry.Tests/
    ├── LogParserTests.cs
    ├── SyncQueueTests.cs
    ├── ConfigManagerTests.cs
    └── ProjectPathResolverTests.cs
```

## Common Search Patterns

### "Where is command X handled?"

```
# Step 1: Find the routing
Grep pattern="app.Add" path="src/OllimTelemetry.Cli/Program.cs"

# Step 2: Open the command file
Read file_path="src/OllimTelemetry.Cli/Commands/StatsCommand.cs"
```

### "Where is class/method X defined?"

```
Grep pattern="class SyncQueue|void Enqueue|Task RunAsync" type="cs"
```

### "All command files"

```
Glob pattern="src/OllimTelemetry.Cli/Commands/*.cs"
```

### "Find all JSON source-gen contexts"

```
Grep pattern="\[JsonSerializable" type="cs" output_mode="content"
```

### "Find all PRIVACY guards in the parser"

```
Grep pattern="PRIVACY" path="src/OllimTelemetry.Core/Parsing/LogParser.cs"
```

### "Which files reference SyncBatch?"

```
Grep pattern="SyncBatch" type="cs" output_mode="files_with_matches"
```

### "Find SQLite schema definitions"

```
Grep pattern="CREATE TABLE" type="cs" output_mode="content"
```

### "Find all test classes"

```
Glob pattern="src/OllimTelemetry.Tests/*.cs"
```

## OllimTelemetry-Specific Navigation Rules

### Adding a new CLI command

1. Create `src/OllimTelemetry.Cli/Commands/<Name>Command.cs` — static class with `RunAsync`
2. Register in `Program.cs` via `app.Add("name", NameCommand.RunAsync)`
3. All output via `Spectre.Console` — never `Console.WriteLine` from a command

### Adding a new Core feature

1. Check `OllimTelemetry.Models/` — add a record there if new data is needed
2. Check `OllimTelemetry.Core/Config/AppConfig.cs` — add a config field if opt-in is required
3. If new JSON serialization is needed, add `[JsonSerializable(typeof(X))]` to the correct `*JsonContext.cs`
4. `Core` must not reference `Spectre.Console` or `ConsoleAppFramework` — verify `.csproj` before adding

### Debugging the daemon flow

1. Entry: `Program.cs` → `--run-daemon` → `DaemonRunner.RunAsync`
2. Startup backfill: `DaemonRunner.BackfillExistingFiles` → `LogParser.Parse`
3. Live events: `LogWatcher` fires → `DaemonRunner.ProcessFile` → `SyncQueue.Enqueue`
4. Flush: `SyncService` interval → `SyncQueue` → `POST /v1/submit`

### Debugging JSON / NativeAOT issues

1. Check that the type has a `[JsonSerializable]` entry in a `*JsonContext.cs`
2. `ConfigJsonContext.cs` covers config types; `CliJsonContext.cs` covers CLI-side types
3. Never add `JsonSerializer.Serialize<T>(value)` without a matching source-gen context — it will fail at NativeAOT publish time

### SQLite / queue issues

1. `src/OllimTelemetry.Core/Queue/SyncQueue.cs` → `EnsureSchema()` for table definitions
2. DB file: `~/.ollim/queue.db`
3. `file_offsets` table tracks byte position per JSONL file; `pending_batches` holds unsynced data

### Configuration issues

1. `src/OllimTelemetry.Core/Config/AppConfig.cs` → field definitions
2. `src/OllimTelemetry.Core/Config/ConfigManager.cs` → `LoadOrCreate` / `Save`
3. Config file: `~/.ollim/config.json`

## Anti-Patterns

❌ **Don't** read every `*Command.cs` file to find one method — Grep for the method name first  
❌ **Don't** use Bash `find src -name "*.cs"` — use Glob  
❌ **Don't** read `Program.cs` fully to find a command — Grep for `app.Add`  
❌ **Don't** add JSON serialization calls without checking the source-gen context first  
❌ **Don't** add `Spectre.Console` usage inside `OllimTelemetry.Core` — check the `.csproj`

## Dependency Check

```
# Check if a NuGet package is already referenced (before adding)
Grep pattern="PackageReference" glob="src/**/*.csproj" output_mode="content"

# Verify Core has no Spectre.Console reference (must stay clean)
Grep pattern="Spectre" path="src/OllimTelemetry.Core/OllimTelemetry.Core.csproj"

# Find all reflection usage (forbidden in NativeAOT paths)
Grep pattern="\.GetType()\|typeof.*Activator\|BindingFlags" type="cs"
```
