---
name: refactor
description: Plan and land a refactor, cleanup, unification, vestige deletion, dead-code removal, type-honesty pass, or "can we collapse these N ways of doing one thing into one" change. Starts by MEASURING the claim the task arrived as - a backlog row, a TODO, "this is sloppy", "why are there three of these" - because such a claim is a hypothesis written at the moment somebody stopped looking, and it is usually wrong about the surface, the blast radius, or which half is dangerous. Then sequences the work the measurement dictates, separates incompleteness (a report) from inconsistency (a fix), proves a behaviour-neutral change without a compiler, and leaves the row closed with what it was WRONG about. Use for "clean this up", "unify these", "is this still used", "delete the dead X", "why are there two ways to do Y", "refactor Z", "retire the old path", or any backlog row labelled cleanup / hygiene / consistency.
---

# Refactor — measure the claim before you plan against it

A refactor task never arrives as a fact. It arrives as a **claim**: a backlog row, a TODO, a
code comment, a reviewer's "this is sloppy", a human's "can we not unify this". The claim was
written at the moment somebody stopped looking, and on this codebase it is wrong often enough
that acting on it directly is the single most expensive thing you can do here.

**Your first deliverable is a measurement, not a plan.** Measured instances, all from rows
somebody had already written down carefully:

| the row said | measured |
|---|---|
| "six `ElementalFloat` fields, behaviour-neutral to convert" | **one** live field, and converting it would have silently switched off an ability, because another system was WRITING it |
| "needs the editor" | the editor was never the hazard; the serialized **data** was |
| "a shared SO mutates its serialized field at runtime" | **dead code** — with a live twin of the same method name on a different class |
| "five open design slots" | **three** — a parallel branch closed two while the report was in review |
| "the audit's 12 disagreements" | **five**; seven were the tool being wrong, not the fleet |
| "44 meshes ship for the 4 that are used" | true, and the reason the call was *replace* rather than chase a receipt |

So: **§1 classify, §2 measure, §3 check the shape against the catalogue, §4 sequence, §5 prove,
§6 delete only behind the gate, §7 leave the trail.** Then `/ship` or `/ship-deep`.

**Where the claim comes from, and why that matters.** Nothing sweeps the tree for this work;
opportunities are noticed during OTHER work and written down for later. Three skills do that
noticing explicitly and none of them is allowed to act on it — `/reorient` §3.5 (a resync has
just read the upstream diff in full, which is when a supersession's leftovers and a
role-changed system are most visible), `/ship` §3.6 and `/ship-deep` D8 (the branch has just
been read adversarially, so the measurement is already in hand). Each hands back **rows with
the command and its output attached**. When you pick a row up here, that evidence is the
thing to re-run first: it was true on the day it was written, which is not today. And when
THIS pass turns up something else — it will — the same rule applies to you (§4, never widen):
a row, not a diff hunk.

---

## 1. Classify — five kinds of work, and only four of them are yours

Name which one before you touch anything. They have different deliverables, and conflating the
last two is the most common failure.

| kind | what it is | deliverable |
|---|---|---|
| **Unification** | N channels doing one job; one of them is addressed wrongly | one channel + every call site migrated + the old one DELETED |
| **Consistency repair** | two call sites that must agree, disagree | make them agree, and say which one was right |
| **Honesty** | the code works; what it *says* is false (a type, a name, a doc comment, a tooltip) | change the thing that lies, not the thing that works |
| **Vestige removal** | it does not run | a deletion **proposal with evidence** (§6), usually not a deletion |
| **Incompleteness** | it was never finished | **A REPORT. Not a fix.** |

> **Incompleteness is not a refactor and must never be dressed as one.** If an element slot has
> no ability, a vessel has no HUD, a mode has no AI — that is a design gap. Writing code to
> "fix" it invents a design decision nobody made. Put it in the system's own gaps report
> (`Docs/ElementalAbilitySystem/FLEET_GAPS.md` is the worked example) and leave it.
>
> The user's own triage rule, verbatim: *"If it is an issue of vessel incompleteness then put
> that into a report. If it is an inconsistent implementation, then lets fix it now."*

**A unification needs a target before it needs a migration.** State the one channel and WHY it
is the right one in a sentence that survives review. The element-scaling pass's sentence was
*"element scaling is PARAMETER-addressed: one `ElementalFloat` on the asset or component that
owns the number"* — and the whole argument for removing the alternative is contained in it: the
retired channel addressed only an ELEMENT, so it had no way to name which parameter it scaled
and every reader of that element got it.

---

## 2. Measure — the recipe

`grep -rn` and ripgrep **time out** on this repo. Use `git grep` with scoped paths:

```sh
git grep -n "<symbol>" -- Assets Docs Tools CLAUDE.md .claude
```

Answer all six of these before you plan. Each one has cost somebody a pass.

**(a) What is the SURFACE?** Enumerate every declaration the claim covers, by name, in a table.
Not "the grow actions" — the six fields on the five classes, listed.

**(b) Is each one CARRIED?** A class nothing instantiates and an asset nothing references are
not the same as a class nothing *mentions*. Resolve the script's own guid from its `.meta` and
sweep it:

```sh
g=$(grep -m1 "^guid:" path/To/Thing.cs.meta | cut -d' ' -f2)
git grep -l "guid: $g" -- Assets | grep -v '\.meta$\|\.cs$'
```

**Never `grep -rl … | head -1`** — exactly one `.meta` OWNS a guid, and an FBX's `.meta` can
carry an `externalObjects` remap INTO another FBX, so the first hit is a plausible false
positive. It put two passes of Rhino jets on a placeholder hull a fifth of the ship's height
(`Docs/VESSEL_CONSTRUCTION.md` §2).

**(c) What does the ASSET author?** Not the C# initializer, not the docs, not the last row of a
table that describes it. Read the block. Four of nine fields in one pass were authored
`Enabled: 0` or flat, which is what turned "convert these" into "convert one".

**(d) Who WRITES it?** A grep for the read does not find the write, and **a refactor of a read
is a refactor of the write.** §3.1.

**(e) Which one is LIVE?** When two things share a name, grep finds the hazard and only
resolving the CALLER'S declared type says which of them is reached. §3.2.

**(f) What ELSE says this is true?** Docs, tooltips, prompts, generators, offline models, a
skill file. Every one of them is a second place the change has to land, and prose is the half no
reference check can find. §7.

**Report the measurement as a table, before proposing anything.** If it contradicts the row —
and it usually does — say so in the same breath. That report is often the most valuable thing
the pass produces, and occasionally it is the whole thing: one measured row's honest answer was
*"this is dead code, so there is nothing to fix here."*

---

## 3. The shapes that keep recurring

Check the task against these before writing a line. Each is one rule and the instance that paid
for it.

**3.1 A read is not the whole surface — somebody writes it too.** `FireGunAction.ProjectileTime`
was an element-scaled parameter that `EnergizeAction` used as a *writable override channel*
(`ProjectileTime.Value = x`, restore on stop). Converting the read to the live evaluator would
have made that write a no-op and switched the ability off with nothing in the console. The write
got a channel of its own (a FLOOR, composed as `Mathf.Max(element, floor)`) **before** the read
moved. General: a value written by one system and read by another is TWO refactors, and the
write does not appear in a grep for the read.

**3.2 Two members can share a name and not a class.** Two `ApplyMaxSizeDebuff` methods existed;
one wrote a shared ScriptableObject's serialized field and was the documented hazard, the other
wrote a private runtime field and was the one anything called. Resolve the caller's
`[SerializeField]` type.

**3.3 "Referenced by nothing" is a statement about what you searched.** A guid sweep cannot see
`Resources.Load` **by name**, and it cannot see a C# type reference. `TMP Settings.asset`
measures zero references and every text component needs it; `PlayFabSDK` measured zero while 20
first-party files compiled against it. A zero inside a `Resources/` folder, or on anything with
code behind it, means nothing (`Docs/LAUNCH_BLOCKER_INDEX.md`).

**3.4 An authored value wins — until the key is absent, and then the INITIALIZER ships.** Unity
applies only the keys a file carries, so a field added to the C# after an asset was last written
takes its C# initializer (the vessel contract's rule 4-i). **This makes a serialized field's
TYPE change the most dangerous edit in this class of work**: turning an `ElementalFloat` into a
`float` turns the YAML from a mapping into a scalar, and a botched block falls back to the
initializer — 3 instead of 120 on the Rhino's skimmer ceiling, a 40× change with nothing to
report it. Rewrite every block in the **same commit**, and have the migration **assert the old
value against the new one before it writes**.

**3.5 A gate catches a dishonest VALUE; only the type fixes a dishonest TYPE.** A build gate
could fail on an `ElementalFloat` authored with a live ramp that nothing evaluates; it
structurally could not see that the type itself was a promise the build could not keep. When you
add a gate, say what it cannot see.

**3.6 A sentinel is not a measurement.** `LeafSize: {0,0,0}` and `MaxTotalSpawnedObjects: -1`
mean *keep what you have*. A measurement layer must resolve a sentinel the way the RUNTIME does,
and a `\d+` regex that skips `-1` by accident rather than by rule is the same bug waiting.

**3.7 A silent clamp or early-return is indistinguishable from a config that never applied.**
`PrismScaleAnimator.SetTargetScale` clamps per axis inside the setter with no log and no return
value, so three passes of flora fitting shipped sizes the engine never used. Before re-fitting a
value that does not read on screen, check what the engine actually STORED.

**3.8 Per-instance state on a shared asset is last-initializer-wins.** A `ScriptableObject` is
shared by every vessel that references it. Save-multiply-`await`-restore on one of its fields
races; the second restore writes the first one's already-multiplied value back as "original".
The fix is per-vessel state in the executor, never a guard on the SO.

**3.9 Deleting a system means sweeping the PROSE, and that is the half no check can find.** The
Wwise removal swept 14 orphan `.meta` files and **five** prose sites, three of which described
live FMOD objects as Wwise and **two of which never named the vendor at all** — a comment citing
a deleted FILE is the worse kind, because it is offered as evidence. Grep the removed
identifier, the removed PATHS and the removed TYPE NAMES across `Assets/**/*.md`, `Docs/`,
`.claude/`, `CLAUDE.md` — and include `Docs/prompts/` explicitly, **because a prompt is code
somebody will run**: one prompt file was still instructing a future session to author the field
the branch had just deleted.

**3.10 A one-shot migration `assert` above a generator's validation makes `--check` vacuous.**
Six of eight mode generators were red and nobody was reading them. A spent one-shot must STAND
DOWN, not abort. And *a `--check` that never reads the disk is not a check.*

---

## 4. Sequence the work the measurement dictates

**Dependencies come out of the measurement, not the row order.** Deleting one dead method
unblocked a type change three rows later, because that method was the only thing writing through
the field. Say the order and why.

**One concern per commit, and the commit message carries the measurement.** A reviewer who
cannot see what you measured cannot check your reasoning. State the surface, what was live, what
the row was wrong about, and which hunks change behaviour.

**Grade every "behaviour-neutral" claim into one of three, per hunk:**

1. **Provably no-op** — the changed path cannot execute, or returns the same value by
   construction (a disabled flag whose evaluator returns the authored value verbatim).
2. **No-op at the authored numbers** — identical for the data that ships today, and it would
   diverge under different authoring. Say which numbers, and where they are authored.
3. **A real change** — then it is a balance or design change. **Flag it for playtest; do not
   bury it in a cleanup.** Two undeclared double-applications of one element were fixed this way,
   deliberately not preserved, at the user's explicit call.

**Never widen.** When the measurement turns up something else — and it will — open a row for it
and keep going. This pass found a latent bug in a sibling class, five dead components and a
shared-state hazard on a live path; all four are rows, none is in the diff. **A refactor branch
that grows a second subject cannot be reviewed as either one.**

---

## 5. Prove it without a compiler

**There is no compiler and no CI in this environment** unless you install one, and `/ship` §0.05
says so. Depth does not buy an exception. What you CAN do, in ascending cost:

**(a) The five standing textual gates.** They do not share a command line — a wrong flag exits 2
with `usage:`, which reads exactly like a finding:

```sh
python3 Tools/Build/check_using_directives.py            # changed-file scoped; prints its scope
python3 Tools/Build/check_enum_member_references.py
python3 Tools/Build/check_switch_label_collisions.py
python3 Tools/Build/check_conditional_compilation.py
python3 Tools/Build/check_self_referential_locals.py --all
```

Plus whatever gate owns the system you are touching (`check_elemental_floats.py --check` and
`--self-test`, `check_credits_manifest.py`, a mode's `author_*_assets.py --check`). Run the gate's
`--self-test` too: **a gate nobody has watched fail is a gate nobody should trust.**

**(b) A Roslyn PARSE of every changed file.** ~40 s to install, no root, and it catches any typo
in your own edits:

```sh
curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
bash dotnet-install.sh --channel 8.0 --install-dir "$PWD/dotnet" --no-path
CSC=$(ls "$PWD"/dotnet/sdk/*/Roslyn/bincore/csc.dll | head -1)
git diff --name-only <base> -- '*.cs' | sed 's/.*/"&"/' > files.rsp   # paths have SPACES
"$PWD"/dotnet/dotnet "$CSC" -langversion:9.0 -target:library -out:/dev/null "@files.rsp"
```

Expect a wall of `CS0518`/`CS0246`/`CS0234` — there are no Unity DLLs here. **Anything else is
real.** State the claim honestly: *"all N changed files parse; nothing but unresolved types"* is
a parse, **not** a type check of the assembly. For genuinely new code, `/asset-surgery` §4's stub
harness is worth the twenty minutes; for a type-swap inside an existing file it is not, because
the risk is grep-complete (below) rather than type-shaped.

**(c) The enumerated-consumer grep, which is the actual proof for a type or signature swap.**
List every reader of every member you changed, migrate each, then re-grep to zero. This is
stronger than a parse for this class of change and it is the thing a reviewer can re-run.

**(d) For an asset migration: assert inside the script.** Before writing, assert the value you
are about to produce equals the value the old shape held, and assert you found every field you
were asked to convert. Then read the resulting diff line by line and confirm the neighbouring
blocks you did NOT mean to touch are untouched.

**(e) A verification matrix, one row per changed system**, columns = *verified how* / *still
needs a human*. **No row may say "compiles."** Legitimate values: measured off the shipped
assets, read-and-grep, parses under Roslyn, provably no-op by construction, gate green,
**not verified — human must build**. A blank row is not a value. This table becomes the PR's
verification section verbatim, and it is where an honest NO usually announces itself.

---

## 6. Deletion has its own gate

**"Referenced by nothing" means nothing is USING it, not that it contains nothing.** The
salvage-before-delete gate (`Docs/LAUNCH_BLOCKER_INDEX.md`, `Docs/VESSEL_CONSTRUCTION_FOLLOWUP.md`)
is a **human verdict**, and nothing is deleted on the strength of a measurement alone.

So the default deliverable for a vestige is a **proposal with its evidence attached**: the guid
sweep per `.meta`, the intra-cluster C# references named as such, the live successor named, and
the one thing that might still want it. This pass proposed five dead components that way and
deleted none of them — and the proposal names `SyncActionWrapper`'s tooltip, which is the only
thing in the tree that still points at one of them.

Delete outright only when the user asks for the deletion, or when the thing is **provably
unreachable AND its removal is the whole point of the change** (an uncalled private method whose
existence is the hazard being fixed). Even then, say in the commit what would have been lost.

Before any deletion of a model, a folder or a third-party tree, read
`Docs/VESSEL_CONSTRUCTION.md` §7 (a model with zero PREFAB references can still be load-bearing
through an `AnimatorController`) and `Docs/THIRD_PARTY_DECISIONS.md` (a removal can have a
licence and a receipt attached to it).

---

## 7. Leave the paper trail

**Close the row where it lives**, and re-derive every count the change invalidated. A gap count,
a "N of M vessels" table, a scanned-blocks number in a gate's output, a doc-index sentence that
transcribes a total — all of them go stale silently. Prefer *"ask the tool"* to a hand-maintained
table wherever a tool can answer.

**Record what the row was WRONG about.** This is the most valuable paragraph you will write,
because the next row was written by the same process. Lead with it.

**Sweep the prose for claims your change made false** (§3.9), and do not stop at the system's own
`Docs/` folder — the per-ability, per-mode and per-vessel docs beside the code are what a reader
opens first, and `Docs/prompts/` is executable.

**Open a row for everything you found and did not do**, with its evidence, its blocking question,
and whether it needs a design call, a playtest or a human verdict. A refactor pass that leaves no
new rows probably did not look very hard.

**Then hand off to `/ship`** (or `/ship-deep` for a LOCKED system, hand-authored asset YAML, or a
long session). Do not open the PR from here.
