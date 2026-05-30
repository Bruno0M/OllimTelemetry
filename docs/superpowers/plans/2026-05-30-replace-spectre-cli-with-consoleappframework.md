# Replace Spectre.Console.Cli with ConsoleAppFramework — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Swap `Spectre.Console.Cli` for `ConsoleAppFramework` so the NativeAOT binary starts without a reflection-based crash, and move the daemon entry point from a hidden sub-command to a `--run-daemon` flag.

**Architecture:** `Program.cs` checks `args.Contains("--run-daemon")` before handing off to ConsoleAppFramework — the daemon loop runs entirely outside the CLI router. The 7 public commands are refactored from `AsyncCommand` subclasses to plain static methods and registered via `app.Add()`. `Spectre.Console` (rich output) is unchanged.

**Tech Stack:** .NET 10, C# 14, ConsoleAppFramework v5, Spectre.Console 0.49 (kept), NativeAOT (`PublishAot=true`)

**Spec:** `docs/superpowers/specs/2026-05-30-replace-spectre-cli-with-consoleappframework-design.md`

---

## File Map

| Action | Path | Purpose |
|--------|------|---------|
| Modify | `src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj` | Swap packages |
| Modify | `src/OllimTelemetry.Cli/TrimmerRoots.xml` | Remove Spectre.Console.Cli entry |
| Create | `src/OllimTelemetry.Cli/Daemon/DaemonRunner.cs` | Extracted daemon loop |
| Delete | `src/OllimTelemetry.Cli/Commands/DaemonCommand.cs` | Replaced by DaemonRunner |
| Modify | `src/OllimTelemetry.Cli/Commands/StartCommand.cs` | Refactor to static class |
| Modify | `src/OllimTelemetry.Cli/Commands/StopCommand.cs` | Refactor to static class |
| Modify | `src/OllimTelemetry.Cli/Commands/StatusCommand.cs` | Refactor to static class |
| Modify | `src/OllimTelemetry.Cli/Commands/ConfigCommand.cs` | Refactor to static class |
| Modify | `src/OllimTelemetry.Cli/Commands/StatsCommand.cs` | Refactor to static class |
| Modify | `src/OllimTelemetry.Cli/Commands/UnlinkCommand.cs` | Refactor to static class |
| Modify | `src/OllimTelemetry.Cli/Commands/UninstallCommand.cs` | Refactor to static class |
| Modify | `src/OllimTelemetry.Cli/Program.cs` | Rewrite with ConsoleAppFramework |
| Modify | `assets/ollim.service.template` | `daemon` → `--run-daemon` |
| Modify | `assets/com.ollim.plist.template` | `daemon` → `--run-daemon` |

---

## Task 1: Swap NuGet packages and TrimmerRoots

**Files:**
- Modify: `src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj`
- Modify: `src/OllimTelemetry.Cli/TrimmerRoots.xml`

- [ ] **Step 1: Remove Spectre.Console.Cli and add ConsoleAppFramework in the csproj**

  Open `src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj`. Replace the `ItemGroup` that contains the package references so it reads:

  ```xml
  <ItemGroup>
    <ProjectReference Include="..\OllimTelemetry.Core\OllimTelemetry.Core.csproj"     />
    <ProjectReference Include="..\OllimTelemetry.Models\OllimTelemetry.Models.csproj" />
    <PackageReference Include="Spectre.Console"        Version="0.49.*" />
    <PackageReference Include="ConsoleAppFramework"    Version="5.*"    />
  </ItemGroup>
  ```

- [ ] **Step 2: Clean up TrimmerRoots.xml**

  Replace the full content of `src/OllimTelemetry.Cli/TrimmerRoots.xml` with:

  ```xml
  <linker>
    <!-- Preserve all Cli types as a conservative safety net for NativeAOT trimming.
         ConsoleAppFramework is source-generated and does not need a root descriptor. -->
    <assembly fullname="ollim" preserve="all" />
  </linker>
  ```

- [ ] **Step 3: Restore packages to verify the swap**

  ```bash
  cd /home/bruno/dev/OllimTelemetry
  dotnet restore src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj
  ```

  Expected: restore succeeds, output includes `ConsoleAppFramework 5.x.x`, no mention of `Spectre.Console.Cli`.

- [ ] **Step 4: Commit**

  ```bash
  git add src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj src/OllimTelemetry.Cli/TrimmerRoots.xml
  git commit -m "chore(cli): swap Spectre.Console.Cli for ConsoleAppFramework, clean TrimmerRoots"
  ```

---

## Task 2: Create DaemonRunner and delete DaemonCommand

**Files:**
- Create: `src/OllimTelemetry.Cli/Daemon/DaemonRunner.cs`
- Delete: `src/OllimTelemetry.Cli/Commands/DaemonCommand.cs`

- [ ] **Step 1: Create `src/OllimTelemetry.Cli/Daemon/DaemonRunner.cs`**

  Create the directory `src/OllimTelemetry.Cli/Daemon/` and write the file:

  ```csharp
  using OllimTelemetry.Core.Config;
  using OllimTelemetry.Core.Parsing;
  using OllimTelemetry.Core.Queue;
  using OllimTelemetry.Core.Sync;
  using OllimTelemetry.Core.Watching;
  using OllimTelemetry.Models;

  namespace OllimTelemetry.Cli.Daemon;

  internal static class DaemonRunner
  {
      public static async Task RunAsync(CancellationToken ct)
      {
          var configManager = new ConfigManager();
          var config        = configManager.LoadOrCreate();

          using var queue = new SyncQueue();
          var parser       = new LogParser();
          var watcher      = new LogWatcher();
          var http         = new System.Net.Http.HttpClient();
          var syncService  = new SyncService(configManager, queue, http);

          // REQ-37: on each file change, parse delta and enqueue batch
          watcher.Start(filePath => ProcessFile(filePath, config.Agent, parser, queue));
          syncService.Start();

          await Console.Error.WriteLineAsync("[ollim] daemon started");

          try
          {
              await Task.Delay(Timeout.Infinite, ct);
          }
          catch (OperationCanceledException) { }

          watcher.Stop();
          await syncService.StopAsync();
          await Console.Error.WriteLineAsync("[ollim] daemon stopped");
      }

      private static void ProcessFile(string filePath, string agent, LogParser parser, SyncQueue queue)
      {
          try
          {
              var offset  = queue.GetOffset(filePath);
              var records = parser.Parse(filePath, offset, out var newOffset, agent);

              if (records.Count == 0) return;

              queue.SetOffset(filePath, newOffset);

              var batch = new SyncBatch(
                  agent,
                  records.Sum(r => r.InputTokens),
                  records.Sum(r => r.OutputTokens),
                  records.Sum(r => r.CacheReadTokens),
                  records.Sum(r => r.CacheWriteTokens),
                  records[0].Timestamp.ToString("O"),
                  records[^1].Timestamp.ToString("O")
              );

              queue.Enqueue(batch);
          }
          catch (Exception ex)
          {
              Console.Error.WriteLine($"[ollim] error processing {filePath}: {ex.Message}");
          }
      }
  }
  ```

  Note: SIGTERM/SIGINT handling is NOT in `DaemonRunner` — it lives in `Program.cs` and arrives here via the `CancellationToken`.

- [ ] **Step 2: Delete `DaemonCommand.cs`**

  ```bash
  rm src/OllimTelemetry.Cli/Commands/DaemonCommand.cs
  ```

- [ ] **Step 3: Verify the project still builds (it won't link yet — that's fine)**

  ```bash
  dotnet build src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj 2>&1 | tail -20
  ```

  Expected: build errors about `DaemonCommand` not found and `AsyncCommand` not found (since we haven't rewritten Program.cs yet). This is expected — confirms the old code is gone.

- [ ] **Step 4: Commit**

  ```bash
  git add src/OllimTelemetry.Cli/Daemon/DaemonRunner.cs
  git rm src/OllimTelemetry.Cli/Commands/DaemonCommand.cs
  git commit -m "refactor(cli): extract DaemonRunner, remove DaemonCommand"
  ```

---

## Task 3: Refactor StartCommand, StopCommand, StatusCommand, ConfigCommand

**Files:**
- Modify: `src/OllimTelemetry.Cli/Commands/StartCommand.cs`
- Modify: `src/OllimTelemetry.Cli/Commands/StopCommand.cs`
- Modify: `src/OllimTelemetry.Cli/Commands/StatusCommand.cs`
- Modify: `src/OllimTelemetry.Cli/Commands/ConfigCommand.cs`

All four follow the same pattern: remove the `AsyncCommand` base class, make the class `internal static`, rename `ExecuteAsync(CommandContext)` to `RunAsync()`.

- [ ] **Step 1: Rewrite `StartCommand.cs`**

  ```csharp
  using OllimTelemetry.Cli.Onboarding;
  using OllimTelemetry.Core.Config;
  using OllimTelemetry.Core.Daemon;
  using Spectre.Console;

  namespace OllimTelemetry.Cli.Commands;

  internal static class StartCommand
  {
      public static Task<int> RunAsync()
      {
          var configManager = new ConfigManager();
          var daemonManager = new DaemonManager();
          var binaryPath    = Environment.ProcessPath ?? "ollim";

          if (!File.Exists(configManager.ConfigFilePath))
          {
              var flow = new OnboardingFlow(configManager, daemonManager);
              flow.Run(binaryPath);
              return Task.FromResult(0);
          }

          var (success, message) = daemonManager.Register(binaryPath);
          if (success)
              AnsiConsole.MarkupLine("[green]✓[/] Daemon started.");
          else
              AnsiConsole.MarkupLine($"[red]✗[/] {message}");

          return Task.FromResult(success ? 0 : 1);
      }
  }
  ```

- [ ] **Step 2: Rewrite `StopCommand.cs`**

  ```csharp
  using OllimTelemetry.Core.Daemon;
  using Spectre.Console;

  namespace OllimTelemetry.Cli.Commands;

  internal static class StopCommand
  {
      public static Task<int> RunAsync()
      {
          var daemonManager = new DaemonManager();
          var (success, message) = daemonManager.Unregister();

          if (success)
              AnsiConsole.MarkupLine("[green]✓[/] Daemon stopped.");
          else
              AnsiConsole.MarkupLine($"[red]✗[/] {message}");

          return Task.FromResult(success ? 0 : 1);
      }
  }
  ```

- [ ] **Step 3: Rewrite `StatusCommand.cs`**

  ```csharp
  using OllimTelemetry.Core.Config;
  using OllimTelemetry.Core.Daemon;
  using OllimTelemetry.Core.Queue;
  using Spectre.Console;

  namespace OllimTelemetry.Cli.Commands;

  internal static class StatusCommand
  {
      public static Task<int> RunAsync()
      {
          var configManager = new ConfigManager();
          var daemonManager = new DaemonManager();
          var config        = configManager.LoadOrCreate();

          var running = daemonManager.IsRunning();

          AnsiConsole.Write(new Rule("[bold]Ollim Telemetry Status[/]").RuleStyle("grey"));
          AnsiConsole.WriteLine();

          AnsiConsole.MarkupLine($"  Daemon:       {(running ? "[green]running[/]" : "[red]stopped[/]")}");
          AnsiConsole.MarkupLine($"  Sharing:      {(config.ShareGlobal ? "[green]enabled[/]" : "[yellow]disabled[/]")}");
          AnsiConsole.MarkupLine($"  Sync every:   [dim]{config.SyncIntervalMinutes} minutes[/]");
          AnsiConsole.MarkupLine($"  Last sync:    [dim]{config.LastSyncAt ?? "never"}[/]");

          AnsiConsole.WriteLine();

          using var queue = new SyncQueue();
          var pending = queue.Dequeue(1000);

          AnsiConsole.MarkupLine($"  Pending batches: [dim]{pending.Count}[/]");

          return Task.FromResult(0);
      }
  }
  ```

- [ ] **Step 4: Rewrite `ConfigCommand.cs`**

  ```csharp
  using OllimTelemetry.Core.Config;
  using Spectre.Console;

  namespace OllimTelemetry.Cli.Commands;

  internal static class ConfigCommand
  {
      public static Task<int> RunAsync()
      {
          var editor     = Environment.GetEnvironmentVariable("EDITOR");
          var configPath = ConfigManager.DefaultConfigFilePath;

          if (!string.IsNullOrWhiteSpace(editor))
          {
              using var proc = new System.Diagnostics.Process();
              proc.StartInfo = new System.Diagnostics.ProcessStartInfo(editor, configPath)
              {
                  UseShellExecute = false
              };
              proc.Start();
              proc.WaitForExit();
          }
          else
          {
              AnsiConsole.MarkupLine($"Config path: [bold]{configPath}[/]");
              AnsiConsole.MarkupLine("[dim]Set $EDITOR to open it automatically.[/]");
          }

          return Task.FromResult(0);
      }
  }
  ```

- [ ] **Step 5: Commit**

  ```bash
  git add src/OllimTelemetry.Cli/Commands/StartCommand.cs \
          src/OllimTelemetry.Cli/Commands/StopCommand.cs  \
          src/OllimTelemetry.Cli/Commands/StatusCommand.cs \
          src/OllimTelemetry.Cli/Commands/ConfigCommand.cs
  git commit -m "refactor(cli): convert StartCommand StopCommand StatusCommand ConfigCommand to static"
  ```

---

## Task 4: Refactor StatsCommand, UnlinkCommand, UninstallCommand

**Files:**
- Modify: `src/OllimTelemetry.Cli/Commands/StatsCommand.cs`
- Modify: `src/OllimTelemetry.Cli/Commands/UnlinkCommand.cs`
- Modify: `src/OllimTelemetry.Cli/Commands/UninstallCommand.cs`

- [ ] **Step 1: Rewrite `StatsCommand.cs`**

  ```csharp
  using OllimTelemetry.Core.Queue;
  using Spectre.Console;

  namespace OllimTelemetry.Cli.Commands;

  internal static class StatsCommand
  {
      public static Task<int> RunAsync()
      {
          using var queue = new SyncQueue();
          var cutoff      = DateTime.UtcNow.AddDays(-7).ToString("O");
          var batches     = queue.GetBatchesSince(cutoff);

          var table = new Table()
              .AddColumn("Date")
              .AddColumn(new TableColumn("Input").RightAligned())
              .AddColumn(new TableColumn("Output").RightAligned())
              .AddColumn(new TableColumn("Cache Read").RightAligned())
              .AddColumn(new TableColumn("Cache Write").RightAligned())
              .AddColumn(new TableColumn("Total").RightAligned());

          var grouped = batches
              .GroupBy(b => DateTime.Parse(b.PeriodStart).Date)
              .OrderByDescending(g => g.Key);

          foreach (var day in grouped)
          {
              var input      = day.Sum(b => b.InputTokens);
              var output     = day.Sum(b => b.OutputTokens);
              var cacheRead  = day.Sum(b => b.CacheReadTokens);
              var cacheWrite = day.Sum(b => b.CacheWriteTokens);
              var total      = input + output + cacheRead + cacheWrite;

              table.AddRow(
                  day.Key.ToString("yyyy-MM-dd"),
                  input.ToString("N0"),
                  output.ToString("N0"),
                  cacheRead.ToString("N0"),
                  cacheWrite.ToString("N0"),
                  $"[bold]{total:N0}[/]"
              );
          }

          if (!table.Rows.Any())
              AnsiConsole.MarkupLine("[dim]No token data for the last 7 days.[/]");
          else
              AnsiConsole.Write(table);

          return Task.FromResult(0);
      }
  }
  ```

- [ ] **Step 2: Rewrite `UnlinkCommand.cs`**

  ```csharp
  using OllimTelemetry.Core.Config;
  using Spectre.Console;

  namespace OllimTelemetry.Cli.Commands;

  internal static class UnlinkCommand
  {
      public static Task<int> RunAsync()
      {
          var configManager = new ConfigManager();
          var config        = configManager.LoadOrCreate();

          // D-09: keep UserId — only disable sharing
          configManager.Save(config with { ShareGlobal = false });

          AnsiConsole.MarkupLine("[green]✓[/] Sharing disabled. Your data will no longer be submitted to the leaderboard.");
          AnsiConsole.MarkupLine("[dim]Your local data and UserId are preserved. Run `ollim start` to re-enable.[/]");

          return Task.FromResult(0);
      }
  }
  ```

- [ ] **Step 3: Rewrite `UninstallCommand.cs`**

  ```csharp
  using OllimTelemetry.Core.Config;
  using OllimTelemetry.Core.Daemon;
  using Spectre.Console;

  namespace OllimTelemetry.Cli.Commands;

  internal static class UninstallCommand
  {
      public static Task<int> RunAsync()
      {
          var confirmed = AnsiConsole.Confirm(
              "[red]This will remove the daemon, config, and all local data. Continue?[/]",
              defaultValue: false);

          if (!confirmed)
          {
              AnsiConsole.MarkupLine("[dim]Uninstall cancelled.[/]");
              return Task.FromResult(0);
          }

          var daemonManager = new DaemonManager();
          daemonManager.Unregister();

          var ollimDir = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ollim");

          if (Directory.Exists(ollimDir))
              Directory.Delete(ollimDir, recursive: true);

          AnsiConsole.MarkupLine("[green]✓[/] Daemon stopped and unregistered.");
          AnsiConsole.MarkupLine("[green]✓[/] All local data removed.");
          AnsiConsole.MarkupLine("[dim]The ollim binary itself was not removed — delete it manually if desired.[/]");

          return Task.FromResult(0);
      }
  }
  ```

- [ ] **Step 4: Commit**

  ```bash
  git add src/OllimTelemetry.Cli/Commands/StatsCommand.cs   \
          src/OllimTelemetry.Cli/Commands/UnlinkCommand.cs   \
          src/OllimTelemetry.Cli/Commands/UninstallCommand.cs
  git commit -m "refactor(cli): convert StatsCommand UnlinkCommand UninstallCommand to static"
  ```

---

## Task 5: Rewrite Program.cs

**Files:**
- Modify: `src/OllimTelemetry.Cli/Program.cs`

- [ ] **Step 1: Rewrite `Program.cs`**

  Replace the entire file content with:

  ```csharp
  using ConsoleAppFramework;
  using OllimTelemetry.Cli.Commands;
  using OllimTelemetry.Cli.Daemon;

  // Daemon entry point — invoked by the OS service manager, not by users.
  // The service template calls: ollim --run-daemon
  if (args.Contains("--run-daemon"))
  {
      var cts = new CancellationTokenSource();
      Console.CancelKeyPress   += (_, e) => { e.Cancel = true; cts.Cancel(); };
      AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();
      await DaemonRunner.RunAsync(cts.Token);
      return;
  }

  var app = ConsoleApp.Create();
  app.Add("start",     StartCommand.RunAsync);
  app.Add("stop",      StopCommand.RunAsync);
  app.Add("status",    StatusCommand.RunAsync);
  app.Add("config",    ConfigCommand.RunAsync);
  app.Add("stats",     StatsCommand.RunAsync);
  app.Add("unlink",    UnlinkCommand.RunAsync);
  app.Add("uninstall", UninstallCommand.RunAsync);
  await app.RunAsync(args);
  ```

- [ ] **Step 2: Build to verify the project compiles**

  ```bash
  dotnet build src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj 2>&1 | tail -20
  ```

  Expected: `Build succeeded. 0 Error(s)`. Warnings from `Spectre.Console` about trim are expected and acceptable.

- [ ] **Step 3: Smoke-test with `dotnet run`**

  ```bash
  dotnet run --project src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj -- --help
  ```

  Expected output includes: `start`, `stop`, `status`, `config`, `stats`, `unlink`, `uninstall`. No `daemon` entry. No crash.

- [ ] **Step 4: Commit**

  ```bash
  git add src/OllimTelemetry.Cli/Program.cs
  git commit -m "feat(cli): rewrite Program.cs with ConsoleAppFramework, --run-daemon flag"
  ```

---

## Task 6: Update OS service templates

**Files:**
- Modify: `assets/ollim.service.template`
- Modify: `assets/com.ollim.plist.template`

- [ ] **Step 1: Update `assets/ollim.service.template`**

  Replace `ExecStart={{BINARY_PATH}} daemon` with `ExecStart={{BINARY_PATH}} --run-daemon`:

  ```ini
  [Unit]
  Description=Ollim Telemetry Daemon
  After=network.target

  [Service]
  Type=simple
  ExecStart={{BINARY_PATH}} --run-daemon
  Restart=on-failure
  RestartSec=5

  [Install]
  WantedBy=default.target
  ```

- [ ] **Step 2: Update `assets/com.ollim.plist.template`**

  Replace the `<string>daemon</string>` argument with `<string>--run-daemon</string>`:

  ```xml
  <?xml version="1.0" encoding="UTF-8"?>
  <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
  <plist version="1.0">
  <dict>
      <key>Label</key>
      <string>com.ollim</string>

      <key>ProgramArguments</key>
      <array>
          <string>{{BINARY_PATH}}</string>
          <string>--run-daemon</string>
      </array>

      <key>RunAtLoad</key>
      <true/>

      <key>KeepAlive</key>
      <true/>

      <key>StandardOutPath</key>
      <string>/tmp/com.ollim.stdout.log</string>

      <key>StandardErrorPath</key>
      <string>/tmp/com.ollim.stderr.log</string>
  </dict>
  </plist>
  ```

- [ ] **Step 3: Commit**

  ```bash
  git add assets/ollim.service.template assets/com.ollim.plist.template
  git commit -m "fix(assets): update service templates to use --run-daemon flag"
  ```

---

## Task 7: Validate NativeAOT build and smoke-test

**Files:** None changed — validation only.

- [ ] **Step 1: NativeAOT publish for linux-x64**

  ```bash
  dotnet publish src/OllimTelemetry.Cli/OllimTelemetry.Cli.csproj \
    -c Release -r linux-x64 --self-contained true /p:PublishAot=true \
    -o dist/linux-x64 2>&1
  ```

  Expected: publish completes. Warnings from `Spectre.Console` (`IL2104`) are acceptable. Zero errors. Zero warnings from `ConsoleAppFramework`.

- [ ] **Step 2: Verify `--help` lists all 7 commands and no `--run-daemon`**

  ```bash
  ./dist/linux-x64/ollim --help
  ```

  Expected output contains: `start`, `stop`, `status`, `config`, `stats`, `unlink`, `uninstall`. Does NOT contain `daemon` or `run-daemon`.

- [ ] **Step 3: Verify `--run-daemon` starts the daemon loop**

  ```bash
  timeout 3 ./dist/linux-x64/ollim --run-daemon 2>&1 || true
  ```

  Expected stderr: `[ollim] daemon started` followed by `[ollim] daemon stopped` after the 3-second timeout kills the process.

- [ ] **Step 4: Verify individual commands execute without crash**

  ```bash
  ./dist/linux-x64/ollim status
  ./dist/linux-x64/ollim stats
  ./dist/linux-x64/ollim config
  ```

  Expected: each command runs and exits 0 (or exits 1 for `status` if daemon is not running — that's correct behavior).

- [ ] **Step 5: Update ROADMAP and STATE**

  In `ROADMAP.md` no new checkboxes needed — this is a technical fix, not a feature.

  In `.specs/project/STATE.md`, mark the two NativeAOT todos as resolved:

  ```markdown
  - [x] Verify `Microsoft.Data.Sqlite` NativeAOT compatibility — needs `dotnet publish --aot` test run
  - [x] Verify `Spectre.Console 0.49` NativeAOT compatibility — `TrimmerRoots.xml` placeholder created, may need entries
  ```

  And add a new lesson under `## Lessons`:

  ```markdown
  - Spectre.Console.Cli is not NativeAOT-compatible (reflection-based command settings discovery). ConsoleAppFramework (source-generated) is the correct replacement. Spectre.Console (rich UI) is fine.
  ```

- [ ] **Step 6: Final commit**

  ```bash
  git add .specs/project/STATE.md
  git commit -m "fix(nativeaot): replace Spectre.Console.Cli with ConsoleAppFramework — closes NativeAOT crash"
  ```
