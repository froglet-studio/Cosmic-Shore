---
name: detect-competing-changes
description: Find lines of source code that get flipped back and forth across git history, then use the commit messages on each toggle to surface the two competing goals (e.g. "remove default interface impl" vs. "restore AIPilot workaround") that keep pulling the code in opposite directions. Use when the user asks about unstable code, churn hotspots, merge tug-of-war, reverts, tech-debt archaeology, or phrases like "flipped", "competing", "tug of war", "oscillating", "undo/redo loop".
---

# Detect Competing Changes

Some lines in a codebase get added, removed, and re-added repeatedly. When that
happens, the commit messages on the toggle events usually name two opposing
goals that are in conflict. Surfacing those pairs lets a human (or Claude)
decide which goal should win permanently — or whether the two can be
reconciled by a third approach.

## When to invoke

- The user asks to find "flip-flopping" code, reverts, or unstable areas.
- Investigating a bug whose fix keeps regressing.
- Doing tech-debt triage on a churn hotspot.
- "Why do we keep changing this line?"

Skip this for a fresh repo (<20 commits) — there isn't enough history for the
signal to be meaningful.

## How it works

`detect_flips.py` walks every commit that touched each source file, records
each line's add/remove events, and flags any line whose events form an
A→B→A pattern (added → removed → re-added, or the reverse) across at least
3 distinct commits with ≥ 2 sign transitions. The commit subjects on those
events are the competing-goal signal.

Per-commit dedup prevents YAML/asset repetition from dominating. Common
binary/asset extensions (`.unity`, `.prefab`, `.asset`, `.meta`, `.mat`,
`.png`, etc.) are skipped by default — pass `--include-assets` to override.
A directory argument is expanded to the tracked files beneath it, so the
same filters apply whether you name a file, a directory, or nothing at all.

**Shallow clones are handled explicitly.** A graft-boundary commit has no
parent in the object store, so `git show` reports the *entire file* as added
and every line of it looks like a flip. Those commits are skipped and the
script says how many it ignored. Results from a shallow clone are still
partial — run it in a full clone when the history matters.

## Run it

From the repo root:

```bash
python3 .claude/skills/detect-competing-changes/detect_flips.py
```

Useful flags:

| Flag | Effect |
|---|---|
| `--min-commits=N` | Only scan files touched by ≥ N commits (default 3) |
| `--since=6.months` | Restrict to recent history |
| `--include-assets` | Include Unity scenes, prefabs, etc. (noisy) |
| `--help` | Usage summary |
| `path [path...]` | Limit analysis to specific files or directories |

`--since` applies to *both* which files are scanned and how far back each
file's own history is walked. An explicitly named path is always scanned,
regardless of `--min-commits`.

Each result block shows the file, the flipping line text, and the sequence of
commits that toggled it with `+` (added), `-` (removed), or `*` (both in one
commit). Read the commit subjects vertically — the competing goals usually
jump out.

## Interpreting results

Strong competing-goals signal:
- Two commits whose subjects directly contradict (`fix(X): do Y` vs.
  `revert: undo Y, restore Z`).
- Bug-fix/regression oscillation (`fix A` → `fix B breaks A` → `fix A again`).
- Architectural split (two contributors pulling a module in opposite
  directions across their respective commits).

Weak/noise signal:
- A single large refactor commit that appears `+` on every line — this just
  means that commit happens to be the current source of that line. Look for
  the `-` events to find the real contention.
- `using X;` imports toggling — usually just dead-code cleanup oscillation,
  not architectural competition.
- Asset / generated files — always skip unless investigating scene churn
  specifically.

## Worked example (this repo)

```
$ python3 .claude/skills/detect-competing-changes/detect_flips.py \
      Assets/_Scripts/Controller/Vessel

=== Assets/_Scripts/Controller/Vessel/R_VesselActions/SCARAB.md ===
  [+5 -2 t=4] |---|---|---|
      + c26846f40  docs(scarab): §3.0 channel-ownership + spring/morph records
      + 3ca69c95f  feat(scarab): align the grapple camera to the orbit axis
      * f17772906  feat(scarab): a held drift REVERSES; retire the ball grapple
      + bdfd60462  feat(scarab): the grab gets its own button, and the plate
                   claims its mirror image
      - 645f2cb4c  revert(scarab): cut the ball phase grab; the mirrored plate stays
```

Read the subjects vertically and the tug-of-war names itself: the Scarab's
"held input turns the hull into a hand" mechanic was **built, retired,
rebuilt on its own button, and cut again**. `*` marks a commit that both
added and removed the line — the toggle happening inside one commit.

The competing goals:

1. **A held button should let the hull grab/reverse a ball** — the mechanic
   kept coming back because the *design intent* (a committed skill move that
   converts possession) was never satisfied by the alternatives.
2. **The hull must never bat the ball it is holding** — each implementation
   collided with the ordinary strike path, the approaching-contact gate and
   the depenetration, all of which exist to enforce "the ball never travels
   through a hull".

The resolution was a third approach, not either goal winning: the blast
became a **mirrored swept plate** that claims the space behind the pilot
without the hull ever holding anything. `CLAUDE.md` records all four rounds
and the traps each one bought — which is exactly the archaeology this skill
is for.

## Limitations

- Identical-text matching misses semantic flips where the same line is
  rewritten with different whitespace/naming each time.
- Rebases and force-pushes hide history; run on the canonical branch.
- In a shallow clone, graft-boundary commits are skipped (see above), so
  flips whose only evidence sits at or before the boundary are invisible.
- Noise lines (`continue;`, closing braces) can slip through even with the
  trivia filter. Always read the commit subjects before concluding.
- Script cost is O(files × commits-per-file × avg diff size). For huge
  repos, narrow with `path` args or `--since`.

## Writing up findings

When reporting to the user, for each real flip:

1. Name the file and line.
2. Quote the two competing commit subjects verbatim.
3. State the two goals in one sentence each.
4. Suggest whether a third approach could reconcile them, or which goal
   should win.

Keep the report focused on flips that represent genuine design tension —
three or four real examples beat thirty noisy ones.
