---
name: ui-redesign-tracker
description: Verify and record completion of a Cosmic Shore UI redesign task (T1-T6) before merging its PR into bleeding-edge. Use when a UI redesign task branch is ready to merge, when asked to update the redesign tracker or checklist, or when asked whether a redesign task is actually done. Verifies acceptance criteria against the working tree rather than trusting a completion claim, updates Docs/UI_REDESIGN_TASKS.md, and routes design questions to the feedback queue.
---

# UI Redesign Tracker

Maintains `Docs/UI_REDESIGN_TASKS.md` for the Cosmic Shore UI overhaul. Runs **before** a task PR merges into `bleeding-edge`.

The point of this skill is not bookkeeping. It is to catch the gap between what a task specified and what actually landed, while that gap is still cheap to close.

## Core rule

**Never tick a criterion you have not verified against the working tree.** A commit message, a PR description, or a previous assistant turn claiming a task is done is not evidence. Open the file, read the value, run the check. If a criterion cannot be verified without the Unity editor or a play session, mark it `[~]` and say so — do not mark it `[x]` and do not mark it `[ ]`.

A partially complete task stays `IN PROGRESS`. Do not round up.

## Process

### 1. Identify the task

Determine which of T1–T6 this branch addresses, from the branch name, the commit range against `bleeding-edge`, and the changed files. If more than one task is touched, handle each separately. If none maps to a tracked task, stop and say so — this skill does not track ad-hoc work.

### 2. Verify each acceptance criterion

Read the task's criteria from the tracker and check each one against the actual repo state. Notes on the ones that need care:

- **Values in prefab/scene YAML** (reference resolutions, PPU, aspect settings) — grep the serialized files for the real numbers. Do not infer from a script that sets them.
- **Override counts (T3)** — count the override entries on the canvas instance in each scene file. This is the criterion most likely to be claimed and not met.
- **"Not yet applied" / "not executed" criteria** — these are satisfied by *absence*. Confirm the change genuinely did not happen; over-delivery is a deviation, not a bonus.
- **Editor-only checks** (atlas generation, screenshots, smoke passes) — mark `[~]` and list what the human needs to confirm.

### 3. Update the tracker

**Touch only this task's own section and its own row in the status table.** Parallel branches
edit this file at the same time, so any change outside those two places — reformatting, reordering,
reflowing, re-wrapping, tidying another task's checkboxes, renumbering the queue — turns a one-line
conflict into a manual merge. Leave whitespace, column widths and line breaks exactly as found, even
where they are ugly. If another task's content looks wrong, say so in the report; do not fix it here.

The one exception is step 4: unblocking a dependent means writing that task's **Status cell and
nothing else** — not its criteria, not its notes, not its row's spacing.

- Set the status. `DONE` only when every criterion is `[x]`, or every remaining one is `[~]` and the human has confirmed them.
- Fill in branch, PR number, and completion date.
- **Deliverables** — what actually landed, with paths.
- **Findings** — anything discovered that the audit did not already record. Facts about the codebase, not opinions.
- **Deviations from spec** — every place the implementation differs from the written task, with the reason. An undocumented deviation is the failure mode this whole skill exists to prevent.

### 4. Unblock dependents

If the completed task was a dependency, move dependent tasks from `BLOCKED` to `TODO` and note it. T2 unblocks T3. T5 unblocks T6.

### 5. Route design questions

If implementation surfaced anything needing a design decision — a colour with no token, a layout the foundation does not cover, a state not specified — add a row to the **Design feedback queue** with the task, the question, and status `OPEN`.

**Do not edit `Docs/STYLE_FOUNDATION.md`.** That document is owned by the design side. Proposals go in the queue; the version log records changes only after they are approved upstream.

### 6. Report

Summarise: task, status, criteria met vs total, deviations, new queue entries, what the human still needs to verify in the editor. If the task is not actually complete, say what is missing and stop — do not merge.

## Task-specific traps

**T2** — `CanvasUpgraderUpgradedPrefabs.txt` must be checked before trusting any migration. A double pass compounds ×2.4 to ×5.76 and looks like a layout bug, not a migration bug.

**T3** — The go-ahead gate is a hard criterion. If more than one scene was re-placed without an explicit approval between them, that is a deviation and must be recorded even if the result looks correct. Also verify `statsToTrack` survived per mode; it is the single genuine per-mode value among the ~20 that differ.

**T4** — `UIThemeSO` must have exactly the 25 fields in §10. Added fields are a deviation. **Team colour fields are a spec violation**, not a helpful addition — the omission is what enforces the team-colour contract. Also confirm no call sites changed; the mapping report is the deliverable, not a refactor.

**T5** — Fonts must not be in `Assets/Unity Assests/TextMesh Pro/`. Confirm `OFL.txt` per family and the credits entry; the licence obligation is real and easy to skip.

**T6** — The Aldrich migration is an estimate. If it was executed, that is a significant deviation — roughly 1,670 references moved without design sign-off.

**T7 — a layout group with `childControl` OFF reads a child's CURRENT rect, so a self-sizing child lags one pass**
(claude/new-session-76hnyc, 2026-10-26). A `KitButton` sizes itself with its own ContentSizeFitter. A wrapper whose
layout group does not control the child's width reads the button's rect, which the fitter sets later in the same pass.
So a JOIN that turns into SPECTATE keeps JOIN's slot and spills out of the drawer. Make the wrapper control the child's
width (`author_menu_arcade.wrap_slot`). The same fitter is why a KitButton dropped into a fixed slot (the configure
strip's `<` `>`) grows past the slot onto its neighbours: use a fixed-size plain button there.

**T8 — the offline rect renderer models layout groups, but not ContentSizeFitters and not Button transitions.** A clean
"0 overlaps" from it says nothing about a self-sizing button's real width, and nothing about a tile whose outline its
component hides at runtime (`KitTile.borderAtRest`). When a fix turns on either of those, assert it in the generator
(the configure strip asserts its arrows are plain fixed buttons) instead of trusting the render. It does not model a
GridLayoutGroup or the preferred height a nested Vertical/Horizontal layout group passes up either (the Codex grid and
page, section 88), so blocks inside a scrolling page render stacked on top of each other. Check those by their layout
settings.

**T9 — `gates.sh` runs only the `author_*_assets.py` generators; the menu generators are not in it**
(claude/new-session-76hnyc, 2026-10-26, the Codex). Every `author_menu_*.py` reads and writes `Menu_Main`, so a change
to the scene or to a shared emitter (`author_menu_arcade.emit_node`) can put any of the twenty in drift with GATES:
ALL OK on screen. Sweep them yourself: `for f in Tools/Build/author_menu_*.py; do python3 "$f" --check >/dev/null 2>&1;
echo "$? $f"; done`. Also, `check_generated_assets.py --base` audits COMMITS, not the working tree ("audited 0 added"
before the commit), and it needs `DOTNET_ROOT=/root/.dotnet`.

**T10 — the menu emitters do not share every RectTransform field.** `author_ui_kit_prefabs.emit_node` writes a node's
`scale`; `author_menu_arcade.emit_node` (the configure / profile / credits chain) wrote `{x: 1, y: 1, z: 1}` whatever
the node said, until section 88 made it read `node.scale`. A generator that sets `.scale` (or `.rot`) and is not on the
Hangar's own emitter must assert the field in its validation. The Codex asserts its 0.88. Otherwise the setting
silently does nothing.

**T11 — lifting a lock (or retiring any optional part) must remove the documents the generator minted for it.**
`author_menu_topnav.py` removed only the ids its CURRENT config mints, so taking CODEX out of `LOCKED` would have
stranded the old lock view in the scene. It now removes `Ids.everything()` and allows those removals in its blast-radius
check. Any generator whose output depends on a set (locked tabs, shown seats, offered modes) needs the same: remove
everything it can ever mint, then write what it mints now.

## Cross-cutting checks

Run these regardless of task, since they are the project's known failure modes:

- No new hardcoded colour literals in `Assets/_Scripts/UI/` (the count only goes down)
- No new scene overrides on any GameCanvas instance
- No `SetActive` toggling introduced where `CanvasGroup` alpha was in use — subscriptions are load-bearing
- Serialized SOAP references intact on any touched prefab; the project is fail-loud by design and a lost reference throws at runtime
- No new prefab created as a copy where a variant was possible
