---
name: ship
description: End-of-branch shipping protocol - review everything on the branch, complete the documentation so the work is ready to build from, make an honest go/no-go call (pushing back with a concrete iteration list when the branch needs another pass), and only then open the pull request. Use when a feature branch feels done ("wrap this up", "open the PR", "ship it"), before ANY pull request is created, or at the end of a long working session. Pairs with /reorient (run it first when the session has run long or bleeding-edge may have moved).
---

# Ship Protocol — review, document, decide, then PR

You are closing out a feature branch. The goal is a pull request another developer can
review, build on, and trust — or an honest "not yet" with a concrete list of what one or
two more iterations should fix. **Opening the PR is the last step, never the first.**

## 0. Reorient first (when in doubt)

If the session has run long, or bleeding-edge may have moved since the branch was cut,
run the `/reorient` skill first and act on its verdict before shipping.

## 1. Survey the branch

- `git log --oneline <base>..HEAD` and `git diff --stat <base>..HEAD` (base is
  `bleeding-edge` unless told otherwise). Read the full commit list; re-read any diff
  hunk you can't summarize from memory.
- Restate, in a few sentences, WHAT the branch delivers and WHY. If you can't, you are
  not ready to ship — go re-read the diff.

## 2. Review pass (the branch, not just the last commit)

Walk every changed file against these gates:

- **Correctness**: no leftover debug scaffolding, dead code, TODO-as-substitute-for-work,
  commented-out experiments, or hand-authored YAML that never got an editor import pass.
- **Architecture**: changes follow CLAUDE.md patterns (SOAP, config-in-SOs, single-writer,
  no new singletons, threading contract, ecology invariants). A violation is a blocker.
- **Blast radius**: grep for every public API you changed or removed - every caller
  migrated? Renamed/deleted assets - every GUID reference updated?
- **Verification honesty**: list what was actually verified (in-editor play, tests) vs.
  what only compiles-by-inspection. Unverified risk goes in the PR body, not under the rug.

## 3. Documentation pass ("ready to build from")

For every system the branch touched, confirm the docs a NEW developer would reach for are
current — update them if not:

- The system's `Docs/<System>/` or co-located `.md` reference (ARCHITECTURE, mechanics log).
- `CLAUDE.md` if the branch changed a pattern, invariant, or key-files table it states.
- In-editor verification steps for anything that needs a human at the editor (you cannot
  run Unity - the human is the gate; hand them the exact steps and knobs).
- Follow-up work goes in the relevant BACKLOG/TODOS doc, not in your head.

## 4. Go / no-go (push back when warranted)

Say **NO** — and list the concrete iterations needed — when any of these hold:

- A review gate in §2 failed and the fix isn't a quick one.
- The branch mixes an unfinished experiment with finished work (split it instead).
- A change is known-broken or known-untested in a way that would block another dev
  building on it (compile risk on hand-authored assets counts).
- Docs for a touched LOCKED system (ecology, party, threading, scoring) lag the code.

Say **GO** when the work is coherent, documented, and honestly labeled. Loose ends that
don't block building on the branch become a **Follow-ups** section in the PR body — named,
scoped, and assigned a doc home — not reasons to sit on finished work.

## 5. Open the PR (GO only)

- Check for a PR template (`.github/pull_request_template.md` and variants); mirror its
  structure if present.
- PR body: what & why, per-system summary, **verification status** (what a human must
  still verify in-editor, with steps), **Follow-ups** list, collider/perf impact where
  the ecology gate applies.
- Base is `bleeding-edge` unless told otherwise. After creating, subscribe to PR
  activity and keep watch (CI, reviews) until merged or told to stop.

## 6. Report

Tell the prompter: the go/no-go call and why, the PR link (or the iteration list), and
the follow-ups you recorded.
