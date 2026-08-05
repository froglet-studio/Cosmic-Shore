---
name: ship-quick
description: The fast lane of the ship protocol - for a small, low-risk branch that was already reviewed as it was written. Trims the review and documentation passes to their load-bearing minimum, but still runs the tool-output gate in full and still makes a real go/no-go call. Use for "just ship it", "quick PR", "this is tiny, push it up". If the branch is large, touches a LOCKED system (ecology, party, threading, scoring, prism animation), or the session has run long, use /ship or /ship-deep instead.
---

# Ship Quick — the fast lane, with the gate intact

**Read `.claude/skills/ship/SKILL.md` first.** This file only says what changes.

Fast means *less review depth*, not *less verification*. The two things that make a
branch unsafe to merge — a half-landed editor tool and an unresolved base merge — are
exactly the things a rushed ship drops, so they are the things this mode keeps whole.

## Refuse the fast lane when

Any one of these means stop and run `/ship` (or `/ship-deep`) instead. Say which
one fired.

- The diff is more than ~10 files or ~400 changed lines.
- It touches a LOCKED system: ecology, party/presence, threading, scoring, prism
  animation, the elemental ability contract, or anything CLAUDE.md calls an invariant.
- It hand-authored `.unity` / `.prefab` / `.asset` / `.shadergraph` YAML or JSON.
- The session has run long enough that you cannot summarize the whole diff from memory.
- More than one logical change is in flight (split the branch instead).

## What you still do, in order

1. **Merge base, not branch tip.** `git merge-base origin/bleeding-edge HEAD`, then merge
   `bleeding-edge` in and resolve conflicts. Non-negotiable — a stale base is how a clean
   diff hides a `CS0102` (see `/asset-surgery` § "a clean merge can still be a semantic
   conflict").
2. **State what the branch delivers in two sentences.** If you can't, you are not in the
   fast lane; go read the diff.
3. **§2.5 tool-output gate, in full.** Classify every tool the branch adds or changes as
   READER or WRITER, prove a WRITER's output is committed, `AskUserQuestion` if it isn't.
   Zero shortcuts here — this gate is the whole reason the fast lane is safe to have.
4. **`git status --porcelain -- Assets ProjectSettings` must be empty** (or every entry
   explained and committed). One command; there is no version of "quick" that skips it.
5. **Compile-risk scan on what you changed**: run
   `python3 Tools/Build/check_conditional_compilation.py` if any script has an `#if`
   guard, and grep for callers of any public member you renamed or deleted.
6. **Docs: one question only** — did this change a pattern, invariant, or key-files row
   that `CLAUDE.md` or a `Docs/<System>/` file states? If yes, update it now; a doc lie
   costs the next reader more than this branch saved you. If no, say "no doc surface
   touched" and move on.
7. **Go / no-go.** Same bar as `/ship`. Fast is not a licence to ship a NO.
8. **PR** with what & why, verification status (what a human must still check in-editor),
   the **Tool output** line from §2.5, and follow-ups. Then subscribe and watch CI.

## What you skip

- The file-by-file §2 review pass (you are asserting it happened as you wrote it — if it
  didn't, you are in the wrong mode).
- The §3 documentation sweep beyond step 6 above.
- The §3.5 skill-capture retrospective, **unless** something in this session was painful
  to figure out — in which case name it in the report in one line so it isn't lost.

## Report

Two paragraphs: what shipped and the PR link, then the §2.5 verdict (tools classified,
where their output landed) and anything you deliberately skipped so the prompter can
call it back.
