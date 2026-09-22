---
name: reorient
description: Pull the latest bleeding-edge, re-evaluate the project state against it, and issue a verdict — continue as planned, course-correct, or hand off to a fresh session with a written handoff prompt. Use when asked to resync with bleeding-edge, sanity-check the session's direction, or when the session has run long and may be working against stale information. Also logs (never acts on) the refactor/cleanup opportunities the resync surfaces, per the /refactor skill.
---

# Reorient — resync with bleeding-edge and re-evaluate the session

You are mid-session on a working branch. Upstream (`bleeding-edge`) may have
moved, and this session may have drifted or burned most of its context.
Execute the steps below in order and end with exactly one clearly labeled
verdict.

## 1. Snapshot the session

Before touching the remote, establish (for your own reasoning and the final
report):

- Current branch, session work so far (`git log --oneline origin/bleeding-edge..HEAD`),
  and uncommitted changes (`git status --short`).
- A one-sentence statement of what this session is trying to accomplish.
- The files/systems this session has modified or plans to modify.

If there is meaningful uncommitted work, commit it now (a WIP commit is fine)
so nothing can be lost in the steps below.

## 2. Pull bleeding-edge

- `git fetch origin bleeding-edge` — on network failure retry up to 4 times
  with exponential backoff (2s, 4s, 8s, 16s).
- New upstream commits: `git log --oneline HEAD..origin/bleeding-edge`
- Upstream-side changes only: `git diff --stat HEAD...origin/bleeding-edge`
  (three-dot).

If there are zero new upstream commits, say so, skip to step 4 (§3.5 is fed by the
upstream diff, so it has nothing to read), and weigh only context health.

## 3. Re-evaluate with the new information

Investigate — do not skim:

- **Docs and rules first.** If `CLAUDE.md`, anything under `Docs/`, or
  `GIT_RULES.md` changed upstream, read those diffs in full. New locked
  designs, retired patterns, or newly resolved decisions override anything
  this session assumed when it started.
- **Overlap.** Intersect upstream's changed files with this session's
  changed/target files. For each overlap, read the upstream change and
  classify it: complementary, conflicting, or superseding.
- **Supersession.** Has upstream already implemented, reverted, or made
  obsolete what this session is doing?
- **Foundation shifts.** Did upstream change APIs, base classes,
  ScriptableObject contracts, SOAP types, or scene wiring that this
  session's work builds on?

## 3.5 Refactor-opportunity pass (log it, never act on it)

You have just read the upstream diff in full and re-read this session's own target
files against it. **That is the cheapest moment in the project to NOTICE structural
debt and the worst one to fix it** — the session already has a task, and a resync that
grows a second subject is the widening `/refactor` §4 forbids. So this pass produces
**rows with evidence attached**, never edits.

Four things this step is uniquely positioned to see:

- **A supersession leaves a vestige.** Any overlap you classified *superseding* in §3
  means upstream replaced something — and the branch that shipped the replacement was
  not looking for what it orphaned. Grep the superseded identifier across code AND
  prose (`/refactor` §3.9); an accessor, a serialized field or a config row nothing
  reads any more is a row.
- **A system that SURVIVED with a changed role.** Grep the upstream-touched type names
  for *fallback / falls back / legacy path / degrades to / kept for* and re-read each
  hit against what the code now does. A comment describing a tier that is no longer
  reachable is the shape that gets cited as evidence later.
- **Upstream docs the upstream change made false.** §3 already had you read the
  `CLAUDE.md` / `Docs/` diffs in full. A claim contradicted by the same push is a row
  for whoever owns that doc, not a silent fix in your branch.
- **Cleanup-labelled rows upstream just added or touched.** If a `BACKLOG`/`TODOS`/
  `REFACTOR` doc changed upstream, its new *cleanup* / *hygiene* / *consistency* rows
  are now in this session's way or adjacent to it. Say which, and whether any is a
  prerequisite for the session's own work — that is the one case where it stops being a
  row and becomes a sequencing fact for the verdict below.

**Keep it bounded.** If context is already thin, name what you saw and where the next
session would measure it — do not open a sweep you cannot finish, and do not let this pass
be the reason §4 comes back HANDOFF.

**A row with no measurement is worse than no row**, because it becomes the next
session's claim to disprove — which is precisely the failure `/refactor` exists to
answer. Attach the command and its output. If the pass finds nothing, say so
explicitly; silence reads as "not checked".

## 4. Assess context health

Honestly judge whether this session has enough context left to finish well:

- Has earlier conversation been summarized away? Are you uncertain about
  decisions you only "remember" making?
- Is the remaining work large relative to the context already consumed?
- Would a fresh session with a good handoff prompt outperform continuing
  here?

## 5. Verdict — pick exactly one and act on it

### CONTINUE
Upstream changes don't threaten the session's direction.

- Merge `origin/bleeding-edge` into the session branch. Resolve conflicts
  honoring upstream docs and locked designs; never silently discard either
  side of a conflict.
- Re-check any overlapping files after the merge, commit, and push with
  `git push -u origin <branch>`.
- Report what's new upstream in one paragraph, then resume the task.

### COURSE-CORRECT
The session's direction conflicts with new upstream reality — superseded
work, contradicted design decision, duplicated effort, or a changed
foundation.

- State precisely what invalidated the direction, citing the upstream
  commits/files.
- Propose the corrected approach. If the correction is unambiguous and stays
  within the original request's scope, merge bleeding-edge and apply it. If
  it changes scope or there are multiple defensible corrections, present the
  options to the user (AskUserQuestion) before proceeding.
- Never relitigate locked designs (eager per-user Relay, mass conservation,
  etc. — see `CLAUDE.md` and `Docs/README.md`). Corrections move *toward*
  locked designs, never away from them.

### HANDOFF
Context is too depleted to finish well, or the corrected task is better
started fresh.

- Commit and push everything to the session branch first — the container is
  ephemeral.
- Skip the merge if conflict resolution would eat the remaining context;
  record it as the new session's first step instead.
- Write a complete handoff prompt containing: the original goal; the branch
  name and what's on it; what is done-and-verified vs. done-but-unverified
  vs. not started; key decisions made and why; upstream changes the new
  session must account for; known gotchas and failed approaches; and
  explicit first steps (usually: fetch + merge `origin/bleeding-edge`, then
  continue at X).
- Output the handoff prompt in a fenced code block as the final deliverable
  so the user can paste it into a new session verbatim.

### OTHER
Anything that doesn't fit the three above — e.g., the task already fully
landed upstream (propose closing out), or the remote is unreachable (report
and stop). Describe the situation and the recommended action.

## Report format

End your reply with:

1. The verdict in bold with a short justification.
2. Upstream summary: commit count plus the notable changes.
3. What you did about it: merge result, correction applied, or the handoff
   prompt.
4. The §3.5 refactor-opportunity outcome: the rows you opened (with where they
   live), anything that is a prerequisite for this session's work, or the
   explicit "nothing found". Never leave this line off — and never satisfy it
   by fixing something.
