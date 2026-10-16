# Diagnostics — the crash detector, the shared bug ledger, compile timing, and the on-screen UI report

**FrogletTools ▸ Diagnostics** is one window with four tabs, plus one standalone report:

| Tool | Question it answers | Data it writes |
|---|---|---|
| **Crash Detector** | *Why did my editor die?* — even when Unity itself never got to say | `Logs/CrashDetector/` (machine-local, gitignored) |
| **Bug Ledger** (no window, no question: new errors and captures go to Claude by themselves after a play test) | *What is broken right now, and is the fix actually proven?* | `BugLedger/local/` (gitignored live store) → sent to the sender's own `bugledger/<user>` branch |
| **Compile Timing** | *What does an edit cost?* — compile + domain-reload seconds, and which assemblies rebuilt | `Logs/CompileTiming/` (machine-local, gitignored) |
| **Report On-Screen UI** (menu item, not a tab) | *What is that thing covering my screen?* — every enabled `Graphic` ≥2% of the display, biggest first | nothing — console + clipboard |

## Report On-Screen UI

**Run it in play mode, on the bad frame.** "There is UI in the way" is a report about a RENDERED
FRAME, and a rendered frame is the one thing static analysis of scenes and prefabs cannot see: a
panel is on screen because of the product of several things living in different files — is its
GameObject active, is its `Graphic` enabled, what did every `CanvasGroup` above it multiply its
alpha by, which `Canvas` draws it and in what order, and did some controller leave it that way.
Reading any one of those in isolation invites a confident wrong answer; three of those in a row is
what this tool is a reaction to.

It lists every enabled `Graphic` covering at least 2% of the display, biggest first, with the full
hierarchy path, the effective alpha and WHICH `CanvasGroup` set it, whether it eats clicks, its
texture/sprite, and its canvas — the offender is normally the first line. Two things that are not
`Graphic`s get their own sections, because both draw over the game and neither appears in a UI
hierarchy: cameras rendering into a `RenderTexture` or restricted to a partial viewport rect, and
`VideoPlayer`s set to draw on a camera plane.

The find that paid for it: `GameCanvas/MiniGameHUD/Pip/border`, a `RawImage` reported as
`tex:NONE` at 55% coverage — a frame graphic whose texture asset had been deleted, so it was
painting a solid navy quad over half the screen (see CLAUDE.md's anti-pattern entry). It is a
READER: it writes no assets, so it carries no ship panel.

---

The first three are always on. **Compile Timing is opt-in and off by default** — it exists to
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

The team's live bug list, with one hard rule: **a fix is not believed until the game proves it.**

**It has no window and nothing to configure (2026-10-15).** Yash, after using the first version:
*"We cannot have this type of diagnostic panels. It's too much... just after I stop the game, it
shows me a panel: send console logs to Claude... no complex UI toolboxes in Unity."* So the whole
UI is ONE question after a play test (§ "Send to Claude"); capture, fixing, validation and
reopening all run unseen. The Bug Ledger window, its Stage & Push tab, the daily check-in reminder
and every per-issue button (Fix / Ignore / Reopen / pause / delete / severity) were deleted.

### `timesSeen` is a FLOOR, and it is most wrong for the issue you care about

The capture path carries a storm guard — `OnLogMessage` drops an entry outright once the worker
queue is full (`Queue.Count >= MaxQueuedEntries`) — so `TimesSeen` counts what got *through*, not
what happened. That is the right trade for the ledger's job (dedupe makes drops harmless: one
surviving entry per signature still files the issue), and it has one consequence worth stating
because it inverts the obvious reading:

> **A signature that is storming is the signature whose count is most suppressed.** It saturates
> the queue, so most of its own occurrences are the ones dropped — while a once-per-session error
> beside it is captured every single time.

So **do not rank by `timesSeen` when hunting a rate problem.** Measured on a real export: a
per-frame `MissingReferenceException` out of `MiniGameHUD.Update` — running at roughly 200/s and
visibly eating the frame rate — read **7**, beneath a once-per-domain-reload editor import error
that read **15**. The ledger named the offender correctly and ranked it fifteenth.

Rank by **CALL SITE** instead. Only an unbounded producer can make a storm, and the ledger already
tells you which one a signature is: read its `stack` and ask whether the frame it throws from is an
`Update`, a `LateUpdate`, a `FixedUpdate`, or a per-contact/per-prism dispatch. Everything else —
a scene load, a join failure, an import — is bounded by an event the player has to cause, and
cannot produce the symptom however large its count. For the live rate, use
[`ErrorStormReporter`](#error-storms--errorstormreporter), which is the thing that *can* count:
it aggregates by the same `BugSignature` with no drop guard and prints the ranked window.

One more shape to expect in an export: **a warn-once message whose text carries a varying number
files a NEW issue every session.** `PrismClockDiagnostics.WarnNoRenderEntity` keys its own
suppression on a constant `reasonKey` (correct — one console line per session) but interpolates a
live entity count into the text, and the signature is over the TEXT, so a single well-behaved
diagnostic appeared three times under three ids. Three low-count issues that are one issue is the
tell.

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
  ledger is used. Issues leave the machine only through **Send to Claude**, onto the sender's
  own `bugledger/<user>` branch (never the branch they are working on);
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
| **Custom** | `C-…` | For anything auto-capture cannot see ("the quest screen is empty"): filed on the Team Bug Board artifact (no Unity needed) and imported by the daily sweep with `bug_ledger.py file`. |
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
                 ┌──────┐ fix (in HEAD)   ┌────────────┐   clean-session quota met
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

- **Marked fixed by whoever fixed it** — in practice a Claude session, `bug_ledger.py mark-fixed`,
  which writes VALIDATING plus the fix commit onto the sender's `bugledger/<user>` branch. The
  editor adopts it only once that commit is in its HEAD (§ "Send to Claude").
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
  sync-down from re-importing a shared copy, which is why deleting an issue writes one too. A
  closed issue simply drops out of the next send, so it disappears from the sender's branch.
- **A recurrence while validating reopens the issue as a regression** — the loudest thing the
  ledger says, because a fix that didn't fix is its whole reason to exist.
- The `Ignore`, `Pause`, `Reopen`, `ResolveNow`, `Delete` and `SetSeverity` calls remain in
  `BugLedger`'s API for tools and Claude; no person is asked to drive them.

### Send to Claude — the only UI

`BugHuntAutoSend` (editor, `[InitializeOnLoad]`). When a play test ends and it logged a red error
Claude does not have yet (an OPEN issue seen this run whose `id:regressions` fingerprint was never
sent), or made a new performance capture (`PerfAutoCapture`), AI training record or multiplayer
log, it **sends at once, with no question** (Yash, 2026-10-16: "make send to Claude automatic too, no
question"; it was a Send / Not now dialog until then) and logs one console line saying what went. The
fingerprints are recorded (`BugLedgerSettings.SentFingerprints`), so the same error is never sent
twice; only a new error, or one that came BACK after a fix (its regression count moved), sends again.

**Send** (`BugLedgerSender`, background thread) pushes this machine's whole live ledger to its own
branch, **`bugledger/<user>`**, containing only `BugLedger/shared/`:

- `issues/<id>.bug.json` — every live issue (a closed one simply drops out);
- `reports/<user>/<redlog|crash|perf>/<file>` — new redlog sessions (`Logs/RedConsole/`), crash
  reports (`Logs/CrashDetector/`) and DiagnosticsHUD JSONs (`Documents/CosmicShore Diagnostics/`),
  ≤ 14 days old, ≤ 512 KB, ≤ 12 per send, append-only (`BugHuntReports`).
- `reports/<user>/net/<file>` — multiplayer test logs (`Logs/NetSessions/`, one per player; see
  "Multiplayer test logs" below).

It never touches the person's work: blobs go straight into the object store (`hash-object -w`), the
tree is built in a THROWAWAY index (`GIT_INDEX_FILE` under `Library/`), the commit is
`commit-tree`d onto the branch's last commit and pushed to `refs/heads/bugledger/<user>`. Their
working tree, staged changes and current branch are never read or written, so there is nothing to
pull first and nothing to conflict; a send whose tree equals the branch's pushes nothing; a push
rejected because Claude pushed in between retries once from a fresh fetch. (The command sequence
was replayed against a throwaway repo on 2026-10-15: first send creates the branch, the second
appends a report and replaces the issue, an unchanged third pushes nothing, the user's HEAD and
staged change untouched.)

**Fixes come back the same way.** `bug_ledger.py mark-fixed <id> --note … [--commit <sha>]` (default
HEAD; push the fix first) rewrites the issue as VALIDATING with `fixCommit` on every
`origin/bugledger/*` branch that carries it — same throwaway-index technique, held by
`--self-test` with a negative control. The editor syncs at load and before every send, and adopts
the fix (`BugLedger.AdoptPublishedFix`) **only once `fixCommit` is in its HEAD** — play runs on code
without the fix must never credit it. Until then a send keeps Claude's claim on the branch instead
of overwriting it. Then the lifecycle above runs unseen: clean play runs close it, a recurrence
reopens it and the next play test asks again.

**Claude's side** is the `/bug-ledger` skill over `Tools/Build/bug_ledger.py` (`collect` reads every
`origin/*` branch, `bugledger/*` included; `show`, `mark-fixed`, `file`). "Is it fixed?" is answered
from the sender's branch: open / fixed in `<sha>` (pull it) / validating k of n / gone (closed) /
reopened. A daily scheduled sweep does the same unattended and mirrors the result into the **Team
Bug Board** artifact (https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno), where anyone can read it
and file a bug without Unity.

**Stack excerpts skip the logging plumbing.** With Unity's native trace format an error's first ~10
frames are the stack walker, `DebugLogHandler`, `Logger` and `BenignLobbyLogFilter`; the excerpt now
drops those and the native address prefix, so its first line is the code that logged. (The first
shared "Rpc methods can only be invoked after starting the NetworkManager!" arrived with nothing
but plumbing, so its sender could not be found.)

**Older path, still read:** issues published before this into a branch's working-tree
`BugLedger/shared/issues/` are still imported at load and still read by `collect`.

### Multiplayer test logs — collected by themselves (Unity and Amoebius)

Yash, 2026-10-15: *"auto collect the logs whenever we test the game, just like bug hunt and
performance"*. Nobody pastes a console after a multiplayer test any more. Every player writes its
own log when it stops, the after-play question sends them all, and Claude lines them up into one
timeline.

**What a log is.** One JSON file per player per test, schema `cosmicshore.nettestlog.v1`:
`net_<utc>_<peer>.json` holding that player's `NetSessionRecord` (role, UGS counters, verdict, the
lifecycle timeline: start, signedIn, party, clientApproved, clientLeft, leaverToAI, refused,
bounce...) plus its console (every line, the first game stack frame of each warning/error; the
oldest lines go first if the file would pass 450 KB, and `dropped` counts them).

**Unity (MPPM).** `NetTestLogs` (editor, `[InitializeOnLoad]`) keeps the console from Play start and,
at Play stop, writes the file **if this was a multiplayer test**: always in an MPPM virtual player;
in the main editor when its record shows ANOTHER player (role client/spectator, a client approved
other than its own client 0, a client left, a leaver handed to the AI, a join refused or bounced) or
a virtual player's editor is running. Solo online play (the presence lobby only) writes nothing.
Virtual players run from `Library/VP/<id>/`, so they write into the MAIN project's
`Logs/NetSessions/` (peer = the MPPM tag, e.g. `p2`, else `vp-<id>`); the main editor's file is
`main`. The main editor waits 4 s for the virtual players' files, then sends them with no
question (one console line names the players). The virtual players never send. The send
is the same `BugLedgerSender` as above: the files go to `reports/<user>/net/` on `bugledger/<user>`.

**Amoebius (START ONLINE, + PLAYER 2).** The launcher passes `COSMIC_SHORE_NET_LOG_DIR`
(`<Amoebius data>/netlogs`) and `COSMIC_SHORE_NET_PEER` (`main`, `p2`) to online games; each game
writes the same file when it closes (`Port/src/CosmicShore.Player/NetTestLog.cs`, format in
`Port/src/Shared/NetTestLogFormat.cs`, `engine: "amoebius"`). Player 2 always writes; `main` by the
same evidence rule. When the last player has closed, a notification asks *"Send this online test's
logs to Claude?"*. **SEND TO CLAUDE** (`NetLogSender`) adds them to the same `bugledger/<user>`
branch and folder with the same throwaway-index technique. It keeps every other file on the branch
(the Unity editor's issues and reports) and never sends a log the branch already has. **CLOSE P2**
closes player 2's window first so it writes its log (it is only killed if it does not close within
5 s). Closing a player some other way also writes its log; a killed process writes nothing.

**Claude's side:** `python3 Tools/Build/net_logs.py collect` lists every test found on every
`origin/*` branch (`bugledger/*` included), grouping files from the same user and engine within
2 minutes of each other into one test, `main` first. `show <n|id>` prints each player's summary
(role, CLEAN/DIRTY, errors, warnings, lobby reads, 429s, events) and ONE merged wall-clock
timeline: lifecycle events plus warnings and errors (`--lines` adds every log line). `--dir PATH`
reads a local folder instead (`Logs/NetSessions`, or Amoebius's `netlogs`); `--self-test` holds the
reader. Read with the `/bug-ledger` skill's "Multiplayer test logs" section.

### The Bug Hunt & Performance badge — the one indicator

The after-play question above is the only dialog. Beside it there is ONE passive indicator (Yash, 2026-10-15: "no
extra UIs in our editor ... at the start of each Unity editor, a small indicator which says the bug hunt and
performance report, with few options to do the most basic necessary steps"). `BugHuntPerfBadge` draws a small card in
the bottom-left corner of every Scene view. It opens expanded once per editor start and folds to a one-line pill
(`–`); `×` hides it until the next start. It never opens a dialog and runs nothing slow.

- **Bugs:** the ledger's open count and how many fixed issues your next play runs are still checking.
- **Perf:** the gates, the regressions and the scenarios still owed a Release capture, read from
  `Docs/perf/status.json` (written by every `perf_hunt.py sweep`), the age of that status, and the first next step.
- **Buttons, each shown only when it applies:** **Send to Claude** (open errors exist; the same `BugLedgerSender`
  push as the after-play question) and **Release** (Code Optimization is Debug, so a capture would not count).
- **No Capture button** (Yash, 2026-10-15: "captures should happen automatic"). `PerfAutoCapture` runs
  `capture <label> 15` by itself in Play, once per scene per session, after the scene's time from the status file
  (S2 8 min, Skim Race 90 s, S5 2 min; any other gameplay scene 90 s under its own name). It skips Debug code
  optimization, a paused editor and the boot scenes. While playing, the badge shows its countdown. A new capture
  then goes to Claude by itself after Play. It costs one short hitch per scene (a 180-frame Profiler recording and a
  renderer census). Turn it off on one machine with the EditorPrefs key `CosmicShore.PerfAutoCapture.Off`.

It replaced the Capture Performance window. Every other question (what regressed, what to test, what to fix next) is
answered in Claude: the `.claude/hooks/health_status.py` hook hands any session that is asked the same status file
(`python3 Tools/Build/perf_hunt.py status`).

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
3. **Send to Claude**: in Play, run `Debug.LogError("bugledger test")` from any script (or hit a real
   error), stop Play → the dialog lists it → Send. Within seconds the console says `Sent to Claude`
   and `origin/bugledger/<you>` has `BugLedger/shared/issues/E-….bug.json`. `git status` is
   unchanged and you are still on your branch.
4. Play and stop again with the same error → **no** dialog (already sent).
5. A Claude session runs `bug_ledger.py mark-fixed <id> --note test`; pull the fix commit; restart
   the editor → the console says `Fix received`, and two clean play runs close the issue (it drops
   off your branch at the next send).

## Files

| Role | Path |
|---|---|
| Crash monitor (watchdog, `[InitializeOnLoad]`) | `Assets/_Scripts/Editor/Diagnostics/CrashDetectorMonitor.cs` |
| Crash settings (`ScriptableSingleton`) | `Assets/_Scripts/Editor/Diagnostics/CrashDetectorSettings.cs` |
| Ledger core (capture, store, validation, tool findings) | `Assets/_Scripts/Editor/Diagnostics/BugLedger.cs` |
| Issue model + hand-rolled JSON | `Assets/_Scripts/Editor/Diagnostics/BugLedgerIssue.cs` |
| Ledger settings (`ScriptableSingleton`) | `Assets/_Scripts/Editor/Diagnostics/BugLedgerSettings.cs` |
| Agent side of the ledger (list / show / mark-fixed / collect / file) | `Tools/Build/bug_ledger.py` + `.claude/skills/bug-ledger/SKILL.md` |
| The automatic after-play send (no question since 2026-10-16) | `Assets/_Scripts/Editor/Diagnostics/BugHuntAutoSend.cs` |
| The one indicator: the Scene view's Bug Hunt & Performance badge (reads `Docs/perf/status.json`) | `Assets/_Scripts/Editor/Diagnostics/BugHuntPerfBadge.cs` |
| Sender to `bugledger/<user>` + fix pickup (throwaway index, off-thread) | `Assets/_Scripts/Editor/Diagnostics/BugLedgerSender.cs` |
| Report files that ride along (redlog / crash / perf) | `Assets/_Scripts/Editor/Diagnostics/BugHuntReports.cs` |
| Team Bug Board (artifact page source) | `Docs/BugHunt/TeamBugBoard.html` → https://claude.ai/artifact/5zNw9wDjMrxvZVjGD7KBno |
| Compile-timing recorder (`[InitializeOnLoad]`, opt-in) | `Assets/_Scripts/Editor/Diagnostics/CompileTimingMonitor.cs` |
| The window (Crash Detector + Compile Timing tabs) | `Assets/_Scripts/Editor/Diagnostics/DiagnosticsWindow.cs` |
| On-screen UI report (reader, play mode only) | `Assets/_Scripts/Editor/Diagnostics/OnScreenUIReport.cs` |
| Live-store gitignore (committed, self-healed by the tool) | `BugLedger/.gitignore` |
| Shared signature core (**runtime-safe**) | `Assets/_Scripts/Utility/BugSignature.cs` |
| Signature determinism tests (edit mode) | `Assets/_Scripts/Tests/Editor/BugSignatureTests.cs` |
| Committed store contract | `BugLedger/README.md` (project root) |
| Crash logs / reports (machine-local) | `Logs/CrashDetector/` (gitignored) |

Every Diagnostics card on the Froglet Master Tool carries a **DOCS chip** linking straight to
this page on GitHub (`FrogletDocLinks`); the window's **Docs** button does the same.
