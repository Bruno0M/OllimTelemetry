# Ollim Telemetry — State

## Decisions

| ID | Decision | Rationale | Date |
|----|----------|-----------|------|
| D-01 | NativeAOT via `PublishAot=true` on Cli project only | Core and Models are class libraries; only the binary entrypoint needs AOT | 2026-05-29 |
| D-02 | Source generators required for all JSON serialization | NativeAOT prohibits runtime reflection; `ConfigJsonContext` covers all serialized types | 2026-05-29 |
| D-03 | `FileSystemWatcher` with recursive=true on `~/.claude/projects/` | Claude Code can create new project hashes at any time; watching the parent directory catches all session files | 2026-05-29 |
| D-04 | SQLite stores byte offsets per file | Enables delta reads on file change without re-parsing entire log; survives daemon restarts | 2026-05-29 |
| D-05 | `Timestamp` field IS read from logs (it's in `TokenUsage`) | Timestamp is operational metadata needed for period aggregation; it is not content. Confirmed acceptable. | 2026-05-29 |
| D-06 | `LogParser` reads sibling `timestamp` field from JSONL line (not just `usage`) | Approved as operational metadata required for `SyncBatch.PeriodStart/End`. Must be annotated with `// PRIVACY: timestamp only — no content` in addition to the usage guard. | 2026-05-29 |
| D-07 | `UserId` is a stable UUID — generated once, never rotated | Required for consistent leaderboard identity. Reset path: `ollim unlink` + `ollim start` (re-enrollment generates new UUID because config is wiped). | 2026-05-29 |
| D-08 | `LogWatcher` uses `IncludeSubdirectories=true` on `~/.claude/projects/` | Catches all project hashes automatically including new sessions created after daemon starts. | 2026-05-29 |
| D-09 | `ollim unlink` sets `ShareGlobal=false` — does NOT delete `UserId` | User retains same leaderboard identity on re-enrollment. For full identity break, user must uninstall. | 2026-05-29 |

## Open Questions (privacy-sensitive — must answer before implementing)

_All questions resolved — see Decisions D-06 through D-09._

## Blockers

_None._

## Todos

- [x] Answer Q-01 through Q-04 — resolved 2026-05-30
- [ ] Verify `Microsoft.Data.Sqlite` NativeAOT compatibility — needs `dotnet publish --aot` test run
- [ ] Verify `Spectre.Console 0.49` NativeAOT compatibility — `TrimmerRoots.xml` placeholder created, may need entries
- [ ] Confirm Claude Code JSONL schema stability (the `usage` field structure)
- [ ] Add a `daemon` sub-command (the plist/service calls `ollim daemon` as the long-running entrypoint — not yet wired)

## Deferred Ideas

- `ollim leaderboard` — view global rankings in terminal (Milestone 3)
- Per-repo token breakdown (requires `ShareRepoName` opt-in)
- Token cost estimation (model pricing × token count, shown in `ollim stats`)
- `--dry-run` flag for `ollim start` to show what would be submitted without sending

## Preferences

_None recorded yet._

## Lessons

_None yet._
