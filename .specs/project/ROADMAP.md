# Ollim Telemetry — Roadmap

## Milestone 1: MVP — Native binary ships (current)

### Phase 1 — Foundation (scaffold + models + config)
- [ ] **F-01** Solution scaffold — sln, 3 projects, project references, NuGet packages
- [ ] **F-02** Models layer — `TokenUsage`, `SyncBatch`, `SubmitPayload` records
- [ ] **F-03** Config layer — `AppConfig` record, `ConfigJsonContext` source gen, `ConfigManager`

### Phase 2 — Core engine
- [ ] **F-04** Log parser — JSONL delta reader, byte offset tracking, `// PRIVACY: usage only` guards
- [ ] **F-05** SQLite queue — `SyncQueue` with pending batches, offset storage, exponential backoff state
- [ ] **F-06** File watcher — `LogWatcher` wrapping `FileSystemWatcher`, 500ms debounce, recursive watch of `~/.claude/projects/`
- [ ] **F-07** Sync service — `SyncService` flushing queue to `POST /v1/submit` on interval

### Phase 3 — OS daemon
- [ ] **F-08** Daemon manager — `DaemonManager` for launchd (macOS) + systemd --user (Linux)
- [ ] **F-09** Graceful shutdown — SIGTERM + SIGINT handlers flush SQLite before exit

### Phase 4 — CLI
- [ ] **F-10** Onboarding flow — interactive first-run opt-in via Spectre.Console prompts
- [ ] **F-11** Commands — `start`, `stop`, `status`, `config`, `stats`, `unlink`, `uninstall`
- [ ] **F-12** Program.cs — Spectre.Console.Cli app wiring

### Phase 5 — Build & distribution
- [ ] **F-13** Build script — `scripts/build.sh` NativeAOT publish for all 4 RIDs
- [ ] **F-14** OS service templates — `com.ollim.plist.template`, `ollim.service.template`

---

## Milestone 2: Phase 2 — Wider distribution

- [ ] `dotnet tool install -g ollim` packaging
- [ ] npm wrapper (thin binary downloader, esbuild/Biome pattern)
- [ ] GitHub Actions CI — build + release on tag push

## Milestone 3: Phase 3 — Ecosystem

- [ ] Homebrew tap (`brew install ollim-telemetry/tap/ollim`)
- [ ] Codex / Gemini agent support (log path detection per agent)
- [ ] Repo name opt-in (`ShareRepoName = true`)
- [ ] `ollim leaderboard` command (view rankings in terminal)

---

## Implementation order (MVP)

1. F-01 Solution scaffold
2. F-02 Models
3. F-03 Config layer
4. F-04 Log parser
5. F-05 SQLite queue
6. F-06 File watcher
7. F-07 Sync service
8. F-08 Daemon manager
9. F-09 Graceful shutdown (part of F-08 / F-12 wiring)
10. F-10 Onboarding flow
11. F-11 Commands
12. F-12 Program.cs wiring
13. F-13 Build script
14. F-14 OS service templates
