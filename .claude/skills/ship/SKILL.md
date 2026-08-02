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
- Read `Docs/EDITOR_TOOL_LEDGER.md`. Any `⏳ PENDING` row this branch's work should have
  discharged is in scope for this ship, not someone else's problem.

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

## 2.5 Editor-tool discharge gate (the un-run tool trap)

**You cannot run Unity.** An editor tool you wrote is *inert* until a human clicks its menu
item and commits the resulting asset diff. Merging the tool without its output ships a
half-landed feature: bleeding-edge gets code that expects wired assets nobody wired, plus a
tool that clutters the menu forever. This gate exists because that has happened repeatedly.

Run `.claude/skills/ship/tool-discharge-check.sh <base>` — it lists the branch's
`[MenuItem]` tools, the asset files the branch changed, and each tool's ledger row. Then
work its output:

**1. Classify every tool the branch adds, changes, or depends on.**

| Kind | Definition | Fate |
|---|---|---|
| **Standing** | Validator, auditor, report, or generator meant to be re-run on demand (`Validate Clock Wiring`, `Audit Vessel Ability Rows`, `Measure Cell Environment Baselines`). | Keep. Ledger row = `standing`. |
| **One-shot** | Authors/migrates/wires assets once, then is dead weight (`Setup Freestyle Toybox`, `Canvas Upgrader`, `Strip Crystal AudioSources`). | Must be **discharged**, then **retired**. |

**2. Prefer eliminating the obligation over documenting it.** Before writing a menu-item
tool at all, check whether `/asset-surgery` can author the asset directly — a programmatic
edit has no pending human step and cannot rot. Only write a tool when the edit genuinely
needs the running editor (importer, mesh/lightmap bake, scene instantiation from runtime
state). Say in the PR which case applies.

**3. A one-shot tool is DISCHARGED only when its output is in this branch's diff.**
`git diff --stat <base>..HEAD -- '*.prefab' '*.asset' '*.unity' '*.shadergraph' '*.mat'`
— a one-shot tool with zero corresponding asset changes is **undischarged**. Do not
rationalize it ("the runtime self-wires", "it's optional") without reading the runtime
consumer and proving the fallback exists; if it does, the tool is a convenience, not a
requirement — say that explicitly in the ledger.

**4. Hand the human ONE copy-pasteable discharge block** (in the ship report *and* the PR
body), per tool, in run order:

```
1. Unity ▸ Tools > Cosmic Shore > <exact menu path>
   expect: <the console/result line that means it worked>
   writes: <exact asset paths or globs>
2. git add <paths> && git commit -m "chore(assets): <tool> output" \
     && git push -u origin <branch>
```

Anything you cannot state precisely (which paths change, what success looks like) is a
sign you don't know what your own tool does — go read it.

**5. Retire the tool once its output is committed.** Delete a discharged one-shot tool in
the same PR (or the immediate follow-up commit), and **rewrite every doc that told the
reader to run it** — past tense, pointing at where the tool now lives, e.g.

> Ran once on branch `claude/foo-abc` (PR #123, `a1b2c3d`). Recover with
> `git show a1b2c3d -- Assets/_Scripts/Editor/FooTool.cs`.

Docs must never point at a menu item that no longer exists, and a retired tool must always
be recoverable by commit reference. Standing tools keep their menu path in the docs.

**6. Record it in `Docs/EDITOR_TOOL_LEDGER.md`.** Every tool the branch adds, runs, or
retires gets its row updated. A one-shot tool that genuinely cannot be discharged before
merge stays as a `⏳ PENDING` row carrying the owner and the full discharge block from
step 4 — that is the **only** acceptable way to merge an undischarged tool, and the PR
body must call it out under Verification. While you're in the ledger, sweep it: any
`✅ RUN` one-shot whose file still exists is cleanup this branch can do now.

## 3. Documentation pass ("ready to build from")

For every system the branch touched, confirm the docs a NEW developer would reach for are
current — update them if not:

- The system's `Docs/<System>/` or co-located `.md` reference (ARCHITECTURE, mechanics log).
- `CLAUDE.md` if the branch changed a pattern, invariant, or key-files table it states.
- In-editor verification steps for anything that needs a human at the editor (you cannot
  run Unity - the human is the gate; hand them the exact steps and knobs).
- Follow-up work goes in the relevant BACKLOG/TODOS doc, not in your head.

## 3.5 Skill-capture retrospective (harvest what the session learned)

Before the go/no-go, review the SESSION (not just the diff) for knowledge worth
keeping — the things that were painful to figure out and would be re-invented
next time:

- **Findings & techniques**: did the session discover a repeatable method (a new
  way to edit an asset class, a validation pattern, a debugging shortcut)?
- **Workarounds & traps**: did anything cost more than ~15 minutes to a
  non-obvious cause (a format quirk, an API that lies, a normalization surprise,
  a clock/coordinate-space mismatch)? Each of those is a trap entry.
- **Pushed-back punts**: did the prompter have to say "you can do this" about
  work you were deferring to them? That gap between assumed and actual
  capability is EXACTLY what a skill exists to close.

Then act on it — this step produces edits, not intentions:

- **Extend an existing skill** when the learning fits one (`ls .claude/skills/`;
  add the trap/technique to the closest skill's list).
- **Create a new skill** when the session established a coherent new capability
  with its own method and trap list (see `/asset-surgery` for the shape: doctrine
  → safety pattern → techniques → traps → limits).
- **At minimum**, name the candidates in the ship report so the prompter can
  decide — silence is the only wrong output. A session that learned nothing
  reusable says so explicitly.

## 4. Go / no-go (push back when warranted)

Say **NO** — and list the concrete iterations needed — when any of these hold:

- A review gate in §2 failed and the fix isn't a quick one.
- The branch mixes an unfinished experiment with finished work (split it instead).
- A change is known-broken or known-untested in a way that would block another dev
  building on it (compile risk on hand-authored assets counts).
- Docs for a touched LOCKED system (ecology, party, threading, scoring) lag the code.
- **§2.5 failed**: a one-shot editor tool on the branch is undischarged AND not registered
  as a `⏳ PENDING` ledger row with a complete discharge block. Shipping the tool without
  the output — or without a written obligation to produce it — is a blocker, not a
  follow-up. (Discharging is usually minutes of the prompter's time: say GO-AFTER-RUN,
  hand them the block, and wait for the push rather than merging half the feature.)

Say **GO** when the work is coherent, documented, and honestly labeled. Loose ends that
don't block building on the branch become a **Follow-ups** section in the PR body — named,
scoped, and assigned a doc home — not reasons to sit on finished work.

## 5. Open the PR (GO only)

- Check for a PR template (`.github/pull_request_template.md` and variants); mirror its
  structure if present.
- PR body: what & why, per-system summary, **verification status** (what a human must
  still verify in-editor, with steps), **Follow-ups** list, collider/perf impact where
  the ecology gate applies.
- **Tool runs required before merge** section whenever §2.5 produced a discharge block —
  reproduce it verbatim, name the ledger rows it clears, and state plainly that merging
  ahead of the run lands the tool without its output. Omit the section entirely when the
  branch adds no one-shot tool; never leave it as an empty heading.
- Base is `bleeding-edge` unless told otherwise. After creating, subscribe to PR
  activity and keep watch (CI, reviews) until merged or told to stop.

## 6. Report

Tell the prompter: the go/no-go call and why, the PR link (or the iteration list), the
follow-ups you recorded, and the §3.5 skill-capture outcome (skills created/extended, or
the explicit "nothing reusable this session").

Lead with the **§2.5 discharge block** if there is one — that is the prompter's next
physical action and it must not be buried under the summary. Then say which tools this
branch retired, and which ledger rows it opened or closed. If the branch added no editor
tool, say "no tool discharge required" explicitly — silence reads as "nothing pending",
which is exactly the failure this gate exists to prevent.
