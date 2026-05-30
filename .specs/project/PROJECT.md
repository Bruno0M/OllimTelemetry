# Ollim Telemetry — Project Vision

## What it is

An open-source CLI daemon written in C# (.NET 10 / NativeAOT) that reads Claude Code's local token usage logs and, with explicit opt-in, submits anonymized token counts to a global leaderboard at ollim.dev.

Distributed as a single self-contained native binary — no runtime required on the user's machine.

## Core value

**Privacy first.** The daemon reads ONLY the `usage` field from Claude Code's JSONL logs — never message content. All data sharing is explicit opt-in, stored locally in `~/.ollim/config.json`. The user can unlink or uninstall at any time with a single command.

## Goals (MVP)

- **G1** — Parse Claude Code token usage logs from `~/.claude/projects/**/*.jsonl` without reading message content
- **G2** — Persist parsed usage locally in a SQLite queue (offline-safe, survives crashes)
- **G3** — Submit anonymized token counts to `api.ollim.dev` only when the user opts in
- **G4** — Register as a native OS daemon (launchd on macOS, systemd on Linux) so it starts on login
- **G5** — Provide a clean CLI (`ollim start|stop|status|config|stats|unlink|uninstall`)
- **G6** — Produce a single NativeAOT binary per platform (osx-arm64, osx-x64, linux-x64, linux-arm64)

## Non-goals (MVP)

- Windows support (graceful "not supported" message only)
- Repo name or project context sharing (future opt-in via `ShareRepoName`)
- Codex / Gemini agent support (schema is ready via `Agent` field, implementation deferred)
- Web dashboard or leaderboard UI (backend responsibility)
- Auto-update mechanism

## Tech stack

| Concern | Choice | Why |
|---|---|---|
| Runtime | .NET 10 + NativeAOT | Single binary, no runtime dependency |
| Language | C# 14, nullable, implicit usings | Type safety, modern records |
| CLI | Spectre.Console + Spectre.Console.Cli | Rich terminal UI, command routing |
| File watching | System.IO.FileSystemWatcher | Zero-polling, OS-native events |
| Config | System.Text.Json + source generators | NativeAOT-safe (no reflection) |
| Local queue | Microsoft.Data.Sqlite + SQLitePCLRaw | Offline-safe, embedded, reliable |
| HTTP | System.Net.Http.HttpClient | Built-in, no extra dependency |
| Daemon | launchd (macOS) / systemd --user (Linux) | Native OS service managers |

## Key constraints

1. **Zero content logging** — `// PRIVACY: usage only` on every `JsonDocument` property access in `LogParser`
2. **NativeAOT compatibility** — no reflection at runtime; source generators for all serialization
3. **Layer isolation** — `Core` never references Spectre.Console; `Models` references nothing
4. **Offline-safe** — HTTP failures never crash the daemon; failed syncs stay queued with exponential backoff (max 1h)
5. **Low resource** — < 10 MB RAM, 0% CPU when idle (event-driven, no polling)
6. **Graceful shutdown** — flush in-memory buffer to SQLite on SIGTERM / SIGINT

## Project layout

```
OllimTelemetry/
├── OllimTelemetry.sln
├── src/
│   ├── OllimTelemetry.Cli/       # Entrypoint, commands, onboarding
│   ├── OllimTelemetry.Core/      # Parser, watcher, queue, sync, daemon, config
│   └── OllimTelemetry.Models/    # Shared data records (no dependencies)
├── assets/                        # OS service templates
└── scripts/                       # build.sh (NativeAOT publish)
```

## Dependency graph

```
Cli → Core → Models
Cli → Models
Core → Models
```

`Core` must never reference Spectre.Console. All terminal I/O lives in `Cli`.
