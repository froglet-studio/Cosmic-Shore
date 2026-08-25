---
name: ship
description: End-of-branch shipping protocol - review everything on the branch, prove any editor tool's ASSET OUTPUT actually landed (§2.5, never skipped), complete the documentation so the work is ready to build from, make an honest go/no-go call (pushing back with a concrete iteration list when the branch needs another pass), and only then open the pull request. Use when a feature branch feels done ("wrap this up", "open the PR", "ship it"), before ANY pull request is created, or at the end of a long working session. Depth variants: /ship-quick (fast), /ship-deep (thorough), /ship-tools (tool output + retirement only). Pairs with /reorient (run it first when the session has run long or bleeding-edge may have moved).
---

# Ship Protocol — review, document, decide, then PR

You are closing out a feature branch. The goal is a pull request another developer can
review, build on, and trust — or an honest "not yet" with a concrete list of what one or
two more iterations should fix. **Opening the PR is the last step, never the first.**

## 0. Pick the depth

| You want | Use | What changes |
|---|---|---|
| The default, full protocol | `/ship` | Everything below. |
| A small, already-reviewed branch out the door | `/ship-quick` | Trims the §2 review and §3 doc passes. **Never** trims §2.5. |
| A big branch, a LOCKED system, or a long session | `/ship-deep` | Adds an adversarial re-read, a blast-radius sweep, and a doc-drift sweep. |
| Only to land an editor tool's OUTPUT (no PR) | `/ship-tools` | Runs §2.5 alone, then retires the tool and pushes. |

`/ship <mode>` works too (`/ship quick`, `/ship deep`, `/ship tools`).

**§2.5 is in every mode. Speed comes out of review depth, never out of the gate** — a
fast path that can silently drop a tool's output is the exact failure this protocol
exists to prevent.

## 0.1 Reorient first (when in doubt)

If the session has run long, or bleeding-edge may have moved since the branch was cut,
run the `/reorient` skill first and act on its verdict before shipping.

## 1. Survey the branch

- `git log --oneline <base>..HEAD` and `git diff --stat <base>..HEAD`. **`<base>` is the
  MERGE BASE, not the base branch's tip** — `git merge-base origin/bleeding-edge HEAD`.
  Diffing against the tip of a branch that moved while you worked reports every commit
  THEY landed as deletions in YOUR diff, which reads as a catastrophic branch and is
  entirely an artefact. Read the full commit list; re-read any diff hunk you can't
  summarize from memory.
- **Merge the base branch in before reviewing**, so you resolve conflicts rather than
  leaving them for a reviewer, and so §2 reviews the tree that will actually land.
  Watch for conflicts a text merge CANNOT see: two branches independently claiming the
  same new **doc section number** (both took `§4.6`) merges clean per-hunk and produces a
  document with two of them. When you renumber, renumber every inbound reference — and
  only YOURS: grep the whole repo, then split the hits by which section they mean.
  **The same collision happens WITHOUT a second branch, against the document's own past** —
  a migration-tracker row id (`C6`), a bug id (`B10`), a test id. You pick the "next" id by
  reading the last row, and the last row is not the highest: `PRISM_ANIMATION.md`'s tracker
  runs C1…C13b with C6 sitting mid-table. Grep for the id you intend to claim BEFORE writing
  it into code comments, doc prose, tool docstrings and commit messages — by the time you
  notice, it is spread across a dozen files and the fix is a sweep, not an edit.
- **A parallel mode/feature branch will have claimed the same ENUM VALUES.** The id-collision
  rule above is not only about doc sections and tracker rows: two branches adding a game mode, a
  toast situation, a log-channel bit, or any hand-numbered enum member both pick "the next one",
  and git merges the two additions cleanly into an enum with duplicate values. One session hit
  this on THREE enums at once (`GameModes` 42, `GameToastSituation` 60-62, `CSLogChannel 1 << 2`).
  Resolve by renumbering YOURS (theirs already merged), then sweep every place the NUMBER travels
  rather than the name — for a game mode that is the enum, the arcade card asset's `Mode:`, and
  the toast config's `gameMode:`/`situation:`/`resetOnSituation:` ids. Code that switches on the
  enum by NAME needs no change, which is exactly why the stale numbers hide in assets. Verify with
  a duplicate-value check over the whole enum, not just your own rows.
- **"Keep both sides" is right for list entries and WRONG inside a chain.** Resolving conflicts by
  concatenating HEAD and theirs works for independent fields, list items and doc paragraphs. It
  produces invalid code when both sides are links in one expression: two halves of a `&&` chain
  become two statements, and two halves of a `+` string chain become two ARGUMENTS (a `HelpBox`
  call silently gaining a third parameter). Compile after every keep-both resolution — and treat
  any conflict hunk whose last non-blank character is `&&`, `+`, `,` or `?` as one needing a
  hand-joined merge, not a concatenation.
- **A parallel branch may have fixed the SAME root cause while you worked.** Read the base
  branch's new commits by subject before you resolve anything — this is not a merge
  conflict, it is a design collision, and git will happily interleave two fixes for one
  bug into a tree that carries both. When it happens: pick ONE implementation on merit and
  take it **wholesale** (`git checkout --theirs <file>`), then delete the machinery yours
  needed and theirs does not — a half-merged pair of fixes is worse than either. Then
  **re-scope your docs to what is still genuinely yours**: your section was written when
  you owned the whole story, and left as-is the repo ends up with two competing narratives
  of one bug. Reference theirs rather than restating it, and keep only the part they do
  not cover. Expect this whenever the base branch touched the same files — check with
  `git log --oneline <merge-base>..origin/<base> -- <your changed files>`.
- **If the branch ships a doc that cites `file:line`, the merge just rotted it.** Line
  references are the one kind of prose that goes stale from a commit that never touched
  your branch. After merging the base, re-resolve EVERY reference mechanically — extract
  them, confirm the file exists, confirm the line is in range, and then **confirm the
  content at that line still says what you claimed**. The third check is the one that
  matters: in-range passes happily while pointing at the wrong line. One session shipped
  a follow-up doc whose six `Cell.cs` refs all slid 22 lines because an unrelated upstream
  commit added 28, and the same sweep caught an off-by-one that had been wrong since it
  was written. Where the reference has to survive, **anchor on the symbol and demote the
  number to a hint** ("`RetireWorldIntoSuctionRoot` (`:2058`) — re-grep before trusting
  the number"), because the next drift is not preventable, only survivable.
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
- **A doc that asserts a consequence is not evidence the consequence happens — find the
  PRODUCER.** When a doc (or a verification step, or CLAUDE.md) says "X still lands", "contact
  costs Y", or "the gate leaves Z alone", grep for who actually *calls* the thing that produces
  X/Y/Z for that vessel/mode/path. A passthrough that is genuinely correct — the gate really
  does leave the channel alone — reads as verified even when nothing upstream is pushing
  anything through it, so the claim survives review indefinitely. Four such claims shipped
  across three docs plus CLAUDE.md describing a prism-collision slow on vessels that had no
  slow effect wired at all. The same shape hides behind NAMES: a serialized field called
  `vesselSlowedByRhinoDangerPrismEvent` under a `"Slow Viewer Integration"` header belonged to
  an effect that only muted an input. Treat "the docs say so" and "the identifier says so" as
  hypotheses to check, never as the check.
- **A number read off a ScriptableObject's FIELD INITIALIZER is not the number the game runs
  on.** The SO declares `public float dynamicMaxDistance = 40f;` and the ASSETS say 250. Reading
  the class is fast, feels authoritative, and is the wrong source — the assets are the game. One
  session built a platform law's central premise ("a pilot's own hull is always 10-40 units from
  its camera, so the near cutoff excludes it for free") on exactly that, and the shipped fleet
  spanned 6.7 to 250, so two of eight vessels marked their own ship. The premise then propagated
  into an `IsSane` branch, an edit-mode test and three documents, **all self-consistent**, because
  every one of them traced back to the same default rather than to any asset. Self-consistency
  across artifacts is not corroboration when they share one upstream source. So: whenever a claim
  turns on an authored value, enumerate the ASSETS (`AssetDatabase.FindAssets("t:Foo")`, or grep
  the `.asset` YAML) and tabulate the real spread — and where the claim must keep holding, make the
  gate re-measure from the assets rather than restating the number, so it fails loudly when an
  artist re-authors one.
- **The mirror of that rule: a "dead surface" claim decays into a live path, and nothing
  announces it.** "Unreferenced", "no producer anywhere", "provably dead" are true *as of a
  date* — the next feature branch is free to wire the thing up, and it will not think to go
  correct a deadness claim it never read. So a doc, comment, or prompt that licenses a
  DELETION must have its emptiness re-proved at ship time, not inherited: grep for the
  producer/caller again, now. `PrismType.Grow` was documented dead in three doc sites and two
  code comments, acquired a producer two days later, and the stale instruction to delete it
  survived in a follow-up prompt that a fresh session would have executed. Deadness claims
  are the most dangerous kind of stale doc, because acting on one is irreversible and the
  code that proves them wrong is somewhere you were told not to look.

## 2.5 Tool-output gate — NEVER SKIPPED, IN EVERY MODE

**An editor tool's deliverable is the DATA it writes, not the tool.** The tool lands in
the branch because you committed it; its output lands in the human's *working tree*,
where nothing forces anyone to notice it. Merge the PR and you have shipped code that
expects a scene / prefab / SO nobody pushed — broken on every other machine, with
nothing in the diff to explain it. This gate is the only thing standing between that
and a green PR, so it runs even in `/ship-quick`.

**1. Classify every tool the branch adds or changes.** Find them:

```sh
git diff --name-only <merge-base>..HEAD -- '*.cs' | xargs -r grep -l 'MenuItem("FrogletTools/'
```

For each hit, read it and decide from the CODE, not the name:

| Kind | Evidence in the source | What the branch must contain |
|---|---|---|
| **READER** | only logs / `Debug.Log` / builds a report; no `AssetDatabase.Save*`, `PrefabUtility.*`, `EditorSceneManager.Mark*`, `ApplyModifiedProperties`, `AssetDatabase.CreateAsset`, `File.Write*` | nothing — say so explicitly in the report |
| **WRITER** | any of the above | its output, committed |

**2. Prove a WRITER's output is on the branch.** Two independent checks, both required:

```sh
git status --porcelain -- Assets ProjectSettings          # nothing tool-shaped may be dirty
git diff --stat <merge-base>..HEAD -- '*.unity' '*.prefab' '*.asset'
```

- Dirty `Assets/**` in the first command is the smoking gun. Do not commit it blind —
  read it, confirm it is that tool's output, then commit it *as its own commit*.
- A WRITER in the diff with **zero** asset changes in the second is the silent failure.
  It means one of: the human has not run it yet, they ran it and never saved, or they
  saved and never committed. You cannot tell from here — **ask** (step 3).
- Also check `Library/FrogletToolChangeLedger.json` when it exists (machine-local, and
  absent in a remote container): it names the exact paths each tool wrote.
- **Grep for `AssetDatabase.CreateAsset`, never a bare `CreateAsset`** — `[CreateAssetMenu]` is
  an attribute on hundreds of ScriptableObject classes and matches the short form, so a bare grep
  reports ordinary gameplay SOs as WRITER tools. Confirm every hit by reading the matching line;
  a hit whose line is an attribute is not a writer, and a branch whose only "writers" are
  `[CreateAssetMenu]` attributes has **no tools at all**.

**3. Prompt the human — this is a blocking question, not a note.** Use
`AskUserQuestion`, name each WRITER tool and the asset paths it targets, and ask which
is true: *ran it and it is saved* / *ran it, not sure it saved* / *have not run it yet* /
*it is read-only, no output expected*. Anything but the first or last means **NO-GO**
until it is resolved. In-editor, `FrogletTools ▸ Build ▸ Pending Tool Changes` answers
this in one click and can push the output itself.

**4. Verify what came back**, then re-run step 2 — the human saving in Unity changes the
working tree under you. Check the recovered output the way §2 checks anything else:
every new asset has its `.meta`, no orphan `.meta`, no `Missing (Mono Script)` rows, GUID
references resolve.

**5. Retire the one-offs.** A tool written to perform one migration is scaffolding, not
surface area: once its output is verified and pushed, delete the tool and its scratch
assets in their own commit (`chore(tools): retire <name> after verification`). Keep it
only if it is idempotent and re-runnable — an auditor, a validator, a wirer someone will
need again — and if you keep it, say why in the PR body. `/ship-tools` automates this
whole section.

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
- **§2.5 is unresolved** — a WRITER tool is on the branch and its output is not, or the
  human has not confirmed they ran it. This one is not a judgment call: no amount of
  otherwise-good work makes a half-landed migration shippable.
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
  still verify in-editor, with steps), **Tool output** (every tool the branch touched,
  reader vs writer, which commit carries its output, which tools were retired and which
  were kept and why — §2.5), **Follow-ups** list, collider/perf impact where the ecology
  gate applies.
- Base is `bleeding-edge` unless told otherwise. After creating, subscribe to PR
  activity and keep watch (CI, reviews) until merged or told to stop.

## 6. Report

Tell the prompter: the go/no-go call and why, the PR link (or the iteration list), the
**§2.5 tool-output verdict** (every tool classified, whose output landed in which commit,
what was retired), the follow-ups you recorded, and the §3.5 skill-capture outcome
(skills created/extended, or the explicit "nothing reusable this session").
