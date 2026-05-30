# Design: Replace Spectre.Console.Cli with ConsoleAppFramework

**Date:** 2026-05-30
**Status:** Approved
**Scope:** `OllimTelemetry.Cli` only — Core and Models are untouched

---

## Problem

`Spectre.Console.Cli` uses reflection at runtime (`GetInterfaces()`, `GetGenericArguments()`) to resolve command settings types. NativeAOT strips the required reflection metadata, causing a crash at startup:

```
Unhandled exception. Spectre.Console.Cli.CommandRuntimeException:
Could not get settings type for command of type 'OllimTelemetry.Cli.Commands.StartCommand'.
```

Neither `TrimmerRoots.xml` nor `IlcRootDescriptor` entries were sufficient to preserve the generic interface metadata the library requires.

---

## Decision

Replace `Spectre.Console.Cli` with **ConsoleAppFramework** (Cysharp/ConsoleAppFramework v5).

ConsoleAppFramework is source-generator based — the CLI router and argument parser are generated at compile time with zero runtime reflection. It is fully NativeAOT compatible by design.

`Spectre.Console` (the rich terminal UI — tables, `AnsiConsole`, prompts) is **kept**. It is a separate package and is not the source of the crash.

---

## Architecture

### Initialization flow

```
ollim [args]
  ├─ args contains "--run-daemon"?
  │     └─ DaemonRunner.RunAsync(CancellationToken)   ← bypasses ConsoleAppFramework
  └─ ConsoleApp.Create()
        └─ app.Add() × 7 public commands
        └─ app.Run(args)
```

The `--run-daemon` check runs **before** ConsoleAppFramework so the daemon loop is never registered as a public command and never appears in `--help`.

### Why a flag instead of a sub-command

The OS service manager (launchd / systemd) needs an entry point for the long-running process. The previous design exposed this as a hidden CLI sub-command (`ollim daemon`), which required framework-level hidden-command support and leaked an internal concept into the public command surface.

With option A (flag), the daemon entry point is a private implementation detail: `Program.cs` checks `args.Contains("--run-daemon")` before handing off to ConsoleAppFramework. The OS service templates are updated accordingly.

---

## Component Changes

### `Program.cs` (rewritten)

```csharp
if (args.Contains("--run-daemon"))
{
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };
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

### Command files (refactored)

Each `Commands/XCommand.cs` file changes from:

```csharp
public sealed class XCommand : AsyncCommand
{
    public override Task<int> ExecuteAsync(CommandContext context) { ... }
}
```

To:

```csharp
internal static class XCommand
{
    public static Task<int> RunAsync() { ... }
}
```

No logic changes — only the class shape changes. All `AnsiConsole` and `Spectre.Console` calls remain identical.

### `DaemonCommand.cs` → `Daemon/DaemonRunner.cs`

The daemon loop is extracted from the old `DaemonCommand` into a plain static class:

```csharp
internal static class DaemonRunner
{
    public static async Task RunAsync(CancellationToken ct) { ... }
}
```

SIGTERM/SIGINT handling moves to `Program.cs` (before the ConsoleAppFramework call), so `DaemonRunner` only receives a `CancellationToken` and awaits it.

### `OllimTelemetry.Cli.csproj`

```xml
<!-- Remove -->
<PackageReference Include="Spectre.Console.Cli" Version="0.49.*" />

<!-- Add -->
<PackageReference Include="ConsoleAppFramework" Version="5.*" />
```

### `TrimmerRoots.xml`

```xml
<linker>
  <assembly fullname="ollim" preserve="all" />
</linker>
```

The `Spectre.Console.Cli` entry added during debugging is removed. The `ollim` entry is kept as a conservative safety net; can be removed after NativeAOT validation passes.

### OS service templates

| File | Before | After |
|------|--------|-------|
| `assets/ollim.service.template` | `ExecStart=... ollim daemon` | `ExecStart=... ollim --run-daemon` |
| `assets/com.ollim.plist.template` | `<string>daemon</string>` | `<string>--run-daemon</string>` |

---

## What is NOT changing

- `OllimTelemetry.Core` — zero changes
- `OllimTelemetry.Models` — zero changes
- `OllimTelemetry.Tests` — zero changes
- `OnboardingFlow.cs` — zero changes
- All `AnsiConsole` / `Spectre.Console` calls inside commands — zero changes

---

## Validation Criteria

1. `dotnet publish -r linux-x64 --aot` completes without CAF-related warnings
2. `./ollim --help` lists all 7 public commands and exits 0
3. `./ollim --run-daemon` starts the daemon loop (verified via stderr output)
4. `./ollim start` / `stop` / `status` / `config` / `stats` / `unlink` execute correctly
5. `./ollim uninstall` shows confirmation prompt (Spectre.Console still works)
6. No `--run-daemon` entry in `--help` output
