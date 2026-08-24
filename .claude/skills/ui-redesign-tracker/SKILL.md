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

## Cross-cutting checks

Run these regardless of task, since they are the project's known failure modes:

- No new hardcoded colour literals in `Assets/_Scripts/UI/` (the count only goes down)
- No new scene overrides on any GameCanvas instance
- No `SetActive` toggling introduced where `CanvasGroup` alpha was in use — subscriptions are load-bearing
- Serialized SOAP references intact on any touched prefab; the project is fail-loud by design and a lost reference throws at runtime
- No new prefab created as a copy where a variant was possible
