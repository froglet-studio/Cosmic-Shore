---
name: reorient
description: Pull the latest bleeding-edge, re-evaluate the project state against it, and issue a verdict — continue as planned, course-correct, or hand off to a fresh session with a written handoff prompt. Use when asked to resync with bleeding-edge, sanity-check the session's direction, or when the session has run long and may be working against stale information.
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

If there are zero new upstream commits, say so, skip to step 4, and weigh
only context health.

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
