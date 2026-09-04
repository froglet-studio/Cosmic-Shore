# Diagnostics — the crash detector, the shared bug ledger, and compile timing

**FrogletTools ▸ Diagnostics** is one window with four tabs:

| Tool | Question it answers | Data it writes |
|---|---|---|
| **Crash Detector** | *Why did my editor die?* — even when Unity itself never got to say | `Logs/CrashDetector/` (machine-local, gitignored) |
| **Bug Ledger** (+ its **Stage & Push** tab) | *What is broken right now, and is the fix actually proven?* | `BugLedger/local/` (gitignored live store) → published to the tracked `BugLedger/shared/` only through the Stage & Push tab |
| **Compile Timing** | *What does an edit cost?* — compile + domain-reload seconds, and which assemblies rebuilt | `Logs/CompileTiming/` (machine-local, gitignored) |

The first two are always on. **Compile Timing is opt-in and off by default** — it exists to
measure the assembly split (`Docs/ASSEMBLY_SPLIT.md` § Measuring), so it is switched on for a
measurement run and back off afterwards rather than left recording.

The crash detector and the ledger exist because of the same gap: editor crashes and red-console bugs were disappearing into
scrollback and memory. Unity's own Crashlytics does not surface error detail without payment, so
the crash story is handled in-repo; the in-game half (devices writing the same signatures into UGS
player data) is planned and already has its shared core (`BugSignature`, below).

Everything here follows the `Docs/TOOLING.md` authoring contract — palette-only UI, config in
ScriptableSingletons under `UserSettings/`, headline-only console output, and no `Assets/` writes
(so neither tool draws a ship panel).

---

## The Crash Detector

An always-on watchdog for THIS editor process. It cannot stop a crash; it guarantees the crash
leaves evidence.

### Mechanism

1. **On editor load** (`[InitializeOnLoad]` → `CrashDetectorMonitor`), a session **sentinel**
   (`Logs/CrashDetector/session.state`) is written: session id, pid, Unity version, start time,
   editor state, `cleanExit=false`.
2. **One background thread** (BelowNormal priority) does all the work:
   - drains a concurrent queue of captured errors/exceptions/asserts into an append-only
     **journal**, flushing after every drain so the tail survives a hard death;
   - re-stamps the sentinel every few seconds (**heartbeat**), including a **main-thread liveness
     stamp** written by `EditorApplication.update`.
3. **Capture** rides `Application.logMessageReceivedThreaded` — it works from ANY thread, so
   nothing depends on the main thread staying responsive. When the main thread is the thing dying
   (or hanging), the journal and heartbeat keep landing. That is the design's whole point.
4. **A clean exit** (`EditorApplication.quitting`) marks the sentinel clean. A **domain reload**
   re-adopts the same session by pid (threads die with the domain; the monitor restarts them).
5. **On the next editor launch**, the previous sentinel is judged:
   - marked clean → verdict "clean";
   - owned by a live Unity process (pid reuse / multi-instance) → no verdict, never cry crash on
     a maybe;
   - otherwise → **the previous session ended abnormally** (Unity crash, PC fault, force-kill,
     hang-then-kill). A `Crash-*.log` report is written from the stale sentinel + the captured
     journal + the tail of Unity's own `Editor-prev.log`, and one console warning names it.
6. The heartbeat-vs-main-thread-stamp gap lets the report distinguish **"died abruptly"** from
   **"was hung for ~N s, then killed"**.
7. **A hang dumps itself** (2026-08-21): when the main-thread liveness stamp goes stale past the
   settings threshold (default 45 s), the writer thread writes a live **minidump** —
   `Logs/CrashDetector/HangDump-<session>.dmp` (Windows only, once per session, pruned with the
   reports; thread stacks + module list, a few MB). Open it in Visual Studio / WinDbg: the main
   thread's stack names the deadlock a Task-Manager kill would otherwise destroy, and the
   next-launch report links the dump. A stall that RECOVERS is journaled as "slow, not hung"
   instead. To make this cover the phase where the 2026-08-21 hangs actually lived, the writer
   no longer stands down at `beforeAssemblyReload` — it stamps the sentinel `DomainReload` and
   keeps running until the domain unload aborts it (handled; the journal flushes in `finally`),
   so a reload that wedges in "Run managed callbacks" still gets dumped.

Guard rails: an error storm is rate-capped (20/s, excess collapsed to one count), the journal is
size-capped (drops with one marker past the cap), reports are pruned to a count, and the whole
boot is wrapped so the watchdog can never take the editor down with it.

### Reading a crash report

Top block: when it died, what state it was in (`EditMode` / `PlayMode` / `DomainReload`), the
hang-vs-abrupt verdict. Then the captured error journal (what the console was saying before
death), then Unity's own log tail (native errors, driver faults, OOM — things no C# hook sees).
The **File Bug** button on a report row tracks the crash as a Bug Ledger issue (deduped by report
filename, severity `blocker`).

### Settings (machine-local)

Enable, heartbeat seconds, capture-warnings, stack lines per entry, reports kept, journal cap,
hang-dump threshold seconds (0 = off).
`UserSettings/CrashDetectorSettings.asset` — never on the branch.

### Limitations

- The detector reports on the NEXT launch; there is no in-flight rescue.
- A crash during the few ms of a domain reload reads as `last state: DomainReload` — correct but
  coarser.
- If detection is off, the sentinel is deleted so a later session can't misjudge; nothing watches
  until it is re-enabled.

---

## The Bug Ledger

The team's live bug list, inside the editor, with one hard rule: **a fix is not believed until
the game proves it.**

### The store — why files, why there, and why two of them

One small JSON file per issue, at the **project root**, in a split store:

```
BugLedger/local/issues/    ← gitignored. The LIVE store: every write the ledger ever makes.
BugLedger/local/resolved/  ← gitignored. Closed issues (archive; doubles as the tombstone set).
BugLedger/shared/issues/   ← tracked. The PUBLISHED set — written only during a Commit & Push.
```

- outside `Assets/` → Unity never imports it, no `.meta` churn, invisible to the asset pipeline;
- **git never sees the ledger working** → auto-capture, occurrence updates, fixes, validation and
  resolution all land in the gitignored `local/`; `git status` stays clean however hard the
  ledger is used. A change reaches version control only through the **Stage & Push** tab;
- **merge-friendly** → different bugs are different files (no conflicts possible); one field per
  line in a stable order (a diff is exactly the fields that changed); a reviewer only sees the
  issues a push actually published;
- **cross-machine deduped** → an auto issue's id derives from its error signature, so the same
  bug lands in the SAME file on every machine;
- **synced down** → teammates' published issues import from `shared/` into your local store on
  load/refresh, unless you already have them live or tombstoned (resolved/deleted) — a bug you
  closed cannot resurrect off a teammate's still-shared copy.

Serialization is hand-rolled both ways (`BugLedgerIssue`): the capture path runs on a background
worker where `JsonUtility` is unsafe, and the parser tolerates unknown keys, missing keys and
garbage files (an unreadable file is skipped, never fatal). Store contract for teammates:
`BugLedger/README.md`.

### Signatures — the identity of a bug

`BugSignature` (**runtime-safe**, `Assets/_Scripts/Utility/`, `CosmicShore.Utility`) turns an
error into a deterministic fingerprint:

```
E-<hash10> = MD5( LogType | normalized message | normalized top user frame )
T-<hash10> = MD5( "Tool" | tool name | normalized finding title )
```

Normalization collapses digit runs (`retry 3` → `retry #`), hex runs (`0xDEADBEEF` → `0x#`), and
cuts machine-local absolute paths back to `Assets/…` in BOTH stack-frame formats (mono-style
`" in <path>:line"` and unity-style `"(at <path>:line)"`). Volatile parts vanish; the bug's
identity survives. Pinned by `BugSignatureTests` (edit mode) — including the cross-OS frame case,
because the first implementation failed it.

It lives in the runtime assembly ON PURPOSE: the planned in-game reporter will hash device-side
errors with the same code and write them into UGS player data, so device hits can merge into the
same ledger issues. Keep it a pure function — no editor types, no statics with lifecycle.

### How issues get filed

| Route | Id | How |
|---|---|---|
| **Auto-capture** | `E-…` | Every distinct error/exception/assert signature files itself, once — captured via `logMessageReceivedThreaded` into a background worker (the log path never blocks on file IO). A thousand repeats update one issue. |
| **Custom** | `C-…` | The window's **+ New Bug** form — for anything auto-capture cannot see ("the quest screen is empty"). Title, notes, severity. |
| **Tool findings** | `T-…` | `BugLedger.ReportFromTool` / `ReportToolFindings` — auditors and validators file their failures as tracked issues. The crash detector's File Bug button uses the same route. |

Auto-capture guard rails: own `[BugLedger]`/`[CrashDetector]` console lines are excluded, a
bounded queue absorbs storms (dedupe makes drops harmless), an **auto-issue cap** stops a runaway
error generator from minting files, and occurrence updates are throttled to **one file write per
issue per editor session** — so a published issue doesn't flip to pending-MOD on every single
occurrence.

### The lifecycle

```
            auto / custom / tool
                    │
                    ▼
                 ┌──────┐   Mark Fixed    ┌────────────┐   clean-session quota met
      ┌────────► │ OPEN │ ──────────────► │ VALIDATING │ ─────────────────────────► resolved
      │          └──────┘                 └────────────┘                            (archived,
      │              │                      │       │                               removed from
      │            Ignore                 pause   error recurs                      the live ledger)
      │              ▼                    (frozen)  │
      │          ┌─────────┐                        │  ← reopened as a REGRESSION,
      │          │ IGNORED │                        │    console warning, count++
      │          └─────────┘                        │
      └─────────────────────────────────────────────┘
```

- **Mark Fixed** stamps who/when and moves the issue to VALIDATING (a signatureless custom issue
  resolves via a confirm instead — there is nothing to auto-validate).
- **Validation evidence** is scope-matched, and only counts while auto-capture is ON (a clean
  session proves nothing while nothing listens):
  - `PlayMode` issues: each play run ≥ the minimum length where the signature stays silent
    counts one clean session (a bounded queue drain runs before the verdict so an in-flight
    error can't be miscounted);
  - `EditMode` issues: a full editor session ≥ the minimum length, credited at clean quit;
  - `Tool` issues: **the tool that filed it is the validator** — a FULL clean re-run of that tool
    resolves its validating findings (quota 1: a deterministic re-run is stronger evidence than
    any number of play sessions). Partial sweeps must not call `ReportToolFindings`.
- At the issue's own `cleanSessionsRequired` (stamped at creation, so the policy is deterministic
  per issue) it is **resolved**: removed from the live store, stamped (`state: resolved`,
  `resolvedUtc`, `resolution`) into **`BugLedger/local/resolved/`** (pruned to a cap), and one
  console line says so. The stamp is unconditional — it is also the TOMBSTONE that keeps the
  sync-down from re-importing a shared copy, which is why deleting an issue writes one too. If
  the issue had been published, its shared file becomes a pending **DEL** to stage.
- **A recurrence while validating reopens the issue as a regression** — the loudest thing the
  ledger says, because a fix that didn't fix is its whole reason to exist.
- Per issue: **pause/resume validation** (nothing counts either way), **Ignore** (parks it;
  matching errors never reopen it and the signature is never re-filed), **Resolve Now**
  (bypass), **Delete** (discard outright; if the error is real it will re-file), editable notes,
  and a click-to-cycle **severity** (`blocker` / `major` / `minor` — sorts within each state).

### Staging and pushing

The **Stage & Push** tab is the ledger's own version-control surface (`BugLedgerStageView` over
`BugLedgerPublisher`). It shows the difference between your local live store and the published
`shared/` set as **pending changes**:

| Badge | Meaning |
|---|---|
| `ADD` | live locally, never published |
| `MOD` | published, but your local copy differs (new occurrences, state, notes…) |
| `DEL` | published, but resolved/deleted locally — staging removes the shared file |

Stage a selection with the per-row **`+` / `−`** buttons (or Stage All / Unstage All), write a
**commit comment**, and press **Commit & Push**. Staging is a UI selection until that moment —
nothing touches a tracked file while you are still choosing. The publish then runs on a background
thread with a **step progress bar**, exactly like any VCS client:

```
fetch origin → check branch state → publish staged files into shared/ → git add → commit → push
```

The guarantees, in order of importance:

- **Scope**: `git add` takes the explicit staged ledger paths (plus the store's README and
  `.gitignore`) — never `-A`, never a wildcard — and the commit carries the same pathspec, so
  nothing else in your working tree, staged or not, can ride along. This tool can only ever
  commit bug data.
- **Safety before commit**: if the fetch shows `origin/<branch>` ahead, the publish stops BEFORE
  applying or committing anything and asks you to pull in your git client first — it never pulls
  or rebases your repo itself.
- **Branch policy**: pushes go to the CURRENT branch after a confirm dialog that names it.
  Deliberate deviation from the ship panel's protected-branch refusal: ledger issues are team
  DATA, not tool output — "file a bug the whole team sees" must not require a feature branch and
  a PR.
- **Fetch** (its own button) is read-only: `git fetch origin`, an ahead/behind readout, and the
  list of incoming ledger files that will land on your next pull.
- A failed push after a successful commit says so explicitly — the commit exists locally, and
  pressing Commit & Push again (or pushing from any git client) finishes the job.

The Bug Ledger tab shows an **N UNPUBLISHED** pill whenever local and shared differ — clicking it
jumps here.

### Honest limits

- A clean session only proves the code paths that session exercised. Two clean sessions of menu
  idling do not validate a SkimRace bug — the quota and minimum lengths reduce, not remove, this;
  pause validation on an issue you know needs a targeted repro.
- Signature normalization is best-effort: bare GUID-ish tokens without `0x` survive partially, so
  a message embedding raw GUIDs can split one bug across ids. The frame half usually holds the
  identity anyway.
- Two machines PUBLISHING the same auto-filed signature before either pushes is the one add/add
  conflict possible in `shared/`; either side can be kept (durable fields agree, counters are
  advisory). Day-to-day capture can never conflict — it never leaves `local/`.
- Tombstones are pruned past a cap (oldest first), so an issue resolved ages ago whose shared
  copy was never staged for removal could eventually re-import. Stage your DELs.
- "Not from the flow" failures that never log an error are invisible to auto-capture by
  definition — that is what custom bugs and `ReportFromTool` are for. The cheapest way to make a
  silent failure trackable is to make the system log a real error.

### For tool authors

```csharp
// One finding (deduped — same tool+title = same issue on every run/machine):
BugLedger.ReportFromTool("My Auditor", $"{prefab.name}: thing is broken", details);

// A FULL run's findings — files/refreshes each, and auto-resolves the validating
// findings this run no longer reports:
BugLedger.ReportToolFindings("My Auditor", findings);
```

Reference integration: `VesselSkimmerAudit` — collects findings during the sweep, offers filing
via one dialog when faults exist (committable data is the human's call), and credits silently on
a clean run. Copy that shape.

### In-editor verification (5 minutes)

1. **Crash tab ▸ Test Error** → the entry appears in the session journal (Open Journal).
2. Kill the editor from the OS while it runs → relaunch → red verdict card + a `Crash-*.log`
   containing the journal and Unity's log tail.
3. **Bugs tab ▸ Test Capture** → one `E-…` issue appears (and only one, however many times you
   click) — and `git status` stays CLEAN, because the live store is gitignored. **Mark Fixed** →
   VALIDATING. Two play runs ≥ the minimum length → the issue resolves itself, archived under
   `BugLedger/local/resolved/`.
4. Test Capture again *while* validating → the issue reopens as a regression with a console
   warning.
5. **Vessels ▸ Audit Vessel Skimmers** with a known fault → dialog files `T-…` findings; fix the
   fault, re-run the audit clean → the findings resolve themselves.
6. **Stage & Push tab** → the surviving issues show as pending ADDs; stage one with `+`, comment,
   **Commit & Push** → the progress bar walks fetch → publish → add → commit → push, and exactly
   one file appears in `BugLedger/shared/issues/` on the branch. Resolve that issue and its DEL
   shows up here to stage.

---

## Files

| Role | Path |
|---|---|
| Crash monitor (watchdog, `[InitializeOnLoad]`) | `Assets/_Scripts/Editor/Diagnostics/CrashDetectorMonitor.cs` |
| Crash settings (`ScriptableSingleton`) | `Assets/_Scripts/Editor/Diagnostics/CrashDetectorSettings.cs` |
| Ledger core (capture, store, validation, tool findings) | `Assets/_Scripts/Editor/Diagnostics/BugLedger.cs` |
| Issue model + hand-rolled JSON | `Assets/_Scripts/Editor/Diagnostics/BugLedgerIssue.cs` |
| Ledger settings (`ScriptableSingleton`) | `Assets/_Scripts/Editor/Diagnostics/BugLedgerSettings.cs` |
| Ledger tab view | `Assets/_Scripts/Editor/Diagnostics/BugLedgerView.cs` |
| Stage & Push tab view | `Assets/_Scripts/Editor/Diagnostics/BugLedgerStageView.cs` |
| Scoped git publisher (fetch/apply/add/commit/push, off-thread) | `Assets/_Scripts/Editor/Diagnostics/BugLedgerPublisher.cs` |
| Compile-timing recorder (`[InitializeOnLoad]`, opt-in) | `Assets/_Scripts/Editor/Diagnostics/CompileTimingMonitor.cs` |
| The window (all four tabs) | `Assets/_Scripts/Editor/Diagnostics/DiagnosticsWindow.cs` |
| Live-store gitignore (committed, self-healed by the tool) | `BugLedger/.gitignore` |
| Shared signature core (**runtime-safe**) | `Assets/_Scripts/Utility/BugSignature.cs` |
| Signature determinism tests (edit mode) | `Assets/_Scripts/Tests/Editor/BugSignatureTests.cs` |
| Committed store contract | `BugLedger/README.md` (project root) |
| Crash logs / reports (machine-local) | `Logs/CrashDetector/` (gitignored) |

Every Diagnostics card on the Froglet Master Tool carries a **DOCS chip** linking straight to
this page on GitHub (`FrogletDocLinks`); the window's **Docs** button does the same.
