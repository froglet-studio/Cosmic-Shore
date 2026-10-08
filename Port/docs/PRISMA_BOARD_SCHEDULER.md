# Prisma BOARD: safe saves and the task-scheduler foundation

*PR [#1044](https://github.com/froglet-studio/Cosmic-Shore/pull/1044), branch `prisma-task-scheduler-p1` → `Ys-bleeding-edge`.
Step P1.0 + P1.1 of the task-scheduler plan. Written 2026-10-09.*

---

## 1. What this is

**Prisma** has two parts:

| | What it is | Where |
|---|---|---|
| **Prisma.exe** (the app) | The window with PLAY, BUILD, AGENT, GIT, EDITOR, TIME, TRACKS, **BOARD**, SETTINGS, CONSOLE on the left | `Port/src/CosmicShore.Launcher` |
| The engine | Runs Cosmic Shore without Unity | the other projects in `Port/src` |

This change only touches **Prisma.exe's BOARD** (bugs and tasks) and the code the BOARD and the agents share.
**It does not touch Unity.** Nothing under `Assets/`, `Packages/` or `ProjectSettings/` changed
(`python3 Port/tools/check_unity_isolation.py` says `ok: 0 change(s) outside Port/`).

**You do not need Unity to test it.** You test it from **Command Prompt** (section 6). If you prefer, Unity's menu
**FrogletTools > Prisma > Launch Prisma** also works: it builds Prisma.exe from your checkout and opens it. But Unity
is only acting as a shortcut there, and nothing in Unity itself changed.

There are **no visible UI changes** in this step. The BOARD looks and works the same. What changed is
underneath: the board can no longer lose cards, and the logic the upcoming scheduler screens need is now
in place (with tests).

---

## 2. What it does

### 2.1 Merge-safe board saves
The board is one file, `board.json`. On Windows it lives at `%LOCALAPPDATA%\Prisma\tracks\board.json`. Several
programs write to it:
- Prisma.exe, when you add, accept, dismiss or move cards, and after every play run (tracks suggestions).
- `prisma-mcp`, every time an agent (Claude) calls `prisma_board_suggest`.

Each save now **re-reads the file and merges** before it writes, instead of overwriting it with its own copy.
Prisma.exe also **re-reads the file once a second** when it changes. An agent's suggestion therefore shows up
on the BOARD within about a second and is never lost.

### 2.2 Corrupt-file protection
If `board.json` cannot be read (half-written, hand-edited wrongly, written by a much newer build), Prisma:
1. copies it to `board.json.corrupt-YYYYMMDD-HHMMSS` in the same folder (only once per distinct content),
2. writes a warning in **CONSOLE**: *"board.json could not be read (...); a copy was kept at ... The board starts
   empty and the copy is never overwritten."*,
3. starts with an empty board.

The copy is never overwritten, so the cards can always be recovered from it.

### 2.3 Scheduler logic layer (no screen yet)
New UI-free code in `Port/src/Shared/Workspace/` that the next PRs will put on screen:
- New optional card fields: planned day and time, deadline (due day and time), tags, assignee, estimate, checklist,
  repeat rule, completed-at.
- A **quick-add** parser for one-line tasks.
- **Today / Upcoming / Overdue** lists.
- **Repeat rules**: finishing a repeating card creates the next one.

> The BOARD's existing "Add a bug or task" box does **not** read this syntax yet. It still adds the text
> as a plain title. The quick-add bar arrives in the next PR (P1.2). Today the syntax is only used by the code
> and its tests.

#### Quick-add cheat sheet
Example: `Fix trail tomorrow 3pm #vfx !1 ~45m @yash` gives the title "Fix trail", planned tomorrow at 15:00, tag
`vfx`, priority 1, 45 minutes, assignee `yash`.

| Type this | Meaning |
|---|---|
| `#vfx` | tag (stored lower-case) |
| `@yash` | assignee |
| `!1` `!2` `!3` (or `!high` `!normal` `!low`, `!p1`...) | priority (1 = high) |
| `~45m` `~45` `~2h` `~1h30m` `~1.5h` `~10min` | estimate |
| `today` `tomorrow` `tmrw` | planned day |
| `mon` ... `fri`, `monday` ... `sunday` | the next such day, **today included** (`fri` on a Friday = today) |
| `next fri` | the next Friday strictly after today |
| `next week` | next Monday |
| `in 3 days`, `in 2 weeks` | relative day |
| `2026-10-20`, `oct 20`, `20 oct`, `20th october` | a date (a month-day already past means next year) |
| `on sun`, `on saturday` | `on` + day. `sat` and `sun` **only** count as days after `on`/`due`/`next`/`every`, so "Polish sun shader" stays a title |
| `3pm` `3:30pm` `4 pm` `17:45` `noon`, optionally `at 3pm` | time. It attaches to the day before it. With no day: today, or tomorrow if that time has passed |
| `due fri 5pm`, `due 2026-10-20` | **deadline** (separate from the planned day) |
| `every day` `every weekday` `every week` `every month` | repeat |
| `every mon,thu` / `every mon and thu` / `every 2 days` / `every 3 weeks` | repeat. With no day given, it starts at the first matching day from today |
| `bug: ...` / `task: ...` at the start | bug or task (default task) |
| `... done when: <check>` at the end | the card's acceptance criterion |

Words like "weekly", "may" (with no day number) and "next" (with no day after it) are left in the title.

#### Today / Upcoming / Overdue
Only **open** cards count (TO DO and DOING). Suggestions, DONE and dismissed cards never appear.

| List | Contains | Order |
|---|---|---|
| **Today** | cards planned or due today, plus anything planned or due earlier (carried over) | overdue first, then by time of day (untimed after timed), then priority |
| **Upcoming** | the next N days (default 7), one group per day; a card sits on its planned day, else its deadline | by time, then priority |
| **Overdue** | deadline on an earlier day, or today at a time already past | by deadline |
| Unscheduled | no day at all (the backlog) | by priority |

#### Repeat rules
Stored on the card as text: `daily`, `weekdays` (Mon-Fri), `weekly` (the card's own weekday), `weekly:mon,thu`,
`monthly` (the card's day of the month; 31st becomes the 28th/30th in short months), `days:N`, `weeks:N`.

Finishing a repeating card:
- marks it DONE;
- creates a **new TO DO card** with the same title, tags, assignee, estimate, criterion and rule, and the checklist unticked;
- plans the new card for the rule's next date after **today** (or after the card's own date, if that is later). An
  overdue daily task done today therefore comes back tomorrow, not yesterday;
- moves a deadline forward by the same gap.

---

## 3. Why the old board was risky (diagnostics and findings)

I found both problems by **reading the code**. I did not see them happen on a real machine, but the new tests
recreate both situations and show the old behaviour is gone. Line numbers refer to `origin/Ys-bleeding-edge` at
`a0c575ad5`, before this PR.

### Bug 1: the app overwrote agent suggestions, and two writers could give out the same key
- `Port/src/CosmicShore.Launcher/LauncherApp.Prisma.cs:26`: `PrismaBoard _board = ClearMilestoneCards(PrismaBoard.Load(TracksDir));`.
  Prisma.exe read `board.json` **once, at startup**, and never read it again.
- It then saved that in-memory copy over the file: `LauncherApp.Prisma.cs:59` (after a play run), `:82` (`SaveBoard()`,
  used by ADD / ACCEPT / DISMISS), `:99` (moving a card).
- `Port/src/Shared/PrismaBoard.cs:67-74`: `Save` wrote the whole board without looking at the file first. Every writer
  also used the same temporary file name, `board.json.tmp`.
- `Port/src/CosmicShore.Mcp/Tools.cs:237-245`: `prisma_board_suggest` does its own Load → Add → Save.
- `PrismaBoard.cs:81`: keys came from per-copy counters (`T-{NextTask++}`).

**How it happens:** Prisma is open, and you ask the agent to "suggest that as a board item". The agent's
`prisma-mcp` adds T-5 and saves. Then you click ACCEPT/ADD/START on any card, or a play run ends. Prisma saves
its old copy, which does not contain T-5, and **the suggestion disappears**. If you had added a card yourself in the
meantime, it could also be called T-5.
**Impact:** agent findings vanished silently, and duplicate keys confused card opening and AGENT hand-off.

### Bug 2: a file that could not be read came back empty and was then saved over
- `PrismaBoard.cs:59-65`: `Load` wrapped everything in `catch (Exception) { }` and returned an **empty** board.
- The next save (bug 1's code path) wrote that empty board over the file.

**How it happens:** `board.json` was half-written (power loss, crash), edited by hand with a mistake, or written by a
newer build with a value this build does not know. Old string enums throw on unknown values, so a future
`"State": "Blocked"` would have been enough. **Impact:** every card was gone, with no message and no backup.

### Smaller risks found on the way
- Fields a build does not know were dropped on save, so an older Prisma would strip newer data.
- Writers shared the temp file name `board.json.tmp`, so two simultaneous saves could trip over each other.

---

## 4. How it's fixed

| Piece | What it does | Where |
|---|---|---|
| **Stable card identity** | Every card has a `Uid`. Old cards get one derived from their key and creation time (`legacy-T-3-<ticks>`), so every program reading the same old file agrees. | `PrismaBoard.cs` (`Item.Uid`), `Workspace/BoardMerge.cs` (`Backfill`) |
| **Merge by Uid** | `Save` takes the lock, re-reads the file, and merges card by card against what this copy last saw. Only the other side changed a card: take theirs (copied into the app's own object, so open cards stay live). Only we changed it: keep ours. Both changed it: the later `Updated` wins. New cards on either side are kept. A card one side deleted stays deleted unless the other side edited it since. | `PrismaBoard.Save`, `Workspace/BoardMerge.cs` (`Merge`) |
| **Key collision renumbering** | If two writers both made T-5, the card already in the file keeps T-5. Ours becomes the next free number, with a note "renumbered from T-5". The counters always move past every existing key. | `BoardMerge.Merge`, `PrismaBoard.NewId` |
| **Refresh every second** | Prisma.exe checks the file's time and size once a second and merges it in if it changed. No write happens. | `LauncherApp.Prisma.cs` (`PollBoard`), `LauncherApp.cs` (`OnRender`), `PrismaBoard.Refresh` |
| **Corrupt copy** | An unreadable file is copied to `board.json.corrupt-<time>` before anything can replace it, and CONSOLE says so. This happens at load, and again at save if the file broke in between. | `PrismaBoard.Load` / `Save`, `Workspace/BoardStore.cs` (`Preserve`), `LauncherApp.Prisma.cs` (`ReportBoardLoad`) |
| **Unknown-field round-trip** | Unknown fields are kept (`[JsonExtensionData]` on the board and on each card). An unknown kind or state is kept as text, shown as Task / TO DO, and written back unchanged until you move the card. | `PrismaBoard.cs`, `Workspace/BoardJson.cs` |
| **Atomic write** | Writes go to a unique temp file in the same folder, are flushed to disk, then moved over `board.json`. A reader sees the old board or the new one, never half. | `Workspace/BoardStore.cs` (`FileBoardStore.Write`) |
| **Lock file** | Writers queue on `board.json.lock` while they read, merge and write. After 3 s of waiting a writer goes ahead anyway (the merge still protects the data, and the UI never hangs). | `FileBoardStore.Lock` |
| **Old builds can still read new files** | Empty new fields are not written, and enum values stay the same strings. | `BoardJson.Options` |

---

## 5. Before you test: get the branch

1. Open **GitHub Desktop** on your Cosmic Shore clone, switch to **`Ys-bleeding-edge`** and press **Fetch / Pull**.
   PR #1044 is merged there.
2. Note where the clone is on disk. Below it is written as `C:\path\to\Cosmic-Shore`; use your real path.

---

## 6. How to test (Windows, Command Prompt, step by step)

### 6.1 Check .NET
Open **Command Prompt** (Start → type `cmd` → Enter) and run:
```
dotnet --version
```
You need **10.x** (for example `10.0.401`). If it says `'dotnet' is not recognized`, or shows a lower number,
install the **.NET 10 SDK** (x64) from https://dotnet.microsoft.com/download/dotnet/10.0. Then **open a new**
Command Prompt and check again. Prisma also installs its own SDK the first time you press START on PLAY, but that
copy is not on your PATH.

### 6.2 Back up your board first
Your real board is used by these tests. Copy it somewhere safe:
```
mkdir "%USERPROFILE%\Desktop\board-backup"
copy "%LOCALAPPDATA%\Prisma\tracks\board.json" "%USERPROFILE%\Desktop\board-backup\"
```
("The system cannot find the file" just means you have no board yet. That is fine.)
To restore it later, **close Prisma** first, then:
```
copy /Y "%USERPROFILE%\Desktop\board-backup\board.json" "%LOCALAPPDATA%\Prisma\tracks\board.json"
```

### 6.3 Open Prisma from source
```
cd /d C:\path\to\Cosmic-Shore
dotnet run --project Port\src\CosmicShore.Launcher
```
The first build takes a minute or two (it downloads packages). Then the Prisma window opens. The very first start
of Prisma on a PC shows a one-minute tour; click through it. Leave this Command Prompt open: closing it closes
Prisma.

**What to expect:** click **BOARD** on the left. You see the same board as before (TO DO / DOING / DONE, "Suggested by
Prisma" if there are suggestions) with all your existing cards. Nothing looks different. That is the point of this
step.

**Quick sanity check:**
1. Choose **TASK**, type `Test card A` in "Add a bug or task, then Enter", type `it appears` in "Done when...",
   then press **ADD**. The card appears in TO DO.
2. Click the card, then **START >**. It moves to DOING.
3. Close Prisma and run the `dotnet run` line again. The card is still in DOING.

### 6.4 Manual check 1: an agent suggestion survives while Prisma is open
This is bug 1's scenario.
1. Keep Prisma open on **BOARD** (from 6.3).
2. Open a **second** Command Prompt and run:
   ```
   cd /d C:\path\to\Cosmic-Shore
   dotnet run --project Port\src\CosmicShore.Mcp -- --call prisma_board_suggest "{\"type\":\"task\",\"title\":\"Hello from the agent\",\"criterion\":\"I can see it on the BOARD\"}"
   ```
   It prints `Suggested T-n: Hello from the agent. The user accepts or dismisses it on Prisma's BOARD.`
   This is exactly what Claude's `prisma_board_suggest` tool does.
3. Within about a second, **"Hello from the agent"** appears under **Suggested by Prisma** without restarting Prisma.
4. Now make Prisma save: add another task (e.g. `Test card B`) or move a card.
5. **Expected:** "Hello from the agent" is **still there**, and all keys are different. Before this PR, step 4 would have
   erased it. Click **ACCEPT** or **DISMISS** to tidy up.

(Instead of step 2 you can ask the Prisma Agent in AGENT: "suggest a board task titled Hello from the agent, done
when I can see it". It uses the same tool.)

### 6.5 Manual check 2: a broken board.json is kept, not wiped
This is bug 2's scenario. Do 6.2 (backup) first.
1. **Close Prisma.**
2. Open the board in Notepad:
   ```
   notepad "%LOCALAPPDATA%\Prisma\tracks\board.json"
   ```
   Delete the **last** `}` in the file, save, and close Notepad.
3. Start Prisma again (`dotnet run --project Port\src\CosmicShore.Launcher`).
4. **Expected:**
   - **BOARD** is empty.
   - **CONSOLE** has a line starting `board.json could not be read (` that ends with
     `a copy was kept at ...board.json.corrupt-YYYYMMDD-HHMMSS. The board starts empty and the copy is never overwritten.`
   - The copy is there: `dir "%LOCALAPPDATA%\Prisma\tracks"` lists `board.json.corrupt-...`.
5. Add a card (so Prisma saves), then run `dir` again. The `.corrupt-` file is still there, unchanged.
6. **Restore:** close Prisma and run the restore line from 6.2. Alternatively, open the `.corrupt-` copy in Notepad,
   put the `}` back, and save it over `board.json`. Before this PR, step 3 would have shown an empty board too, but
   step 5 would have destroyed your cards for good.

You may also see a zero-byte `board.json.lock` in that folder. It is expected; leave it.

### 6.6 Automated tests
```
cd /d C:\path\to\Cosmic-Shore
dotnet test Port\tests\CosmicShore.Tests --filter Prisma
```
The first run builds the engine test project, which takes a few minutes. **Expected last line:**
```
Passed!  - Failed:     0, Passed:    85, Skipped:     0, Total:    85, ...
```
(`--filter Prisma` runs every test whose name contains "Prisma": the 4 existing board/tracks tests plus the 81 new ones.)

Optional: `dotnet test Port\tests\CosmicShore.Launcher.Tests` (Prisma.exe's own tests). Expected: `Passed: 21`
on the merged branch.

---

## 7. What the automated tests cover

| Group | File | What it proves |
|---|---|---|
| Stale-copy overwrite (bug 1) | `Port/tests/CosmicShore.Tests/PrismaBoardStoreTests.cs` | An agent suggestion survives the app saving its older copy. The clashing key is renumbered and the stored card keeps T-2. `Refresh` pulls changes in without writing. |
| Merging | same | Edits to different cards both survive, and the app's card objects update in place. On the same card, the later edit wins. A deletion stays deleted unless the card was edited. 4 parallel writers × 15 saves lose nothing, never share a key and leave no temp files. |
| Unreadable file (bug 2) | same | It is copied aside once, never wiped, and the copy is untouched by later saves. A file that breaks between load and save is copied first. A missing file is just an empty board. |
| Format and migration | same | Unknown fields and states (`"State": "Blocked"`, `"Type": "Note"`, extra fields) round-trip. An old-format `board.json` (string and numeric enums, no Uids) loads with stable Uids and keeps every field. What this build writes still reads in the old model. The in-memory store behaves like the file. |
| Quick add | `Port/tests/CosmicShore.Tests/PrismaSchedulerTests.cs` | The example line, plus 40+ phrases: days, times, deadlines, estimates, priorities, tags, assignee, bug/criterion, repeats, and ordinary words left in the title. |
| Queries | same | Today ordering (overdue, then time, then priority), Overdue, Upcoming grouping, Unscheduled, Tagged, planned minutes. |
| Repeat rules | same | The next date for every rule, the month-end clamp, rejecting bad rules, and completing a repeating card (next date after today, deadline shift, checklist reset, new key and Uid). A plain card does not repeat. |
| Existing board/tracks behaviour | `Port/tests/CosmicShore.Tests/PrismaTracksBoardTests.cs` | Unchanged and still passing. |

Results on the build box (Linux): **85/85** for `--filter Prisma`, **21/21** launcher tests. The full engine suite has
13 failures that need model and shader files the box's checkout does not have. They fail the same way without
this change.

---

## 8. Known limitations / not verified

- **Not run on Windows yet.** The lock (`FileShare.None`: a real OS lock on Windows, `flock` on Linux/macOS) and the
  retries when Windows briefly holds the file open were only exercised on Linux. Manual checks 6.4 and 6.5 are
  the first Windows test.
- **Old builds still overwrite.** A Prisma.exe or `prisma-mcp` built **before** this PR saves the old way. Update
  every copy you run: pull, then rebuild or use Prisma's UPDATE. Close old Prisma windows. Only then are all writers safe.
- **`PRISMA_DATA_DIR` mismatch.** Prisma.exe honours this variable (it moves all its data elsewhere), but
  `prisma-mcp` does not (`PrismaTracks.DefaultDir()`). If you set it, agents write to the default folder while Prisma
  reads the other one. Not changed in this PR. Normally the variable is unset and both use `%LOCALAPPDATA%\Prisma\tracks`.
- **No UI for the new fields yet.** There is no quick-add bar, no TODAY/UPCOMING view, no dates on cards and no reminders.
  Coming in P1.2-P1.5.
- **Lock file.** `board.json.lock` (0 bytes) stays beside `board.json`. That is by design.
- **Bugs were found by reading the code**, not reproduced on a user machine. The tests recreate the situations.

---

## 9. Architecture

```
Prisma.exe (CosmicShore.Launcher)          prisma-mcp (CosmicShore.Mcp)           tests (CosmicShore.Tests)
   BOARD page, PollBoard, ReportBoardLoad      prisma_board / prisma_board_suggest     PrismaBoardStoreTests, PrismaSchedulerTests
            \                                          |                                        /
             +----------------- Port/src/Shared (linked into all three, no packages) --------+
                PrismaBoard.cs            the board and its card: Load / Save / Refresh / Add / Move, Suggest / Verify (tracks)
                Workspace/
                  PrismaBoard.Schedule.cs card fields: Scheduled, Due, Tags, Assignee, Estimate, Checklist, Recurrence, CompletedAt
                  BoardStore.cs           IBoardStore (Read, Write, Preserve, Stamp, Lock); FileBoardStore (board.json); MemoryBoardStore
                  BoardJson.cs            the JSON format: tolerant reading, empty fields left out, enum names kept as text
                  BoardMerge.cs           three-way merge by Uid, legacy Uid backfill, key renumbering
                  BoardQueries.cs         Today, Upcoming, Overdue, Unscheduled, Tagged, PlannedMinutes (read side)
                  BoardCommands.cs        add from quick add, schedule, set due, snooze, complete with repeat (write side)
                  QuickAdd.cs             the one-line parser (+ DateWords)
                  Recurrence.cs           repeat rules and next-date maths
```

**Why `Shared/Workspace` and not a new project:** Prisma's UPDATE button and Unity's Launch Prisma only build
`Port/src/CosmicShore.Launcher`, `Port/src/Shared` and `Port/Directory.Build.props`
(`LauncherUpdater.cs` `SourcePaths`, `Assets/_Scripts/Editor/LaunchPrisma.cs`). A new project would break updates.
The files are linked with `<Compile Include="../Shared/Workspace/*.cs" .../>` in the launcher, MCP and test `.csproj` files.

**How the next pieces plug in:**
- **Scheduler screens (P1.2-P1.5):** new views under `Port/src/CosmicShore.Launcher/` call `BoardQueries` to show
  lists and `BoardCommands` to change cards, then `SaveBoard()`. They never touch JSON or files. Reminders reuse
  Prisma's `Notify()` toasts.
- **Shared workspace / sync (Phase 2):** a new `IBoardStore` (e.g. a private Git repo or a server) replaces
  `FileBoardStore`. The merge by Uid already handles several writers, which is the hard part of sync.
- **Prism view (Phase 3):** another view over the same `BoardQueries`, drawn with ImGui like the rest of Prisma.
- **Agents:** the MCP tools already go through `PrismaBoard.Load/Save`, so they got the safe saves for free.
  New tools (filters by day/tag, notes) will call the same queries and commands.

---

## 10. Next steps (roadmap)

Short version of the plan (Phase 1 = local scheduler in Prisma, then sharing, then the game-like view, then messaging):

| Step | What | Done when |
|---|---|---|
| **P1.0 + P1.1** (this PR) | Safe saves, corrupt-file protection, scheduler logic and tests | 85 Prisma tests pass; manual checks 6.4 / 6.5 pass on Windows |
| P1.2 | Quick-add bar on BOARD using the cheat sheet above | typing `Fix trail tomorrow #vfx !1` makes a P1 card tagged vfx for tomorrow |
| P1.3 | TODAY (default), UPCOMING (7-day strip) and the current BOARD as switchable views | a screenshot of each view |
| P1.4 | Card editor (dates, tags, checklist, estimate) and drag and drop (columns, days) | dragging a card onto Thursday plans it for Thursday |
| P1.5 | Reminders (toasts with DONE / SNOOZE / TOMORROW) and repeating cards on screen | a card planned 1 min ahead raises a toast |
| P1.6 | Fun pass: daily goal ring, streaks, completion animation, "Plan my day" (agent suggests) | Yash uses it for a week instead of other apps |
| P1.7 | Agent tools: filters (today, tag, assignee), dates on suggestions, notes on cards | `prisma_board` filters return the right cards |
| Phase 2 | Shared workspace: GitHub sign-in, sync through a new `IBoardStore`, assign and notify, activity feed | two PCs converge within one sync interval |
| Phase 3 | Gamified "task prism" view | every board action works in the prism view |
| Phase 4 | Card threads, then chat (needs a server) | two users exchange messages |

Decisions still open (from the plan): where shared data lives (private Git repo vs own server vs hosted service), who
the colleagues are and what they run, one board or separate workspaces, and how much agents may change without ACCEPT.
