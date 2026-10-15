---
name: bug-closeout
description: Close out a FIXED bug in every record that tracks it, in one pass - the Bug Ledger (FrogletTools > Diagnostics > Bug Ledger, BugLedger/), Amoebius's BOARD (the engine's bug and task cards, amoebius_board / amoebius_board_suggest), the QA records (Docs/QA/DEV_TASKS.md and QA_BACKLOG.md via /qa-backlog), the owning doc's dated log row, and the verification record (Docs/UNITY_VERIFICATION_CHECKLIST.md or the PR body's Verification status). Use whenever a session - any collaborator's, any branch - fixes a bug, closes a dev task, resolves a board card or makes a listed test pass, and on "the bug is fixed, update the lists", "close this out", "mark DT-00N done", "what do I update now that X works". Never marks anything done without naming the check that proved it.
---

# Bug closeout — one fix, every record

A bug in this repo can be tracked in up to FIVE places, each owned by a different tool. A fix that
updates one and not the others is how the same bug gets re-filed, re-tested and re-triaged. This skill
is the route. **Read before writing: every record below says who may write it.**

## 0. The one rule

**A bug is closed by the check that proves it, never by the fix "looking right".** Each record has its
own proof (a QA item passing, a clean session count, a board criterion met, a measured number). Name
that proof in every place you write. If you cannot name it, the bug is *fixed, awaiting proof* — say so,
and leave the record open in that state.

## 1. Find every record that carries the bug (do all five, every time)

| Record | Where | Search |
|---|---|---|
| **Bug Ledger** (editor) | `BugLedger/shared/issues/*.bug.json` (published) and the user's gitignored `BugLedger/local/` | `grep -rl "<words from the error>" BugLedger/shared/` |
| **Amoebius BOARD** (engine) | `%LOCALAPPDATA%/Amoebius/tracks/board.json` on the user's machine; reachable from a session ONLY through the `amoebius_board` / `amoebius_board_suggest` MCP tools | `amoebius_board` when the tools are connected; otherwise ask the user for the card id |
| **QA dev tasks** | `Docs/QA/DEV_TASKS.md` (`DT-00N`, each tied to a `QA-...` item in `Docs/QA/QA_BACKLOG.md`) | `grep -n "<symptom>" Docs/QA/DEV_TASKS.md Docs/QA/QA_BACKLOG.md` |
| **The owning doc** | the system's record: `Docs/PERFORMANCE_OPTIMIZATION.md` §2 (perf), `Docs/SKIM_RACE_AI.md` (that AI), `Docs/ECOSYSTEM*.md`, a `BACKLOG.md` ... (`Docs/claude/DOCUMENTATION_INDEX.md` says which) | `grep -rln "<symptom>" Docs/` |
| **Verification record** | `Docs/UNITY_VERIFICATION_CHECKLIST.md` (🔴 sections) and the PR body's *Verification status* | `grep -n "<feature>" Docs/UNITY_VERIFICATION_CHECKLIST.md` |

## 2. Write each one the way its owner allows

- **Bug Ledger.** An agent never edits `BugLedger/shared/` by hand (it is the Stage & Push tab's output).
  Tell the user: *Mark Fixed* on the issue in **FrogletTools > Diagnostics > Bug Ledger**; it goes to
  `validating` and auto-closes after `cleanSessionsRequired` clean sessions; then **Stage & Push**. A
  `T-...` tool finding closes itself on the next clean run of the tool that filed it. Put the issue id
  in the commit or PR body so the two records point at each other.
- **Amoebius BOARD.** Cards are the user's; agents only *suggest*. With the MCP tools: `amoebius_board`
  to find the card, then `amoebius_board_suggest` with a `criterion` (the check) if a follow-up is owed, and
  tell the user which card to accept or move. A card born from the tracks closes itself when its problem
  is not seen in 3 runs through its scene; say that instead of asking the user to close it. Without the
  tools (a cloud session has no board): list the card titles and the proof in the reply, for the user to
  apply.
- **QA records.** `Docs/QA/DEV_TASKS.md` and `QA_BACKLOG.md` are written by `/qa-backlog` - never hand-edit a
  row. A fix lands there as a **result**: run `/qa-backlog` with the QA item's pass/fail (a
  `Docs/QA/RESULTS/<date>-<who>.md` submission, or "we ran it, QA-XYZ passes on <commit>"); the skill
  archives the pass and closes the `DT-` task. A fix with no QA run yet stays 🟢 "fixed, awaiting retest" -
  `/qa-backlog` sets that from the PR body's Verification status, so write it there.
- **The owning doc.** Add the dated row (what was wrong, the fix, the proof, the commit) to that doc's log
  table, and strike or update the claim that is now false. The perf record's rule (`PERFORMANCE_OPTIMIZATION.md`
  §4): a number without its measurement protocol is not a result.
- **Verification record.** A 🔴 section whose steps ran green becomes 🟢 with the date and the editor build;
  one that ran red stays 🔴 with what failed. A fix that could not be run in Unity in the session stays
  🔴 and says what ran instead (`unity_refcompile`, the Amoebius run, the edit-mode suite).

## 3. Cross-link, then prove the proof

- Commit message: `fix(scope): ... (closes DT-00N, ledger E-..., board #N)`, conventional commits
  (`GIT_RULES.md`). The PR body's *Verification status* names the same ids and the check that ran.
- Re-run the check named in §0 on the final commit and paste its output in the reply. A closeout whose
  proof was run on an earlier commit is a claim.

## 4. What this skill does not do

It does not decide a bug is fixed, does not pick which check proves it (that is the record's own
"done when"), and does not edit the tool-owned files (`BugLedger/shared/`, `Docs/QA/*.md`). It
routes. If a record has no "done when", the first write is to give it one.
