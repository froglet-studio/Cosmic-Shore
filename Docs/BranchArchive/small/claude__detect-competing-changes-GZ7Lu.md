# Branch archive: `claude/detect-competing-changes-GZ7Lu`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-04-20 by Claude
- **Unmerged commits:** 1
- **Forked from:** `07cdbff58` (2026-04-20, Update Menu_Main.unity)
- **Tip:** `8a02b75aa`
- **Files touched (2):**
  - `.claude/skills/detect-competing-changes/SKILL.md`
  - `.claude/skills/detect-competing-changes/detect_flips.py`

### `8a02b75aa` — feat(skills): add detect-competing-changes skill for git flip analysis

_Claude, 2026-04-20 20:14:16 +0000_

```text
Walks every commit touching each file and flags lines added, removed, then
re-added (or vice versa) across >= 3 commits with >= 2 sign transitions.
The commit subjects on toggle events surface the two competing goals that
keep pulling the code in opposite directions — e.g., IVesselStatus.cs
toggling between "remove default interface impls" and "revert: restore
AIPilot workaround" 8 minutes apart, signaling an unresolved root cause.

Per-commit dedup prevents YAML/asset repetition from dominating. Common
binary/generated extensions are skipped by default; pass --include-assets
to override. Explicit paths bypass the churn filter so shallow/grafted
histories still resolve.
```

```text
 .claude/skills/detect-competing-changes/SKILL.md        | 130 ++++++++++++++++++++++++++++++++++
 .claude/skills/detect-competing-changes/detect_flips.py | 176 ++++++++++++++++++++++++++++++++++++++++++++++
 2 files changed, 306 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 318 lines)</summary>

```diff
diff --git a/.claude/skills/detect-competing-changes/SKILL.md b/.claude/skills/detect-competing-changes/SKILL.md
new file mode 100644
index 000000000..752275712
--- /dev/null
+++ b/.claude/skills/detect-competing-changes/SKILL.md
@@ -0,0 +1,130 @@
+---
+name: detect-competing-changes
+description: Find lines of source code that get flipped back and forth across git history, then use the commit messages on each toggle to surface the two competing goals (e.g. "remove default interface impl" vs. "restore AIPilot workaround") that keep pulling the code in opposite directions. Use when the user asks about unstable code, churn hotspots, merge tug-of-war, reverts, tech-debt archaeology, or phrases like "flipped", "competing", "tug of war", "oscillating", "undo/redo loop".
+---
+
+# Detect Competing Changes
+
+Some lines in a codebase get added, removed, and re-added repeatedly. When that
+happens, the commit messages on the toggle events usually name two opposing
+goals that are in conflict. Surfacing those pairs lets a human (or Claude)
+decide which goal should win permanently — or whether the two can be
+reconciled by a third approach.
+
+## When to invoke
+
+- The user asks to find "flip-flopping" code, reverts, or unstable areas.
+- Investigating a bug whose fix keeps regressing.
+- Doing tech-debt triage on a churn hotspot.
+- "Why do we keep changing this line?"
+
+Skip this for a fresh repo (<20 commits) — there isn't enough history for the
+signal to be meaningful.
+
+## How it works
+
+`detect_flips.py` walks every commit that touched each source file, records
+each line's add/remove events, and flags any line whose events form an
+A→B→A pattern (added → removed → re-added, or the reverse) across at least
+3 distinct commits with ≥ 2 sign transitions. The commit subjects on those
+events are the competing-goal signal.
+
+Per-commit dedup prevents YAML/asset repetition from dominating. Common
+binary/asset extensions (`.unity`, `.prefab`, `.asset`, `.meta`, `.mat`,
+`.png`, etc.) are skipped by default — pass `--include-assets` to override.
+
+## Run it
+
+From the repo root:
+
+```bash
+python3 .claude/skills/detect-competing-changes/detect_flips.py
+```
+
+Useful flags:
+
+| Flag | Effect |
+|---|---|
+| `--min-commits=N` | Only scan files touched by ≥ N commits (default 3) |
+| `--since=6.months` | Restrict to recent history |
+| `--include-assets` | Include Unity scenes, prefabs, etc. (noisy) |
+| `path [path...]` | Limit analysis to specific files/dirs |
+
+Each result block shows the file, the flipping line text, and the sequence of
+commits that toggled it with `+` (added), `-` (removed), or `*` (both in one
+commit). Read the commit subjects vertically — the competing goals usually
+jump out.
+
+## Interpreting results
+
+Strong competing-goals signal:
+- Two commits whose subjects directly contradict (`fix(X): do Y` vs.
+  `revert: undo Y, restore Z`).
+- Bug-fix/regression oscillation (`fix A` → `fix B breaks A` → `fix A again`).
+- Architectural split (two contributors pulling a module in opposite
+  directions across their respective commits).
+
+Weak/noise signal:
+- A single large refactor commit that appears `+` on every line — this just
+  means that commit happens to be the current source of that line. Look for
+  the `-` events to find the real contention.
+- `using X;` imports toggling — usually just dead-code cleanup oscillation,
+  not architectural competition.
+- Asset / generated files — always skip unless investigating scene churn
+  specifically.
+
+## Worked example (this repo, April 2026)
+
+Top result: `Assets/_Scripts/Controller/Vessel/IVesselStatus.cs`
+
+```
+[+3 -1 t=2] string PlayerName
+    + 5560bbce  fix(ui): modal reopen with external deactivation detection
+    + 0358c370  fix(player): initialize Domain to Jade to match NetDomain default
+    - a4f7ccbf  fix(vessel): remove default interface implementations for Domain and PlayerName
+    + 0268b21e  revert: undo VesselStatus.Domain fixes, restore AIPilot workaround
+```
+
+The two competing goals are named directly in the commit subjects:
+
+1. **`a4f7ccbf` — "remove default interface implementations"**
+   Goal: clean C# interface, force every implementor to define `Domain` /
+   `PlayerName` so `AIPilot`'s `IVesselStatus` dispatch hits the concrete
+   `VesselStatus` property (not the default member).
+
+2. **`0268b21e` — "restore AIPilot workaround"** (8 minutes later)
+   Goal: keep the default-member workaround because the underlying dispatch
+   bug persisted and removing the defaults broke other callers.
+
+The oscillation is a symptom of an unresolved root cause: `IVesselStatus`
+dispatch in `AIPilot` returns wrong data, and neither fix resolved it. A
+durable resolution needs a third approach — e.g., stop calling through the
+interface in `AIPilot`, or replace the interface with an abstract class.
+
+Second example (shader iteration): `ForcefieldCrackle.hlsl` flips
+`_CrackleColorA.rgb` / `_CrackleColorB.rgb` between a voronoi-based pattern
+and an FBM-based electrical arc effect across three commits — a visual
+iteration tug-of-war rather than a bug fix loop.
+
+## Limitations
+
+- Identical-text matching misses semantic flips where the same line is
+  rewritten with different whitespace/naming each time.
+- Rebases and force-pushes hide history; run on the canonical branch.
+- Noise lines (`continue;`, closing braces) can slip through even with the
+  trivia filter. Always read the commit subjects before concluding.
+- Script cost is O(files × commits-per-file × avg diff size). For huge
+  repos, narrow with `path` args or `--since`.
+
+## Writing up findings
+
+When reporting to the user, for each real flip:
+
+1. Name the file and line.
+2. Quote the two competing commit subjects verbatim.
+3. State the two goals in one sentence each.
+4. Suggest whether a third approach could reconcile them, or which goal
+   should win.
+
+Keep the report focused on flips that represent genuine design tension —
+three or four real examples beat thirty noisy ones.
diff --git a/.claude/skills/detect-competing-changes/detect_flips.py b/.claude/skills/detect-competing-changes/detect_flips.py
new file mode 100755
index 000000000..d5b672a9d
--- /dev/null
+++ b/.claude/skills/detect-competing-changes/detect_flips.py
@@ -0,0 +1,176 @@
+#!/usr/bin/env python3
+"""
+Detect lines that flip back and forth across git history.
+
+Algorithm:
+  For each file, walk every commit that touched it (oldest->newest) and record
+  every line that was added (+) or removed (-). A "flip" is a line whose exact
+  text was added in one commit and later removed (or vice versa) >= 2 times,
```

</details>
