# Andrew — your test list, and how to report it

Written 2026-10-15 for Andrew (the team's tester — Workstream C in
`Docs/STEAM_EA_INVESTOR_CHECKPOINT.html`: C2 first-session validation, C8 the daily all-modes
session, C11 quest and progression end to end). This file is the **one place** that says what the
owner (Yash) wants tested on `Ys-bleeding-edge` and where to send what you find. It does not
replace the QA system, it routes into it: every result goes through the normal QA loop in
`Docs/QA/README.md`, so nothing you report gets lost.

Start whenever you are ready. Nothing here is time-boxed.

## 0. Your whole list, in one command

Everything the owner tests by hand is a ticket on Froglet Tasks (GitHub issues labelled `team-task`; the
board is https://claude.ai/artifact/E7pkoypRXCTXiSM4ZxZe8f): the first-login guide, the performance captures, the Third Eye and
Regatta playtests, the bug-flow check, and two for multiplayer (`T-20261015-4354` Unity, `T-20261015-0426`
Amoebius). Tickets name nobody (since 2026-10-23); take one by naming your branch.

```bash
python3 Tools/Build/team_tasks.py list --dept testers                       # what you can take
python3 Tools/Build/team_tasks.py take <id> --who Andrew --branch <branch>   # so nobody does it twice
python3 Tools/Build/team_tasks.py done <id> --note "what happened"           # when finished
```

A test that FAILS is not "done": write what failed on the ticket, and the fix goes on a ticket of its own
(`/team-tasks add --kind dev ...` from a session). Sections 1–3 below are the multiplayer tasks in full.

---

## 1. Get the build

```bash
git fetch origin Ys-bleeding-edge
git checkout Ys-bleeding-edge
git pull origin Ys-bleeding-edge
git rev-parse --short HEAD        # your session file must record this commit
```

Open in Unity **6000.3.17f1**, let it fully reimport (a stale `Library/` is the usual cause of a
false failure), and turn **Error Pause** and **Clear on Play** off in the Console.

Note the branch: `Docs/QA/README.md` says `bleeding-edge` by default. For this list it is
**`Ys-bleeding-edge`**.

## 2. Test, in this order

### Part A — Unity multiplayer (the QA-NET items)

These are already written up as step-by-step items with exact PASS and FAIL lines in
`Docs/QA/QA_BACKLOG.md`. Work them with **FrogletTools ▸ QA ▸ QA Session** in Unity. Before any
multi-player test, **give each Multiplayer Play Mode virtual player its own tag (P2, P3, P4)**, or
they all share one UGS account and the results are wrong (`Docs/PartySystem/TESTS.md` § MPPM
prerequisites).

| Order | Item ID | What it proves | Needs |
|---|---|---|---|
| 1 | `QA-NET-SDK-UPGRADE` | The game still compiles and reaches the menu on the new multiplayer packages. A gate: if it fails, stop and report | 1 editor |
| 2 | `QA-NET-RATE-LIMIT-RETEST` | Two players get into a party without anyone being thrown out | MPPM, 2 players |
| 3 | `QA-NET-CLIENT-CAN-LEAVE` | A joined player can leave from every screen | MPPM, 2 players |
| 4 | `QA-NET-SCENE-TRANSITION-TIMEOUT` | A lost loading screen no longer traps the player forever | MPPM, 2 players |
| 5 | `QA-NET-READY-GATE-LEAVER` | Somebody leaving does not strand everyone at the ready screen | MPPM, 3 players |
| 6 | `QA-NET-MIDMATCH-LEAVER` | A player who quits mid-match leaves their ship and their score behind | MPPM, 3 players |
| 7 | `QA-NET-JOIN-SPECTATE` | Joining and watching somebody's game without an invite | MPPM, 2 players |
| 8 | `QA-NET-PARTY-4P-JOIN` | Three and four players can all get into one game | MPPM, 4 players |
| 9 | `QA-NET-OFFLINE-MODE` | The game is fully playable with no internet (the seven offline cases) | 1 editor |
| 10 | `QA-NET-ARCADE-LOBBY-FOLLOW` | Guests follow the host to the game card | **Three real machines** — mark `BLOCKED` if you do not have them, that is a valid result |
| 11 | `QA-NET-PRESENCE-PARTY` | Party/presence regression pass and the fault-rate numbers (already 🟡; read its "Last QA" line first) | 3–4 players and one **standalone build**, not the Editor |

Two extra checks from the owner's list that have no QA item yet (record them in the
"Anything else" section of your results file, with the console text):

- **Host drops, then the new host invites at once — repeat 5 times.** Expect the Accept seats the
  guest every time; never "party is no longer available".
- **Request discipline: idle 5 minutes in the menu.** Expect no 429 errors.
  Exact steps for both are in `Docs/MULTIPLAYER_START_HERE.md` §5.2 and
  `Docs/PartySystem/TESTS.md`.

### Part B — Amoebius (our own engine, Windows, no Unity)

The owner's eight-step hand list is in `Docs/MULTIPLAYER_START_HERE.md` §5.1: four players tile,
they party up, a simulated 4G line, pulled cable, session-fault "party is full", capture, TCP
still works, and through the relay. Each step states the result to expect. Needs Windows and
`Prisma.exe`.

These are not in the QA backlog (it tracks Unity work), so report them in the "Anything else"
section of your results file: step number, PASS or FAIL, and for any FAIL the log text, verbatim.

### Do NOT run: the live UGS test

`Docs/MULTIPLAYER_START_HERE.md` §5.3 (UGS RELAY CHECK and the game through UGS Relay) creates real
player accounts in the live UGS project. **It is the owner's to run, or run only when the owner
says go.** Do not run it from this list. Also: never accept a consent or privacy prompt on a
player's behalf — press "No thanks".

### Known failures — do not file these as new

Grizzly, lizard, Sparrow and RaceRankToast failures are already known and are not multiplayer
bugs (`Docs/MULTIPLAYER_START_HERE.md` §2; some have dev tasks in `Docs/QA/DEV_TASKS.md`). Each QA
item lists its own "Known, do not fail on" lines too — honour them.

## 3. Report it

Use the QA loop; it is built so a result can be picked up cold.

1. In Unity: **FrogletTools ▸ QA ▸ QA Session** (or tell Claude Code *"Set up a QA session for me,
   branch Ys-bleeding-edge, items QA-NET-…"* and it creates `Docs/QA/RESULTS/<date>-<you>.md`
   pre-filled with the commit).
2. One verdict per item: `PASS`, `FAIL`, `PARTIAL`, `BLOCKED` or `SKIP`. Anything that is not
   `PASS` needs a note naming the **step number** and **what you saw**, with console text pasted
   verbatim (including `file:line`). Attach a screenshot for every `FAIL` under
   `Docs/QA/RESULTS/evidence/<session-file-name>/`.
3. Press **Submit** (the window's button, or `python3 Tools/QA/submit.py`). It refuses until the
   file is complete, then publishes, commits and pushes.
4. **Do not fix anything you find.** Write it down and move on.
5. Tell Claude: *"My QA results are in `Docs/QA/RESULTS/<date>-<you>.md` — apply them."* Passes
   leave the backlog for good, failures become `DT-nnn` dev tasks in `Docs/QA/DEV_TASKS.md`.

**Red errors in the Console are reported separately, and automatically.** After any play test that
logs a new red error, the editor sends it to Claude by itself — nothing to press. It pushes the error to your own
branch `bugledger/<you>` (never the branch you are working on) and Claude fixes it from there
(`/bug-ledger`). Pull afterwards to get the fix; two clean play tests close it. Details:
`Docs/DIAGNOSTICS.md` § "Send to Claude". Do not copy-paste console text for these; for QA items
paste it in the Notes cell as the steps above say.

**Feedback that is not a pass/fail** — feel, confusing UI, anything a first-timer would trip on
— goes in the "Anything else" section of the same file. Items that end in "Report the feel" or
"Judgement call to report" are not optional; someone is waiting on a one-sentence opinion.

A retest after a fix is always a **new** session file, never an edit.

## 4. Where everything else is

| Need | File |
|---|---|
| The team board (your tasks, take/done) | `Docs/TEAM_TASKS.md` |
| Console errors → Claude | `Docs/DIAGNOSTICS.md` § "Send to Claude" |
| The whole QA workflow, in full | `Docs/QA/README.md` |
| The test items (steps, PASS/FAIL, known exceptions) | `Docs/QA/QA_BACKLOG.md` |
| What the owner tests by hand, and what comes next | `Docs/MULTIPLAYER_START_HERE.md` §5 |
| MPPM procedures and the smoke gate (S1–S4, S9) | `Docs/PartySystem/TESTS.md` |
| Every known party bug and its status | `Docs/PartySystem/BUGS.md` |
| Failures already turned into dev tasks | `Docs/QA/DEV_TASKS.md` |
| Items that already passed (do not retest) | `Docs/QA/ARCHIVE.md` |
| Blank results form, if Claude and the window are both unavailable | `Docs/QA/RESULTS/TEMPLATE.md` |
