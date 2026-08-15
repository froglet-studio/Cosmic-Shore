# Documentation Audit — Cosmic Shore

**Date:** 2026-08-15 · **Scope:** all 145 first-party Markdown files (`CLAUDE.md`, `README.md`,
`PLAN.md`, `GIT_RULES.md`, `Docs/**`, `Assets/_Scripts/**/*.md`, `Tools/**`, `.claude/skills/**`).
Third-party vendor docs (Plugins, PlayFabSDK, NiceVibrations, Wwise, ExternalDependencyManager,
YethGameDev) are excluded.

**Method.** Every claim that could be machine-checked was machine-checked against the tree at
`claude/documentation-audit-p5ov1g`: referenced file paths resolved against the real file list,
C# type names resolved against every `class`/`struct`/`interface`/`enum` declaration, enum values
read from source, SO fields read from asset YAML, section anchors resolved against real headers,
and paragraph-level duplicate detection across all docs. Findings below carry the evidence that
produced them. Where a doc and the code disagree, the code was read to decide which side is wrong.

---

## How to use this document

Each finding is self-contained and ends with a **Prompt** — a ready-to-paste instruction for a
fresh session that fixes exactly that issue and nothing else. Prompts are written to be run
independently and in any order, except where a dependency is stated.

Prompts assume the agent will follow `CLAUDE.md` house rules (verify against code, don't invent,
don't restructure without instruction). None of them ask for code changes unless explicitly noted
(D-3, F-2 and G-1 touch source comments or add a tiny doc file; everything else is docs-only).

**Severity:**

| | Meaning |
|---|---|
| 🔴 **Critical** | Actively misleads. An agent or engineer following the doc will write wrong code or waste a session. |
| 🟠 **High** | Wrong or contradictory, but the reader is likely to notice before acting. |
| 🟡 **Medium** | Navigation, redundancy, or lifecycle problems. Costs time, not correctness. |
| 🔵 **Low** | Polish, consistency, and future-proofing. |

---

## Index

| # | Severity | Finding | Primary file(s) |
|---|---|---|---|
| **A. Docs contradict the code** | | | |
| A-1 | 🔴 | Vessel HUD controller/view table names 11 classes; 8 do not exist, 2 marked absent do exist | `CLAUDE.md` |
| A-2 | 🔴 | `DomainAssigner` is documented in 4 places and does not exist in the codebase | `CLAUDE.md`, `MultiplayerArchitecture/…/90-appendix-files.md` |
| A-3 | 🔴 | `ECOSYSTEM.md` §1–§2 still teach "prism count is the spine"; the LOCKED invariant is volume | `Docs/ECOSYSTEM.md` |
| A-4 | 🟠 | `FLEET_MAPS.md` records the Sparrow Mass-5/Space-5 swap that was reverted on 2026-08-13 | `Docs/ElementalAbilitySystem/FLEET_MAPS.md` |
| A-5 | 🟠 | `PlayerCountStepper` documented as a real class; actual classes are `IntStepper` / `PlayerCountButton` | `CLAUDE.md` |
| A-6 | 🟠 | HexRace key-files table names 4 classes that don't exist | `CLAUDE.md`, `Docs/SCENES.md` |
| A-7 | 🟠 | Every file count in the Project Structure tree is understated by 20–55% | `CLAUDE.md` |
| A-8 | 🟠 | "`_Scripts/Game/` contains only non-code assets" — it contains 3 `.cs` files, one of them live | `CLAUDE.md` |
| A-9 | 🟠 | "8 primary namespaces" — there are 10 | `CLAUDE.md` |
| A-10 | 🟡 | "17 test files" in `_Scripts/Tests/Editor/` — there are 54 | `CLAUDE.md` |
| A-11 | 🟡 | Scene inventories miss 4–5 real scenes each; `SCENES.md` predates Dog Fight and Maelstrom | `CLAUDE.md`, `Docs/SCENES.md` |
| A-12 | 🟡 | ~60 dangling file-path references across 30 docs | many |
| **B. Docs contradict each other** | | | |
| B-1 | 🔴 | Three different answers to "what is the Tournament mode's display name?" | `ShuffleSystem/`, `Docs/README.md`, `CLAUDE.md` |
| B-2 | 🟠 | `Docs/README.md` bug-tracker summaries are stale in both directions | `Docs/README.md` |
| B-3 | 🟡 | `Docs/README.md` calls the shipped Shuffle deltas "deferred / not yet built" | `Docs/README.md` |
| **C. Navigation and discoverability** | | | |
| C-1 | 🔴 | 69 of 145 docs are unreachable from any index | all indexes |
| C-2 | 🟠 | `CLAUDE.md`'s Documentation Index omits `ECOSYSTEM.md` — the most cross-referenced doc in the repo | `CLAUDE.md` |
| C-3 | 🟠 | `Docs/README.md` claims to be "the navigation index" but covers 12 of 44 entries | `Docs/README.md` |
| C-4 | 🟡 | Root `README.md` game-mode list is ~2 years stale and lists modes that no longer exist | `README.md` |
| **D. Redundancy and duplication** | | | |
| D-1 | 🟠 | `MultiplayerArchitecture/src/content/` is a second, drifting copy of 6 subsystem docs | `Docs/MultiplayerArchitecture/` |
| D-2 | 🟠 | `CLAUDE.md` inlines whole sections that exist as dedicated docs (~40% of its 292 KB) | `CLAUDE.md` |
| D-3 | 🟡 | `GameModes.cs` enum comments duplicate — and contradict — the mode docs | `Assets/_Scripts/Data/Enums/GameModes.cs` |
| D-4 | 🔵 | 6 byte-identical paragraph blocks duplicated across doc pairs | various |
| **E. Lifecycle: ephemera parked in a reference tree** | | | |
| E-1 | 🟠 | 8 session-scoped docs (kickoffs, handoffs, overnight logs) sit unmarked next to canonical refs; all cite dead branches | `Docs/*_KICKOFF.md`, `*_HANDOFF.md`, `*_LOG.md` |
| E-2 | 🟠 | `PLAN.md` at repo root is a dead scratch plan for work that shipped differently | `PLAN.md` |
| E-3 | 🟡 | `UNITY_VERIFICATION_CHECKLIST.md` has 14 🔴 and zero 🟡/🟢 — the close-out half has never run | `Docs/UNITY_VERIFICATION_CHECKLIST.md` |
| **F. Ambiguity and missing definitions** | | | |
| F-1 | 🔴 | Player-facing mode names are used as if defined but are documented nowhere ("Scurry", "Skim Race") | many |
| F-2 | 🟠 | Code says `Block`, docs say `Prism`; no doc states they are the same thing | all |
| F-3 | 🟠 | The "tests live under `Editor/`" rule is stated absolutely; its own table shows the exception | `CLAUDE.md` |
| F-4 | 🟡 | No repo-wide glossary; the only one is buried in the PDF sources and is multiplayer-scoped | `Docs/` |
| **G. Structural / process** | | | |
| G-1 | 🟡 | No convention for doc status, ownership, or freshness; git dates are useless (squashed history) | all |
| G-2 | 🟡 | `Docs/README.md` "shared conventions" apply to 4 folders but 7 folders now exist | `Docs/README.md` |
| G-3 | 🔵 | No automated doc-drift gate, though every check in this audit is scriptable | `Tools/Build/` |

---

# A. Docs contradict the code

## A-1 🔴 The vessel HUD table is almost entirely wrong

**Where:** `CLAUDE.md:1993` (§ Lava-Lamp Mode → "Per-vessel HUD controllers").

**Evidence.** The table lists `MantaHUDController`, `MantaHUDView`, `RhinoHUDController`,
`RhinoHUDView`, `SerpentHUDController`, `SerpentHUDView`, `DolphinHUDView`, `SquirrelHUDView`.
None of those eight types exist. The real declarations are:

| Documented | Actual |
|---|---|
| `MantaHUDController` / `MantaHUDView` | `MantaVesselHUDController` / `MantaVesselHUDView` |
| `RhinoHUDController` / `RhinoHUDView` | `RhinoVesselHUDController` / `RhinoVesselHUDView` |
| `SerpentHUDController` / `SerpentHUDView` | `SerpentVesselHUDController` / `SerpentVesselHUDView` |
| `DolphinHUDView`, controller listed as "—" | `DolphinVesselHUDView` **and `DolphinVesselHUDController` exists** |
| `SquirrelHUDView`, controller listed as "—" | `SquirrelVesselHUDView` **and `SquirrelVesselHUDController` exists** |
| `SparrowHUDController` / `SparrowHUDView` | correct (the only correct row) |

The two "—" entries are the damaging half: they tell a reader the Dolphin and Squirrel have no HUD
controller, which is the opposite of the truth and would send someone to build one.

**Prompt**

```
In CLAUDE.md, fix the per-vessel HUD table under "Lava-Lamp Mode" (around line 1993).

Verify every entry against the real declarations first:
  grep -rhoE "class [A-Za-z0-9_]+(HUDController|HUDView)" --include=*.cs Assets/_Scripts/UI | sort -u

The correct classes are MantaVesselHUDController/View, RhinoVesselHUDController/View,
SerpentVesselHUDController/View, DolphinVesselHUDController/View,
SquirrelVesselHUDController/View, and SparrowHUDController/SparrowHUDView (the Sparrow is the
only one without the "Vessel" infix — keep that asymmetry, it is real).

Remove the "—" placeholders in the Dolphin and Squirrel controller cells; both controllers exist.
Add a file-path column so the table is verifiable at a glance. Change nothing else in the section.
```

---

## A-2 🔴 `DomainAssigner` does not exist

**Where:** `CLAUDE.md:265` (structure tree), `:770`, `:778` (spawn-flow diagram), `:1122` (full API
description), plus the Key Files table and `Docs/MultiplayerArchitecture/src/content/90-appendix-files.md`.

**Evidence.** `grep -rn "DomainAssigner" --include=*.cs Assets/` returns **zero matches**. The type
is described in `CLAUDE.md` in unusual detail — `Initialize()`, `GetDomainsByGameModes()`, its pool
contents, its exhaustion return value, and a "**Must** be called per session start" instruction —
and it appears as a step in the documented player-spawn chain. All of it is fiction as far as the
current tree is concerned.

**Why it matters.** This is the worst failure mode in the set: an agent told "`DomainAssigner.Initialize()`
must be called per session start to prevent duplicate/swapped domains" will go looking for a bug in
a call that cannot exist, or will recreate the class. `CLAUDE.md` also states the real mechanism
elsewhere — `Player.NetDomain` written server-side, with `ServerPlayerVesselInitializerWithAI.GetBalancedDomain`
doing tie-breaks — so the document contains both the live design and a ghost of a retired one.

**Prompt**

```
`DomainAssigner` is referenced throughout CLAUDE.md but does not exist in the codebase
(grep -rn "DomainAssigner" --include=*.cs Assets/ returns nothing).

First establish what actually assigns domains today. Read:
  - Assets/_Scripts/Controller/Player/Player.cs (NetDomain, RequestSetDomain_ServerRpc)
  - Assets/_Scripts/Controller/Multiplayer/ServerPlayerVesselInitializerWithAI.cs (GetBalancedDomain)
  - Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs
  - Assets/_Scripts/System/MainMenuController.cs

Then remove every DomainAssigner reference from CLAUDE.md and replace it with the real path:
  - line ~265 structure tree (drop it from the Multiplayer/ contents)
  - lines ~770 and ~778 in the Menu_Main spawn-chain diagram
  - line ~1122 bullet (delete the whole bullet, or rewrite it to describe the live mechanism)
  - the "Key Files — Player Spawning" table row
Also fix Docs/MultiplayerArchitecture/src/content/90-appendix-files.md.

If domain assignment genuinely has no single owner today, say so explicitly in one sentence rather
than naming a class. Do not add a DomainAssigner class to satisfy the docs.
```

---

## A-3 🔴 `ECOSYSTEM.md` §1 contradicts the locked "volume is the spine" invariant

**Where:** `Docs/ECOSYSTEM.md:109` (`## 1. The spine: everything keys off **prism count**`) and the
Mermaid diagram node at `:152` (`COUNT["PRISM COUNT<br/>LiveBlockCount + per-domain counts…"]`).

**Evidence.** `CLAUDE.md`'s LOCKED ecosystem invariants say:

> **Volume is the spine.** Phase, dominant domain, prey, HUD all key off per-domain **VOLUME**
> (`Cell.LiveVolume`), not prism count. Count is a rare frenzy/perf backstop only.

The code agrees — `Cell.cs:1054`:

```csharp
var newPhase = CellPhaseRules.Compute(LiveVolume, LiveBlockCount, phase, in thresholds);
```

Volume is the primary argument; count is the backstop. But `ECOSYSTEM.md` §1 — the section a reader
hits first, titled "The spine" — still opens "One state variable drives the whole system: the cell's
live prism count," and the §2 architecture diagram still puts PRISM COUNT at the centre. §13 later
introduces the volume redesign without ever going back to correct §1 or the diagram.

**Why it matters.** `ECOSYSTEM.md` is the mechanics log for a LOCKED system and the `/ecology` skill
routes people into it. Its first two sections teach the superseded model, and every phase-threshold
mistake this project has logged (Rampage §27 item 4, Ribcage, Astro League) traces back to reasoning
in counts instead of volume.

**Prompt**

```
Docs/ECOSYSTEM.md §1 and the §2 diagram still teach the retired "prism count is the spine" model,
contradicting the LOCKED invariant in CLAUDE.md ("Volume is the spine") and the code
(Cell.cs:1054 — CellPhaseRules.Compute(LiveVolume, LiveBlockCount, ...) takes volume first,
count as backstop).

Read Docs/ECOSYSTEM.md §0, §1, §2, §13, §13.1 and Assets/_Scripts/Controller/Environment/Cell.cs
(LiveVolume, GetDomainVolume, LiveBlockCount, GetDomainBlockCount, RecomputePhase) before editing.

Rewrite §1 so VOLUME is the spine:
  - retitle it, and lead with Cell.LiveVolume / GetDomainVolume
  - keep the force table (the +/- sources are unchanged and still correct)
  - state explicitly that LiveBlockCount survives only as a frenzy/perf backstop, and cite the
    CellPhaseRules.Compute signature as the proof
  - keep the §0 "the minus column is exhaustive / mass is conserved" note intact

Update the §2 Mermaid diagram node at line ~152 from "PRISM COUNT / LiveBlockCount" to the volume
spine, keeping the count as a clearly-secondary backstop node.

Add one line at the top of §1 pointing forward to §13/§13.1 for the nucleus control-zone refinement,
so a reader who stops at §1 still ends up with the current model. Do not restructure sections 3+.
```

---

## A-4 🟠 `FLEET_MAPS.md` records a Sparrow upgrade swap that was reverted

**Where:** `Docs/ElementalAbilitySystem/FLEET_MAPS.md:57–58`.

**Evidence.** FLEET_MAPS still says Mass 5 is `*(open again — Shielded Prisms moved to Space 5,
2026-08 round 4)*` and gives Space 5 both pierce *and* shielded turret prisms. The authored asset
disagrees — `Assets/Resources/ElementalAbilityMaps/Sparrow.asset`:

```yaml
- Element: 2                      # Mass
  UpgradeLabel: Shielded Prisms
  UpgradeDescription: ... Briefly lived at Space 5 (2026-08 round 4) and was returned here by
    design sign-off: MASS owns the substance of what you fire, SPACE owns its reach.
```

and so does the code — `FullAutoBlockShootActionSO.cs:81–89` defines `FiredPrismState.ShieldedAtMass5`
as the shipped default. `CLAUDE.md` has the correct account (returned to Mass on 2026-08-13);
FLEET_MAPS was simply never updated.

**Prompt**

```
Docs/ElementalAbilitySystem/FLEET_MAPS.md lines 57-58 are stale: they still record the 2026-08
round-4 move of "Shielded Prisms" from Mass 5 to Space 5. That move was reverted by design sign-off
on 2026-08-13.

Ground truth (read these, don't take my word):
  - Assets/Resources/ElementalAbilityMaps/Sparrow.asset  (Element: 2 = Mass carries
    UpgradeLabel "Shielded Prisms"; Element: 3 = Space carries "Piercing Bullets")
  - Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/FullAutoBlockShootActionSO.cs
    (FiredPrismState.ShieldedAtMass5 is the authored default)

Fix the Sparrow table so Mass 5 = Shielded Prisms and Space 5 = Piercing Bullets (reach + pierce on
both fire modes, no shield). Preserve the split rule the asset states — MASS owns the SUBSTANCE of
what you fire, SPACE owns its REACH — and keep a one-line note that it briefly lived at Space 5 and
was returned, so the history isn't lost.

Then re-read Docs/ElementalAbilitySystem/AUDIT.md and CLAUDE.md's fleet-status table for the same
stale claim and align all three.
```

---

## A-5 🟠 `PlayerCountStepper` does not exist

**Where:** `CLAUDE.md:1615`, `:1666`, `:1668` and the "Key Files — Player Count" table.

**Evidence.** `CLAUDE.md` documents `PlayerCountStepper` at
`_Scripts/UI/Elements/PlayerCountStepper.cs` with a three-field serialized table and an
`Initialize(min, max, current)` signature. No such class or file exists. The real types are
`IntStepper` and `PlayerCountButton`. The doc also describes the legacy `playerCountButtons`
fallback, so it is half-right — which makes the wrong half harder to spot.

**Prompt**

```
CLAUDE.md documents a `PlayerCountStepper` class at _Scripts/UI/Elements/PlayerCountStepper.cs.
It does not exist. The real classes are `IntStepper` and `PlayerCountButton`.

Find them and read them:
  grep -rln "class IntStepper\|class PlayerCountButton" --include=*.cs Assets/_Scripts

Then, in the "Player Count & AI Backfill Pipeline" section of CLAUDE.md:
  - fix the data-flow diagram (~line 1615)
  - rewrite the "#### PlayerCountStepper" subsection (~line 1666) against the real component:
    correct class name, real serialized fields, real init signature, real path
  - fix the Key Files table row
  - verify the claim that ArcadeGameConfigureModal drives it, and correct it if not

If IntStepper is generic and PlayerCountButton is the per-count legacy button, say which one the
modal actually uses today and note the other as legacy.
```

---

## A-6 🟠 HexRace key-files table names four non-existent classes

**Where:** `CLAUDE.md` "Key Files — HexRace", and `Docs/SCENES.md`.

**Evidence.** `HexRaceEndGameController`, `HexRaceHUD`, `HexRaceScoreboard` and
`HexRacePlayerStatsProfile` are all documented with file paths; none is declared anywhere. The real
HexRace types are `HexRaceController`, `HexRaceHUDView`, `HexRaceObjectiveProvider`,
`HexRaceScoreTracker`, `HexRaceScoringRuleSO`, `HexRaceStatsProvider`. Note `HexRaceEndGameController`
is additionally given the path `_Scripts/Utility/DataContainers/` — an end-game controller in the
data-containers folder should have read as a smell.

**Prompt**

```
The HexRace key-files table in CLAUDE.md (and the matching rows in Docs/SCENES.md) names four
classes that do not exist: HexRaceEndGameController, HexRaceHUD, HexRaceScoreboard,
HexRacePlayerStatsProfile.

Enumerate what actually exists:
  grep -rhoE "class [A-Za-z0-9_]*HexRace[A-Za-z0-9_]*" --include=*.cs Assets/_Scripts | sort -u

Rebuild both tables from that list with real paths. For each retired name, work out what replaced it
(e.g. the end-game/scoreboard duties now sit in the shared Scoreboard + MultiplayerHUD path — confirm
this before writing it) and map it, so the tables explain the change rather than just dropping rows.

Then check Assets/_Scripts/Controller/Arcade/HEXRACE.md for the same four names and fix it too.
```

---

## A-7 🟠 Every file count in the Project Structure tree is stale

**Where:** `CLAUDE.md:254–308`.

| Claim | Actual | Drift |
|---|---|---|
| `_Scripts/` ~1,100 C# files | 1,577 | +43% |
| `Controller/` ~536 | 739 | +38% |
| `UI/` ~188 | 226 | +20% |
| `System/` ~126 | 143 | +13% |
| `Data/` ~29 | 45 | +55% |
| `ScriptableObjects/` ~70 | 121 | +73% |
| `SOAP/` 16 subdirectories | 21 | +31% |

The SOAP count is quoted twice (`:308` and `:587`), and `:587` then enumerates the subdirectories by
name — an enumeration that is now missing five entries.

**Prompt**

```
Refresh the file counts in CLAUDE.md's Project Structure tree (lines ~254-308). They are all
understated; ScriptableObjects/ is off by 73%.

Recompute:
  find Assets/_Scripts -name "*.cs" | wc -l
  for d in Controller UI System Data ScriptableObjects Utility; do
    printf "%-20s %s\n" "$d" "$(find Assets/_Scripts/$d -name '*.cs' | wc -l)"; done
  ls -d Assets/_Scripts/ScriptableObjects/SOAP/*/ | wc -l

Update every count. The SOAP subdirectory count appears twice (~line 308 and ~line 587) — fix both,
and regenerate the named enumeration at line 587 from `ls Assets/_Scripts/ScriptableObjects/SOAP/`
so it lists all of them, adding a one-line purpose for each new entry.

Round to the nearest 25 and keep the "~" prefix so small drift doesn't make the doc wrong again.
```

---

## A-8 🟠 `_Scripts/Game/` is described as code-free; it holds live code

**Where:** `CLAUDE.md:328`.

> "A vestigial `_Scripts/Game/` directory exists containing **only non-code assets** (compute shaders,
> input action mappings, material files, and the `PRISM_PERFORMANCE_AUDIT.md`). All C# code has been
> reorganized into the directories listed above."

**Evidence.** It contains three `.cs` files:

- `Game/Environment/CapsuleMembrane.cs` — live gameplay code: renders the cell membrane as an
  instanced icosphere of capsules via `Graphics.RenderMeshInstanced`, with a baked animation preset.
- `Game/Environment/CapsuleMembraneAnimationSO.cs` — its config SO.
- `Game/IO/_Input Mapping/InputActionsAsset.cs` — generated Input System bindings.

`CapsuleMembrane` is also the only meaningful user of the `CosmicShore.Game` namespace (see A-9), and
it is a Cell-owned visual — squarely inside the LOCKED ecology area — sitting in the one directory the
docs tell people to ignore.

**Prompt**

```
CLAUDE.md line 328 claims _Scripts/Game/ contains "only non-code assets". It contains three .cs
files: Game/Environment/CapsuleMembrane.cs, Game/Environment/CapsuleMembraneAnimationSO.cs, and
Game/IO/_Input Mapping/InputActionsAsset.cs (generated).

Read CapsuleMembrane.cs and CapsuleMembraneAnimationSO.cs. CapsuleMembrane is live gameplay code
(instanced capsule membrane rendering with a baked animation preset) and is a Cell-owned visual,
which puts it in the LOCKED ecology area.

Correct the note at line 328 to state exactly what is there and that the directory is NOT code-free.
Then add the membrane renderer to the "Key Systems & Classes" table (Cell environments row or its
own row), with its real path, so it is discoverable from the ecology docs — right now the only place
it is mentioned tells readers to ignore the folder.

Flag, but do not act on, whether CapsuleMembrane.cs should move under Controller/Environment/ to
match the stated reorganisation. That is a code move and needs sign-off.
```

---

## A-9 🟠 "8 primary namespaces" — there are 10

**Where:** `CLAUDE.md:2323`.

**Evidence.** Actual top-level namespace usage:

```
713  CosmicShore.Gameplay      225  CosmicShore.UI        171  CosmicShore.Utility
151  CosmicShore.Core          118  CosmicShore.ScriptableObjects
 66  CosmicShore.Editor         54  CosmicShore.Tests      44  CosmicShore.Data
  4  CosmicShore.ECS             2  CosmicShore.Game
```

`CosmicShore.ECS` and `CosmicShore.Game` are undocumented. Since the section is framed as a
convention ("All game code lives under `CosmicShore.*` with 8 primary namespaces"), the two
unlisted ones read as violations rather than as the small, deliberate exceptions they may be.

**Prompt**

```
CLAUDE.md line 2323 says there are 8 primary namespaces. There are 10 — CosmicShore.ECS (4 files)
and CosmicShore.Gameplay/UI/Utility/Core/ScriptableObjects/Editor/Tests/Data plus CosmicShore.Game
(2 files) are all in use.

Verify:
  grep -rhoE "^\s*namespace\s+[A-Za-z0-9_.]+" --include=*.cs Assets/_Scripts \
    | sed -E 's/^\s*namespace\s+//' | awk -F. '{print $1"."$2}' | sort | uniq -c | sort -rn

Update the count and add rows for CosmicShore.ECS and CosmicShore.Game. For each, read the files and
state in one line whether it is a sanctioned namespace or a stray that should be folded into an
existing one — do not guess. CosmicShore.Game is CapsuleMembrane + its SO (see also finding A-8);
CosmicShore.ECS is the DOTS component set.
```

---

## A-10 🟡 Test-file count is 3× understated

**Where:** `CLAUDE.md:2453` — "`Assets/_Scripts/Tests/Editor/` — 17 test files". Actual: 54.

**Prompt**

```
CLAUDE.md line 2453 says _Scripts/Tests/Editor/ holds 17 test files; it holds 54
(ls Assets/_Scripts/Tests/Editor/*.cs | wc -l).

Update the count and refresh the "covering ..." list so it reflects what is actually tested now.
Generate the coverage summary from the real filenames rather than editing the old list — group them
by area (enums/data SOs, geometry/math, party/presence, vessel/elemental, spawn formation, prism,
etc.) so a reader can tell at a glance whether their area has tests.

While there, verify the other three suite counts in the same section against
Assets/_Scripts/System/Bootstrap/Tests/Editor/, Assets/_Scripts/Controller/Multiplayer/Tests/Editor/,
and Assets/_Scripts/System/Playfab/PlayFabTests/.
```

---

## A-11 🟡 Scene inventories are incomplete and `SCENES.md` is self-declared stale

**Where:** `CLAUDE.md` § Scene Inventory; `Docs/SCENES.md:3` ("Last updated June 2026").

**Evidence.** 27 scenes exist. Missing from `CLAUDE.md`: `SplashScreen`, `BenchmarkStressTest`,
`DensityPartitionBenchmark`, `PrismInstancingStressTest`. Missing from `Docs/SCENES.md`: those three
benchmark/test scenes **plus `MinigameDogFight`** (mode 41) and **`Maelstrom.unity`** (the Tournament
scene, renamed in the v2 rework). `SCENES.md` is described in `Docs/README.md` as the canonical scene
reference, so its missing the newest shipped mode is the sharp edge here.

`SplashScreen.unity` is also notable: `CLAUDE.md` documents a splash step in the bootstrap flow and
`SplashToAuthFlow` as "placed on the splash scene", but never lists the scene.

**Prompt**

```
Both scene inventories are incomplete.

Ground truth:
  find Assets/_Scenes -name "*.unity" | sed 's|Assets/_Scenes/||' | sort
  grep -oE "Assets/_Scenes/[^ ]*\.unity" ProjectSettings/EditorBuildSettings.asset

Docs/SCENES.md is missing MinigameDogFight (GameModes.DogFight = 41) and Maelstrom.unity (the
Tournament/Maelstrom scene), plus BenchmarkStressTest, DensityPartitionBenchmark and
PrismInstancingStressTest. Its header still says "Last updated June 2026".

CLAUDE.md is missing SplashScreen, BenchmarkStressTest, DensityPartitionBenchmark and
PrismInstancingStressTest.

Add every missing scene to both, with its mode + controller where it has one and its purpose where
it does not. Give the benchmark/test scenes their own subsection rather than mixing them with
shipping scenes. Add SplashScreen to CLAUDE.md's core-application scene table and connect it to the
existing SplashToAuthFlow description.

Replace SCENES.md's "Last updated June 2026" line with the convention agreed in finding G-1 (or, if
G-1 hasn't been done, just the current date).

Separately, note in SCENES.md which scenes are in EditorBuildSettings (only 4 are: Bootstrap,
Authentication, Menu_Main, PhotoBooth) and how gameplay scenes are loaded despite that. Verify the
mechanism in SceneLoader.cs before writing it — do not assume.
```

---

## A-12 🟡 ~60 dangling file-path references across 30 docs

**Evidence (sample, full machine-checkable list reproducible with the script in the Prompt):**

| Doc | Dangling references |
|---|---|
| `Docs/PRISM_CLOCK_WIRING_CHECKLIST.md` | `PrismScaleManager.cs`, `MaterialStateManager.cs`, `AdaptiveAnimationManager.cs` (all deleted under the clock-material law) |
| `Docs/PRISM_ECS_MIGRATION.md` | + `PrismEntityBridge.cs`, `AOEComponents.cs`, `Docs/ECS-Migration-Guide-Prisms.md` |
| `Docs/PartySystem/{ARCHITECTURE,REFACTOR,TODOS}.md` | `PARTY_SYSTEM_REFACTOR.md`, `Docs/PARTY_OPEN_BUGS.md` (pre-split filenames) |
| `Docs/ElementalAbilitySystem/AUDIT.md` | `Docs/VESSELS/FLEET_STATUS.md` (no `Docs/VESSELS/` exists) |
| `Docs/DENSITY_PARTITIONING_HANDOFF.md` | `Docs/ECOLOGY_HUD.md` |
| `Docs/ScoringSystem/BUGS.md` | `EndGameCinematicController.cs`, `EndGameVesselDisplayManager.cs` |
| `Docs/CameraMigrationReview.md` | `LegacyCameraController.cs`, `CameraRigAnchor.cs` |
| `Docs/GAMECANVAS.md` | `Assets/Resources/GameModePrefabKit.asset` |
| `Assets/_Scripts/Controller/Arcade/*.md` | 3 × `GameToastConfig_*.asset`, `MultiplayerJoustHUD.cs`, `MultiplayerCrystalCaptureHUD.cs`, … |

Some are legitimately historical ("the deleted `PrismScaleManager`"). The problem is that nothing
distinguishes a deliberate reference to a deleted file from a broken pointer, so a reader can't tell
which is which without checking each one.

**Prompt**

```
Roughly 60 file-path references across ~30 docs point at files that do not exist. Some are
deliberate historical references (e.g. "the deleted PrismScaleManager"); others are stale pointers.
Nothing distinguishes them.

Step 1 — regenerate the list:

  python3 - <<'PY'
  import os,re
  root="."
  skip=("/Library/","/node_modules/","/Assets/Plugins/","/Assets/PlayFabSDK/",
        "/Assets/NiceVibrations/","/Assets/Wwise/","/Assets/ExternalDependencyManager/",
        "/Assets/YethGameDev/","/.git/")
  allf=set(); base={}
  for dp,_,fn in os.walk(root):
      if any(s in dp+"/" for s in skip): continue
      for f in fn:
          p=os.path.relpath(os.path.join(dp,f),root); allf.add(p); base.setdefault(f,[]).append(p)
  pat=re.compile(r'`([A-Za-z0-9_\-./]+\.(?:md|cs|asset|prefab|unity|shader|hlsl|json|py))`')
  for dp,_,fn in os.walk(root):
      if any(s in dp+"/" for s in skip): continue
      for f in fn:
          if not f.endswith(".md"): continue
          rel=os.path.relpath(os.path.join(dp,f),root)
          for m in set(pat.findall(open(os.path.join(dp,f),encoding="utf-8",errors="replace").read())):
              c=m.lstrip("./")
              if c in allf or os.path.basename(c) in base: continue
              print(f"{rel}: {m}")
  PY

Step 2 — for each hit, decide and apply ONE of:
  (a) the file was renamed  -> update the path to the new one
  (b) the file was deleted and the mention is deliberate history -> rewrite it so the reader can
      tell, e.g. "`PrismScaleManager` (deleted 2026-08, see Docs/PRISM_ANIMATION.md §5)"
  (c) the reference is simply dead -> remove it

Verify each with git log --all --diff-filter=D -- <path> before deciding; don't guess between (a)
and (b). Work doc-by-doc and commit per doc so the diff stays reviewable.
```

---

# B. Docs contradict each other

## B-1 🔴 Three different answers for the Tournament mode's display name

**Evidence.** Ground truth is `Assets/_SO_Assets/Games/ArcadeGameTournament.asset:17` →
`DisplayName: Maelstrom`.

| Source | Claim | Status |
|---|---|---|
| `CLAUDE.md:522` | "**Maelstrom** is the player-facing display name" | ✅ correct |
| `Docs/ShuffleSystem/ARCHITECTURE.md:2` | "**Maelstrom** is the player-facing display name" | ✅ correct |
| `Docs/ShuffleSystem/ARCHITECTURE.md:14` | "`DisplayName` (currently `Shuffle`)" | ❌ contradicts its own line 2 |
| `Docs/ShuffleSystem/ARCHITECTURE.md:21` | banner renders `"SHUFFLE"` / `"SHUFFLE RESULTS"` | ❌ would render `MAELSTROM` |
| `Docs/README.md:52` | "`ShuffleSystem/` ← 'Shuffle' = display name of Tournament mode" | ❌ |
| `Docs/README.md:88` | "Find 'Shuffle' (it's Tournament's card display name)" | ❌ |

The `ShuffleSystem` doc's entire purpose is to be the single source of truth for this one field, and
it contradicts itself 12 lines apart.

**Prompt**

```
The Tournament mode's player-facing display name is documented three different ways. Ground truth:
Assets/_SO_Assets/Games/ArcadeGameTournament.asset line 17 -> `DisplayName: Maelstrom`.

Fix Docs/ShuffleSystem/ARCHITECTURE.md:
  - line ~14: "(currently `Shuffle`)" -> "(currently `Maelstrom`)"
  - line ~21: the banner example renders DisplayName upper-cased, so it is "MAELSTROM" /
    "MAELSTROM RESULTS", not "SHUFFLE" / "SHUFFLE RESULTS"
  - re-read the whole file for any other residual "Shuffle" used as the live display name (as
    opposed to the legacy folder name or the IsShuffleComplete flag, both of which stay)

Fix Docs/README.md:
  - line ~52: the tree comment should read: ShuffleSystem/ <- legacy folder name; Tournament's
    player-facing name is "Maelstrom"
  - line ~88: the "How to read these" row should say readers may arrive looking for "Shuffle" OR
    "Maelstrom" and both mean Tournament

Then confirm the display-name value is stated in exactly ONE place across all docs and every other
mention points at it, since that single-source claim is the doc's whole reason to exist.
```

---

## B-2 🟠 `Docs/README.md` bug summaries are stale in both directions

**Where:** `Docs/README.md:18` and `:45`.

| Line | Claim | Reality |
|---|---|---|
| `:18` | PartySystem "open bugs (B2, B5, B7; B3/B8/B9/B10 fixed)" | B7 is ⚪ **deferred**, not open. B8 has a section in `BUGS.md` but **no row** in that file's own summary table |
| `:45` | ScoringSystem "open correctness issues (**B1-B5**)" | `ScoringSystem/BUGS.md` contains **B1–B18** |

`ScoringSystem/BUGS.md` also has no summary table at all, unlike `PartySystem/BUGS.md` — so there is
no cheap way to see status across 18 bugs.

**Prompt**

```
Docs/README.md's bug-tracker summaries are stale.

Line ~18: PartySystem/BUGS.md is summarised as "open bugs (B2, B5, B7; B3/B8/B9/B10 fixed)". Open
that file and reconcile: B7 is marked deferred, not open, and B8 has a "## B8" section but no row in
the file's own summary table.

Line ~45: ScoringSystem/BUGS.md is summarised as "B1-B5". It actually contains B1 through B18.

Do three things:
  1. Add the missing B8 row to PartySystem/BUGS.md's summary table (read the B8 section for its
     title/confidence/status).
  2. Add a summary table to ScoringSystem/BUGS.md matching PartySystem/BUGS.md's format
     (ID | Title | Confidence | Status), generated from its B1-B18 sections.
  3. Rewrite both Docs/README.md lines to cite counts by status rather than enumerating IDs
     ("N open / M deferred / K fixed — see the summary table"), so the index stops going stale every
     time a bug is added.
```

---

## B-3 🟡 `Docs/README.md` calls shipped work "deferred"

**Where:** `Docs/README.md:53–54` — `ShuffleSystem/` described as holding "a deferred list of
planned Shuffle behavior deltas (NOT a separate mode)".

`ShuffleSystem/ARCHITECTURE.md` marks all five deltas ✅ **shipped** and states "All five deltas are
now **shipped**". `CLAUDE.md:522` agrees. Only the index is behind.

**Prompt**

```
Docs/README.md lines ~53-54 describe ShuffleSystem/ARCHITECTURE.md as holding "a deferred list of
planned Shuffle behavior deltas". All five deltas are marked shipped in that file and in CLAUDE.md.

Update the tree comment to describe what the doc is now: a pointer to TournamentSystem/ARCHITECTURE.md
plus the shipped-delta record and the display-name single-source rule. Note the two remaining
one-time editor wiring steps that file lists (BootStatusBroadcaster.tournamentData and the per-scene
Scoreboard.tournamentData) — those are the only genuinely outstanding items, and they belong in
Docs/UNITY_VERIFICATION_CHECKLIST.md too. Add them there if absent.
```

---

# C. Navigation and discoverability

## C-1 🔴 69 of 145 docs are unreachable from any index

**Evidence.** Cross-referencing every `.md` against `CLAUDE.md`, `Docs/README.md`, `README.md`,
`PLAN.md` and `Docs/MultiplayerArchitecture/README.md` leaves **69 orphans (48%)**, including:

- **all 8** `Docs/Analytics/` docs
- **all 4** `Docs/Legal/` docs
- **all 4** build/release docs — `BRANCHING_AND_RELEASE.md` (which `GIT_RULES.md` calls authoritative:
  "If the two ever disagree, that document wins"), `BUILD_AND_DELIVERY.md`, `BUILD_PIPELINE_SETUP.md`,
  `STEAM_BUSINESS_SETUP.md`
- **7 of 11** vessel-ability deep docs (`RHINO_ENERGY_SWORD.md`, `SPARROW_TURRET_STANCE.md`,
  `SQUIRREL_TUBE.md`, …) — despite `CLAUDE.md` saying "Per-ability deep docs live beside the code"
- `Docs/SettingsSystem/`, `Docs/EnvironmentSpawning/`, `Docs/MENU_PROGRESSION_AND_IAP.md`,
  `Docs/ECONOMY_AND_PRICING.md`, `Docs/ECONOMY_TABLES.md`
- 4 of the 5 `PRISM_*` docs

**Why it matters.** The project relies on agents reading `CLAUDE.md` and following pointers. An
unreferenced doc is functionally deleted — worse than deleted, because it still ages and can be found
later and trusted.

**Prompt**

```
48% of this repo's documentation (69 of 145 files) is not referenced from CLAUDE.md, Docs/README.md,
README.md, or any other index. Make every doc reachable.

Step 1 — regenerate the orphan list:

  python3 - <<'PY'
  import os
  root="."
  skip=("/Library/","/node_modules/","/Assets/Plugins/","/Assets/PlayFabSDK/",
        "/Assets/NiceVibrations/","/Assets/Wwise/","/Assets/ExternalDependencyManager/",
        "/Assets/YethGameDev/","/.git/","/.claude/")
  docs=[]
  for dp,_,fn in os.walk(root):
      if any(s in dp+"/" for s in skip): continue
      for f in fn:
          if f.endswith(".md"): docs.append(os.path.relpath(os.path.join(dp,f),root))
  idx=["CLAUDE.md","Docs/README.md","README.md","Docs/MultiplayerArchitecture/README.md"]
  blob="".join(open(i,encoding="utf-8",errors="replace").read() for i in idx)
  for d in sorted(docs):
      if d not in idx and os.path.basename(d) not in blob and d not in blob:
          print(d)
  PY

Step 2 — route each orphan to exactly one home, then link it:
  - Docs/Analytics/*, Docs/Legal/*, Docs/ECONOMY_*, Docs/MENU_PROGRESSION_AND_IAP.md,
    Docs/STEAM_BUSINESS_SETUP.md -> new "Business, Analytics & Compliance" section in Docs/README.md
  - Docs/BRANCHING_AND_RELEASE.md, BUILD_AND_DELIVERY.md, BUILD_PIPELINE_SETUP.md -> new
    "Build & Release" section in Docs/README.md, AND cross-link from GIT_RULES.md (which already
    calls BRANCHING_AND_RELEASE.md authoritative but is not itself reachable from Docs/README.md)
  - the 7 unlisted R_VesselActions/*.md -> a per-ability table in Docs/ElementalAbilitySystem/
    ARCHITECTURE.md, since CLAUDE.md already promises "per-ability deep docs live beside the code"
    without listing them
  - Docs/SettingsSystem/, Docs/EnvironmentSpawning/ -> CLAUDE.md Documentation Index (see C-2)
  - the PRISM_* docs -> link from Docs/PRISM_ANIMATION.md as its companion set
  - Docs/MultiplayerArchitecture/src/content/* -> leave orphaned; they are PDF build inputs, not
    reference docs. Say so explicitly in Docs/MultiplayerArchitecture/README.md (see D-1).

Step 3 — re-run the script. The only remaining orphans should be the PDF content sources.
```

---

## C-2 🟠 `CLAUDE.md`'s Documentation Index omits `ECOSYSTEM.md`

**Evidence.** Of 44 entries in `Docs/`, the Documentation Index table lists 20. The omissions include
`ECOSYSTEM.md` and `ECOSYSTEM_MASTERPLAN.md` — the two most cross-referenced docs in the project
(33 distinct `ECOSYSTEM.md §n` citations across the tree) and the ones the LOCKED "Ecosystem Design
Principles" section depends on. Also missing: `UNITY_VERIFICATION_CHECKLIST.md`,
`PRISM_CLOCK_WIRING_CHECKLIST.md` (mentioned only inside another row's prose),
`DENSITY_PARTITIONING_*`, `Analytics/`, `Legal/`, `SettingsSystem/`, `EnvironmentSpawning/`,
`MultiplayerArchitecture/`, `BRANCHING_AND_RELEASE.md`, `BUILD_*`.

**Prompt**

```
CLAUDE.md's "### Documentation Index" table lists 20 of the 44 entries under Docs/. Most notably it
omits Docs/ECOSYSTEM.md and Docs/ECOSYSTEM_MASTERPLAN.md, even though the LOCKED "Ecosystem Design
Principles" section immediately above depends on both and 33 section-level citations across the repo
point into ECOSYSTEM.md.

Reproduce the gap:
  python3 - <<'PY'
  import os
  c=open('CLAUDE.md',encoding='utf-8').read()
  t=c[c.find('### Documentation Index'):c.find('## Architecture Patterns')]
  for d in sorted(os.listdir('Docs')):
      p=os.path.join('Docs',d); k=d+'/' if os.path.isdir(p) else d
      if not os.path.isdir(p) and not d.endswith('.md'): continue
      if ('`%s`'%k) not in t: print('MISSING',k)
  PY

Add a row for every missing entry, keeping the existing three-column format
(Document | Location | Content) and the existing convention that high-traffic rows carry a bold
"**Read before ...**" clause. Put ECOSYSTEM.md and ECOSYSTEM_MASTERPLAN.md at the top of the added
rows with such a clause, since they gate a LOCKED system.

Do not expand the prose in existing rows — this is an additive pass only.
```

---

## C-3 🟠 `Docs/README.md` overstates its own coverage

**Where:** `Docs/README.md:3–5` — "This README is the navigation index; each linked doc is
self-contained." Its layout tree covers 6 subfolders and 4 loose files. `Docs/` actually holds
**11 subfolders and 33 loose files**. Everything in A-12/C-1 flows through this gap.

The "Shared conventions" section compounds it: "These apply across all three folders" (the text then
lists four), while seven subsystem folders now exist.

**Prompt**

```
Docs/README.md claims to be "the navigation index" for Docs/ but its layout tree covers ~12 of 44
entries (11 subfolders + 33 loose files exist).

Rewrite it as a real index:
  1. Regenerate the layout tree from `ls Docs/` so every subfolder and loose doc appears, each with
     a one-line purpose. Group into sections: Networked subsystems (Party/Presence/NetworkDiagnostics/
     Scoring/Tournament/Shuffle), Simulation & rendering (Ecosystem, Prism, Spatial index, Palette,
     Speed tunnel, Performance), Platform & process (Threading, Tooling, Conditional compilation,
     GameCanvas, Scenes, Unity verification), Build & release, Business/Analytics/Legal, and
     Generated artifacts (MultiplayerArchitecture).
  2. Extend the "How to read these for the first time" table with a row per new section.
  3. Fix the "Shared conventions" preamble: it says "across all three folders" and then lists four;
     seven subsystem folders exist. State which folders actually follow the
     ARCHITECTURE/REFACTOR/BUGS/TESTS/TODOS shape and which do not, and why.

State the relationship to CLAUDE.md's Documentation Index explicitly — one of them should be
authoritative and the other should point at it. Recommend Docs/README.md as authoritative for Docs/
and CLAUDE.md's table as the curated "read before touching X" subset, and say so in both files.
```

---

## C-4 🟡 Root `README.md` describes a game that no longer exists

**Where:** `README.md` § Game Modes.

**Evidence.** The public-facing README lists 8 modes: *Get the Crystal, Dolphin Darts, Ransack Rally,
Freestyle Toybox, Duel for the Cell, HexRace, Wildlife Blitz, Joust*. Against the real card assets:

- **"Get the Crystal"** and **"Ransack Rally"** match no `DisplayName` in `_SO_Assets/Games/`.
- **"HexRace"** is the enum name; players see **"Skim Race"**.
- **Missing entirely:** Scurry (Crystal Capture), Astro League, Brood Rush, Rampage, Peel the Cage,
  Wildlife Liberation, Dog Fight, Maelstrom — i.e. every mode shipped in the last cycle.

The vessel table is also softer than `CLAUDE.md`'s (Serpent listed without its dedicated HUD).

**Prompt**

```
The root README.md Game Modes list is badly out of date. It names "Get the Crystal" and "Ransack
Rally" (which match no card asset), uses the internal name "HexRace" instead of the player-facing
"Skim Race", and omits every recently shipped mode.

Ground truth — the player-facing names live in the card assets:
  for f in Assets/_SO_Assets/Games/*.asset; do
    printf "%-46s %s\n" "$(basename $f .asset)" "$(grep -m1 '^  DisplayName:' $f | sed 's/^  DisplayName: //')"
  done

Rewrite the Game Modes section using DisplayName values, covering at least: Skim Race, Scurry,
Joust, Maelstrom, Astro League, Brood Rush, Rampage, Peel the Cage, Wildlife Liberation, Dog Fight,
Duel for the Cell, Wildlife Blitz, and the Freestyle Toybox. One line each, player-facing tone —
this is the public repo front page, not an engineering doc, so no class names.

Drop modes whose scene no longer exists (CLAUDE.md notes many single-player modes reference deleted
scenes) rather than advertising them.

Also align the vessel table with CLAUDE.md's: Serpent is a playable vessel with a dedicated HUD.
```

---

# D. Redundancy and duplication

## D-1 🟠 `MultiplayerArchitecture/src/content/` is a second, drifting copy

**Evidence.** 24 Markdown files under `Docs/MultiplayerArchitecture/src/content/` mirror the
canonical docs one-for-one:

| PDF source | Canonical doc |
|---|---|
| `13-presence-lobby.md` | `Docs/PresenceSystem/ARCHITECTURE.md` |
| `14-party-session.md` | `Docs/PartySystem/ARCHITECTURE.md` |
| `15-invite-flow.md` | `Docs/PartySystem/UI.md` |
| `16-spawn-pipeline.md` | `CLAUDE.md` § Player Spawning |
| `19-threading.md` | `Docs/THREADING.md` |
| `22-bug-catalog.md` | `Docs/PartySystem/BUGS.md` + `PresenceSystem/BUGS.md` |
| `24-diagnostics.md` | `Docs/NetworkDiagnostics/ARCHITECTURE.md` |
| `90-appendix-files.md` | `CLAUDE.md` Key Files tables |

They have already drifted — `90-appendix-files.md` still lists `DomainAssigner` (A-2). The README
says they are "also reusable as article text", which invites reading them as reference material, and
nothing in them says "generated view, do not trust over the canonical doc".

**Prompt**

```
Docs/MultiplayerArchitecture/src/content/*.md (24 files) is a second copy of content that lives
canonically in Docs/PartySystem/, Docs/PresenceSystem/, Docs/NetworkDiagnostics/, Docs/THREADING.md
and CLAUDE.md. It has already drifted (90-appendix-files.md still lists DomainAssigner, which does
not exist — see finding A-2).

Do NOT merge or delete them; they are the build input for a real deliverable (the PDF dossier +
LinkedIn deck). Make the relationship unambiguous instead:

  1. Add a short banner to the top of every file under src/content/:
     "> **Generated dossier source — not the canonical reference.** Synthesised from <canonical
     > doc path> as of <date>. If this disagrees with the canonical doc, the canonical doc wins."
     Fill in the correct canonical path per file (mapping in the audit finding D-1).

  2. In Docs/MultiplayerArchitecture/README.md, replace "also reusable as article text" with an
     explicit statement that these are point-in-time snapshots for publication, list the
     source->canonical mapping, and state that engineering changes go to the canonical doc first.

  3. Fix the DomainAssigner reference in 90-appendix-files.md as part of A-2.

  4. Add a "Refresh" note to the README: what to re-read before regenerating the PDF so the snapshot
     is re-synced rather than re-drifted.
```

---

## D-2 🟠 `CLAUDE.md` inlines whole sections that exist as dedicated docs

**Evidence.** `CLAUDE.md` is 2,789 lines / 292 KB — the largest doc in the repo and the one loaded
into every session's context. A large share restates content that already has a canonical home:

| `CLAUDE.md` section | Approx. lines | Canonical doc |
|---|---|---|
| Authentication & Session Flow (incl. full ASCII flow) | ~180 | `Docs/MultiplayerArchitecture/…/12-auth-bootstrap.md`, `BOOTSTRAP_AUDIT.md` |
| Party / Invite Lobby System | ~200 | `Docs/PartySystem/ARCHITECTURE.md` + `UI.md` |
| Friend System (incl. full facade API table) | ~120 | *(no canonical doc — this is the only home)* |
| HexRace Game Mode | ~110 | `Assets/_Scripts/Controller/Arcade/HEXRACE.md` |
| Lava-Lamp Mode | ~230 | `Docs/ToySystem/ARCHITECTURE.md`, `Docs/SCENES.md` |
| Player Spawning Architecture | ~150 | `Docs/MultiplayerArchitecture/…/16-spawn-pipeline.md` |
| Scene Inventory + Game Modes | ~90 | `Docs/SCENES.md` |

The mode-enum paragraph at `CLAUDE.md:423` is a single ~7,000-word paragraph covering thirteen game
modes — it is the primary reason the HexRace/Ribcage/WildlifeLiberation details drift between
`CLAUDE.md`, `SCENES.md` and the per-mode docs.

Duplication is also the *mechanism* behind findings A-1, A-5, A-6 and A-11: each of those is a table
in `CLAUDE.md` restating something with a canonical home, which was updated in one place only.

**Prompt**

```
CLAUDE.md is 2,789 lines and duplicates several sections that have canonical homes, which is the
direct cause of several drift bugs (the HUD table, the HexRace key-files table, the scene inventory).

This is a careful, high-risk edit — CLAUDE.md is the file every session loads. Do it in ONE pass per
section, with a commit per section, and do NOT touch these, which must stay inline and complete:
  - Prime Directive
  - Ecosystem Design Principles (LOCKED)
  - Design Philosophy / fundamentals
  - Anti-Patterns to Avoid
  - What Claude Code Should Never Do
  - Architecture Patterns (SOAP, threading, audio, config separation)

For each of these sections, replace the body with a 5-15 line summary plus a pointer, moving any
detail that exists ONLY in CLAUDE.md into the target doc first (never delete unique content):
  - "HexRace Game Mode"            -> Assets/_Scripts/Controller/Arcade/HEXRACE.md
  - "Party / Invite Lobby System"  -> Docs/PartySystem/ARCHITECTURE.md + UI.md
  - "Authentication & Session Flow"-> a new Docs/AuthSystem/ARCHITECTURE.md (create it; today the
                                      only full account is inline in CLAUDE.md)
  - "Friend System"                -> a new Docs/FriendSystem/ARCHITECTURE.md (same reason)
  - "Lava-Lamp Mode"               -> Docs/ToySystem/ARCHITECTURE.md
  - "Player Spawning Architecture" -> Docs/MultiplayerArchitecture is a generated snapshot, so
                                      create Docs/SpawnPipeline/ARCHITECTURE.md as canonical
  - "Scene Inventory"/"Game Modes" -> Docs/SCENES.md

Separately, split the ~7,000-word single paragraph at CLAUDE.md:423 into a table
(Mode | ID | Display name | Vessel restriction | Objective/metric | Doc), moving each mode's
narrative into its existing per-mode doc under Assets/_Scripts/Controller/Arcade/. That paragraph is
where most of the mode-detail drift lives.

Target: CLAUDE.md under ~1,200 lines with zero loss of unique content. After each section, verify no
Documentation Index row or cross-reference broke.
```

---

## D-3 🟡 `GameModes.cs` enum comments duplicate and contradict the mode docs

**Where:** `Assets/_Scripts/Data/Enums/GameModes.cs`.

**Evidence.** Two enum comments describe designs that were explicitly reverted:

| Line | Comment says | Reality |
|---|---|---|
| `:70` | WildlifeLiberation — "first **PLAYER** (not domain - this one is a **free-for-all**) to the kill target wins" | `CLAUDE.md:459`: the free-for-all winner "shipped here briefly and was **reverted**… It is an ordinary domain race… **Do not re-derive it.**" |
| `:60-64` | Ribcage — "A hollow **SHIELDED** prism sphere **pens the cell's brood**… the fauna wave hatches in the leader's colour" | `CLAUDE.md:423`: "Every bar is a one-hit **PLAIN** prism… **nothing is shielded or super-shielded. No fauna** — the mode's former leader-pinned brood ladder was removed" |

These are the highest-risk stale statements in the repo, because they sit *in the source file* an
engineer opens when adding a mode — the place most likely to be trusted without cross-checking, and
the one place `CLAUDE.md`'s "Do not re-derive it" warning cannot reach.

**Prompt**

```
Two comments in Assets/_Scripts/Data/Enums/GameModes.cs describe designs that were explicitly
reverted, and they contradict CLAUDE.md and the per-mode docs.

1. WildlifeLiberation (line ~70) says "first PLAYER (not domain - this one is a free-for-all) to the
   kill target wins". Per CLAUDE.md:459 and Assets/_Scripts/Controller/Arcade/WILDLIFE_LIBERATION.md,
   the per-player winner shipped briefly and was REVERTED; it is an ordinary domain race to 250
   summed kills, and the docs say "Do not re-derive it".

2. Ribcage (lines ~60-64) describes a hollow SHIELDED prism sphere that "pens the cell's brood" with
   a fauna wave hatching in the leader's colour. Per CLAUDE.md:423 and
   Assets/_Scripts/Controller/Arcade/RIBCAGE.md, every bar is a one-hit PLAIN prism, nothing is
   shielded, and the fauna ladder was removed.

Confirm both against the controllers (WildlifeLiberationController, RibcageController) and the
scoring rule SOs before editing — the code is the tie-breaker, not CLAUDE.md.

Then rewrite both comments to match. Keep them SHORT: enum comments should carry the ID rationale
and a doc pointer, not a design essay. Move any detail worth keeping into the per-mode .md.

Finally, audit the remaining enum comments in that file the same way — several others carry
multi-line design narrative that will drift for the same reason.
```

---

## D-4 🔵 Six byte-identical paragraph blocks duplicated across doc pairs

**Evidence.**

| Block | Duplicated in |
|---|---|
| Analytics "Provenance" note | all 4 of `event-taxonomy.md`, `implementation-plan.md`, `utm-conventions.md`, `viability-report.md` |
| App-state SOAP transition list | `BOOTSTRAP_AUDIT.md`, `CLAUDE.md` |
| `FrogletToolShipContext` snippet | `.claude/skills/ship-tools/SKILL.md`, `Docs/TOOLING.md` |
| NetDiag catch-block pattern | `NetworkDiagnostics/ARCHITECTURE.md`, `NetworkDiagnostics/TODOS.md` |
| Ready-button flow diagram | `CRYSTAL_CAPTURE.md`, `JOUST.md` |
| Sparrow gun-position table | `DOGFIGHT.md`, `SPARROW_TURRET_STANCE.md` |

Low harm today, but each is a future divergence — the Sparrow gun-position table is exactly the kind
that caused the muzzle-transform bug `DOGFIGHT.md` documents.

**Prompt**

```
Six paragraph blocks are byte-identical across doc pairs. For each, keep ONE canonical copy and
replace the other with a pointer (or, where the duplicate genuinely aids reading in place, keep it
but add "canonical copy: <path>" so a future editor knows to update both):

  1. Analytics "Provenance" note — duplicated in all four of Docs/Analytics/{event-taxonomy,
     implementation-plan,utm-conventions,viability-report}.md. Move it once into
     Docs/Analytics/ANALYTICS_HANDOFF.md (or a new Docs/Analytics/README.md) and link.
  2. App-state SOAP transition list — canonical in Assets/_Scripts/System/Bootstrap/BOOTSTRAP_AUDIT.md;
     CLAUDE.md should point at it. (Fold into finding D-2 if that is being done.)
  3. FrogletToolShipContext snippet — canonical in Docs/TOOLING.md; the skill should link.
  4. NetDiag catch-block pattern — canonical in NetworkDiagnostics/ARCHITECTURE.md; TODOS.md links.
  5. Ready-button flow — canonical in the shared controller doc; CRYSTAL_CAPTURE.md and JOUST.md
     both link (it is base-class behaviour, not per-mode).
  6. Sparrow gun-position table — canonical in SPARROW_TURRET_STANCE.md; DOGFIGHT.md links. This one
     matters most: DOGFIGHT.md itself records a bug caused by a drifted muzzle transform.

Re-run the detector afterwards to confirm:
  (the duplicate-paragraph script from this audit, threshold 180 chars)
```

---

# E. Lifecycle: ephemera parked in a reference tree

## E-1 🟠 Eight session-scoped docs sit unmarked beside canonical references

**Evidence.** These are kickoff briefs, handoffs and work logs — valuable as history, but shelved in
`Docs/` with no status marker, and every one cites a branch that no longer exists:

| Doc | Cited branch | Branch alive? |
|---|---|---|
| `ECOSYSTEM_PHASE2_KICKOFF.md` | `claude/keen-newton-OwUKo` | ❌ gone |
| `ECOSYSTEM_OVERNIGHT_LOG.md` | `claude/quirky-meitner-bcos8u` | ❌ gone |
| `DENSITY_PARTITIONING_AUDIT.md` | `claude/audit-density-partitioning-2EvgR` | ❌ gone |
| `DENSITY_PARTITIONING_HANDOFF.md` | `claude/audit-density-partitioning-2EvgR` | ❌ gone |
| `PRISM_ECS_MIGRATION.md` | `claude/ecs-migration-guide-Db42i` | ❌ gone |
| `ECOSYSTEM_PLATFORM_KICKOFF.md` | — (paste-in brief) | n/a |
| `PRISM_CLOCK_FOLLOWUP_PROMPTS.md` | `claude/prism-animation-audit-*` | ❌ gone |
| `TournamentSystem/MAELSTROM_UX_HANDOFF.md` | — | n/a |

`ECOSYSTEM_OVERNIGHT_LOG.md` opens "Running autonomously while you're away (~8h)" in the present
tense; `DENSITY_PARTITIONING_HANDOFF.md` says "Two issues remain open at this handoff" with no
indication whether they were ever closed. Both read as live.

**Prompt**

```
Eight docs under Docs/ are session-scoped ephemera (kickoff briefs, handoffs, overnight logs) shelved
next to canonical references with no status marker. Every branch they cite is deleted:

  ECOSYSTEM_PHASE2_KICKOFF.md              claude/keen-newton-OwUKo
  ECOSYSTEM_OVERNIGHT_LOG.md               claude/quirky-meitner-bcos8u
  ECOSYSTEM_PLATFORM_KICKOFF.md            (paste-in brief)
  DENSITY_PARTITIONING_AUDIT.md            claude/audit-density-partitioning-2EvgR
  DENSITY_PARTITIONING_HANDOFF.md          claude/audit-density-partitioning-2EvgR
  PRISM_ECS_MIGRATION.md                   claude/ecs-migration-guide-Db42i
  PRISM_CLOCK_FOLLOWUP_PROMPTS.md          claude/prism-animation-audit-*
  TournamentSystem/MAELSTROM_UX_HANDOFF.md

For each:
  1. Determine whether its work landed. Use `git log --all --oneline --grep=<keyword>` and check
     whether the classes/behaviour it describes exist now.
  2. Add a status banner at the top:
     "> **Archived session document** (<date>). Work status: SHIPPED | PARTIALLY SHIPPED | ABANDONED.
     >  Branch `<name>` no longer exists. Canonical reference: <path>. Kept for rationale/history."
  3. Extract any still-true, still-unique content into the canonical doc BEFORE archiving
     (DENSITY_PARTITIONING_AUDIT.md in particular holds benchmark findings that likely belong in
     Docs/SPATIAL_INDEX.md or Docs/PERFORMANCE_OPTIMIZATION.md — check before assuming).
  4. Move them to Docs/Archive/ preserving relative structure, and add one "Archive" line to
     Docs/README.md explaining what lives there and that it is history, not guidance.

Two need explicit resolution rather than just a banner:
  - DENSITY_PARTITIONING_HANDOFF.md says "Two issues remain open" (menu cell never reaching Quiet
    phase, static spawn-cycle ring). Determine whether they are still open; if so, move them to
    Docs/ECOSYSTEM.md or a live BUGS file rather than leaving them in an archived handoff.
  - PRISM_CLOCK_FOLLOWUP_PROMPTS.md is a live work queue, not history. If items remain, keep it out
    of Archive and re-title it as a backlog; if all are done, archive it.
```

---

## E-2 🟠 `PLAN.md` is a dead scratch plan at the repo root

**Evidence.** `PLAN.md` sits beside `README.md`, `CLAUDE.md` and `GIT_RULES.md` — where a reader
expects authoritative material — and is a single-feature implementation plan
("Refactor Shape Spawning into Freestyle SegmentSpawner Pipeline"). It specifies creating files
under `Assets/_Scripts/Game/Environment/MiniGameObjects/`, a path that does not exist (the real
location is `Controller/Environment/MiniGameObjects/`), and its premise — a lobby-based shape
selection flow with `ModeSelectTrigger` / `ShapeSign` — describes the pre-lava-lamp era that
`Docs/SCENES.md` says was removed. It contains no date, status, or owner.

**Prompt**

```
PLAN.md at the repo root is a stale single-feature implementation plan ("Refactor Shape Spawning
into Freestyle SegmentSpawner Pipeline"). It specifies file paths under
Assets/_Scripts/Game/Environment/MiniGameObjects/ (that directory does not exist — the real one is
Controller/Environment/MiniGameObjects/), and its premise (lobby-based shape selection via
ModeSelectTrigger / ShapeSign) describes the pre-lava-lamp flow that Docs/SCENES.md says was removed.

Determine its status: do SpawnableShapeBase, ShapeCollisionTrigger, SpawnableStar, SpawnableCircle,
SpawnableLightning, SpawnableSmiley exist? Does ShapeDrawingManager still work the way the plan
assumes?

Then EITHER:
  (a) if the work is still wanted — rewrite it against the real tree (correct paths, current
      lava-lamp/freestyle model per CLAUDE.md "Lava-Lamp Mode" Phase 2) and move it to
      Docs/ToySystem/BACKLOG.md, which is where freestyle follow-up work already lives; or
  (b) if it is dead — move it to Docs/Archive/ with the E-1 status banner.

Either way, remove PLAN.md from the repo root. A root-level file with no date, status, or owner
sitting next to README.md and CLAUDE.md reads as authoritative, and this one is not.
```

---

## E-3 🟡 The verification checklist has never been closed out

**Where:** `Docs/UNITY_VERIFICATION_CHECKLIST.md` — **14 🔴, 0 🟡, 0 🟢**.

The doc defines a three-state workflow ("tick what you confirm, and delete (or move to 'Verified')
what holds up") and a `🔴/🟡/🟢` legend, but no entry has ever moved off 🔴 and there is no "Verified"
section to move things to. Either 14 change sets are genuinely unverified — in which case that is the
project's most important open risk and should be surfaced far more loudly — or entries were verified
and never marked, in which case the doc's signal is zero. Both readings are bad, and the doc gives no
way to tell them apart.

**Prompt**

```
Docs/UNITY_VERIFICATION_CHECKLIST.md contains 14 entries marked 🔴 unverified and zero marked 🟡 or
🟢. The doc defines a workflow ("tick what you confirm, delete or move to 'Verified'") and a
three-state legend, but nothing has ever moved off 🔴 and there is no "Verified" section.

Do not guess at verification status — that requires the Unity Editor. Instead make the doc usable:

  1. Add the missing "## Verified" section the workflow refers to, with the intended format.
  2. Add a dated header line to each existing entry ("Landed: <date>, branch <name>") so age is
     visible. Recover dates from git log for the referenced branches/commits where possible.
  3. Add a summary table at the top: entry | area | landed | status. 14 items buried in 979 lines of
     prose is why none of them gets worked.
  4. Sort entries so the ones gating LOCKED systems (prism animation, ecology, shield morphs) are
     first, since those carry the most risk if wrong.
  5. Add an explicit note to the "How to use it" section: if an entry cannot be verified because the
     change has since been superseded, move it to Verified with the reason rather than leaving it
     🔴 forever.

Then raise with the human whether the 14 open items are real. This is the single largest cluster of
unverified risk in the documentation and it is currently invisible outside this one file.
```

---

# F. Ambiguity and missing definitions

## F-1 🔴 Player-facing mode names are used as if defined, but defined nowhere

**Evidence.** No doc holds the enum-name → display-name mapping, yet display names are used as
primary terms throughout. From the card assets:

| Enum / scene | Player-facing `DisplayName` | Documented anywhere? |
|---|---|---|
| `HexRace` (33) | **Skim Race** | ❌ — used in `PALETTE.md:463`, `GAMECANVAS.md:42`, `PRISM_ECS_MIGRATION.md`, `PartySystem/BUGS.md:706` with no definition |
| `MultiplayerCrystalCapture` (35) | **Scurry** | ❌ — `CLAUDE.md` says "Scurry's destructive analog", "Scurry Cell Config", "Scurry intensity 4 Atlantis" and never defines it |
| `MultiplayerCellularDuel` (29) | **Online Duel for the Cell** | ❌ |
| `Tournament` (36) | **Maelstrom** | ✅ (`ShuffleSystem/`, but see B-1) |
| `NucleusRush` (38) | **Brood Rush** | ✅ (`CLAUDE.md`) |
| `Ribcage` (39) | **Peel the Cage** | ✅ (`CLAUDE.md`) |

"Scurry" is the worst case: `CLAUDE.md` uses it as an established term in a LOCKED section
(`Scurry Cell Config` → `HalfNucleus.prefab` is a worked example of the Cell-owned-visuals rule)
without ever saying it means Crystal Capture. A reader searching `Scurry` in the codebase finds an
asset name and no explanation.

**Prompt**

```
The enum-name -> player-facing-name mapping for game modes is documented nowhere, yet display names
are used as primary terms across the docs. "Scurry" appears in CLAUDE.md's LOCKED ecology section
("Scurry Cell Config", "Scurry's destructive analog", "Scurry intensity 4 Atlantis") and is never
defined — it is MultiplayerCrystalCapture (35). "Skim Race" appears in PALETTE.md, GAMECANVAS.md,
PRISM_ECS_MIGRATION.md and PartySystem/BUGS.md and is never defined — it is HexRace (33).

Build the mapping from the assets:
  for f in Assets/_SO_Assets/Games/*.asset; do
    printf "%-46s %s\n" "$(basename $f .asset)" \
      "$(grep -m1 '^  DisplayName:' $f | sed 's/^  DisplayName: //')"
  done

Then:
  1. Add a "Mode names" table to Docs/SCENES.md as the single source:
     Enum (ID) | DisplayName (what players see) | Scene | Controller | Doc
     Include the cards with an empty DisplayName and flag them.
  2. Add a one-line pointer to that table from CLAUDE.md's game-modes section.
  3. On first use in each doc, disambiguate: "Scurry (Crystal Capture)", "Skim Race (HexRace)".
     Do a repo-wide pass — grep -rn "Scurry\|Skim Race" --include=*.md .
  4. State the convention explicitly in SCENES.md: code/enums/scenes use the internal name, UI and
     player-facing copy use DisplayName, and docs should give both on first mention.

Note that several cards have no DisplayName at all (Multipass, ObstacleCourse, PumpNDump, Sidewinder,
Soar). Record that in the table rather than silently omitting them — if they render blank in the
Arcade UI that is a bug worth surfacing to the human.
```

---

## F-2 🟠 Code says `Block`, docs say `Prism`, nothing says they are the same

**Evidence.** `Prism` is the documented fundamental — `CLAUDE.md` § "Prisms / Prismscapes",
`Docs/PRISM_ANIMATION.md`, `Docs/SPATIAL_INDEX.md`. But the code is full of `Block`:

```
119 GyroidBlockType   81 BlockType   56 Block        49 trailBlock
 37 RemoveBlock       35 AddBlock    33 CreateBlock  27 BlockGraph   21 LiveBlockCount
```

plus `BlueBlock.prefab`, `ExplodingBlockGraph`, `*BlockColor` fields in `SO_ColorSet` (which
`Docs/PALETTE.md` tells readers to edit), and `Cell.AddBlock` / `RemoveBlock` (which
`Docs/ECOSYSTEM.md` cites as the mass sinks). **Zero docs explain the equivalence.**

A reader told "never edit any `*BlockColor` field without reading `PALETTE.md`" has to already know
that a BlockColor is a prism colour. Someone grepping for `AddPrism` after reading the ecology docs
finds nothing.

**Prompt**

```
The codebase uses "Block" where all documentation says "Prism" — AddBlock, RemoveBlock,
LiveBlockCount, trailBlock, BlockType, GyroidBlockType, BlockGraph, ExplodingBlockGraph,
BlueBlock.prefab, and the *BlockColor fields in SO_ColorSet. No doc anywhere states the two terms
refer to the same thing.

This is a documentation fix, not a rename. Do NOT rename any code identifier.

  1. Add a short "Prism and Block are the same thing" note to CLAUDE.md's "Prisms / Prismscapes"
     fundamental: Prism is the canonical design term; Block is the legacy identifier still used
     throughout the code and asset names; list the main surviving identifiers so a reader can grep
     either way.
  2. Repeat a one-line version at the top of Docs/PRISM_ANIMATION.md, Docs/SPATIAL_INDEX.md and
     Docs/ECOSYSTEM.md, since each cites Block-named APIs as if the reader already knows.
  3. In Docs/PALETTE.md, state it explicitly where *BlockColor is introduced — that doc instructs
     readers to edit those fields and never says they are prism colours.

Then check whether a code rename is worth proposing separately. Do not start one here; flag it as a
question for the human, with a count of affected identifiers.
```

---

## F-3 🟠 The "tests live under `Editor/`" rule contradicts its own table

**Where:** `CLAUDE.md` § "**Tests live under an `Editor/` folder, never in an asmdef.**"

**Evidence.** The rule is stated absolutely — "Every first-party test is under a folder literally
named `Editor`" and "**Do not 'fix' this by authoring test `.asmdef`s.**" — and is backed by a real
IL2CPP build failure. But the table immediately below lists:

| PlayFab tests | `_Scripts/System/Playfab/PlayFabTests/` (has its own `.asmdef`) |

`PlayFabCatalogTests.cs` is the one test file **not** under an `Editor/` folder. It is in fact safe —
`CosmicShore.PlayFabTests.asmdef` sets `"includePlatforms": ["Editor"]` and
`"defineConstraints": ["UNITY_INCLUDE_TESTS"]`, so NUnit never reaches the linker — but the doc never
says why the exception is safe. A reader following the absolute rule would either "fix" a working
setup or conclude the rule is unreliable.

**Prompt**

```
CLAUDE.md's "Tests live under an Editor/ folder, never in an asmdef" section states an absolute rule
("Every first-party test is under a folder literally named Editor", "Do not fix this by authoring
test .asmdef's") and then lists an exception in its own table without explaining it: the PlayFab
tests are at _Scripts/System/Playfab/PlayFabTests/ with their own .asmdef, not under Editor/.

Read Assets/_Scripts/System/Playfab/PlayFabTests/CosmicShore.PlayFabTests.asmdef. It is safe because
it sets "includePlatforms": ["Editor"] and "defineConstraints": ["UNITY_INCLUDE_TESTS"], so NUnit
never reaches the IL2CPP linker.

Rewrite the section so the rule and its exception are both stated precisely:
  - the real invariant is "no test assembly may be included in a player build" — Editor/ folders and
    an Editor-only asmdef are two valid ways to satisfy it
  - Editor/ is the DEFAULT because it also implicitly references Assembly-CSharp, which asmdef-based
    tests cannot do, and all gameplay code lives there
  - the asmdef route is valid ONLY when the tests do not need gameplay types (the PlayFab case) AND
    the asmdef is Editor-only with the UNITY_INCLUDE_TESTS constraint — quote both settings
  - keep the IL2CPP failure story (error IL1005 / nunit.framework), it is the reason the rule exists

State plainly which route a new test should take and when the exception applies, so the guidance is
followable without reading the asmdef.
```

---

## F-4 🟡 No repo-wide glossary

**Evidence.** The only glossary is `Docs/MultiplayerArchitecture/src/content/91-appendix-glossary.md`
(33 lines) — a PDF build input (D-1), multiplayer-scoped, and orphaned from every index (C-1).

Terms used as established across the docs with no central definition: *domain* (vs "colour"),
*cell* (vs "biome"), *prism* / *block* (F-2), *HyperSea*, *elemental* / *element level* / *overcharge
band*, *skim* / *skimmer*, *danger prism* / *shielded* / *super-shielded*, *lava lamp* vs *freestyle*,
*toy*, *heart*, *wither*, *joust*, *nucleus* / *control zone*, *volume* vs *count*, *Scurry* /
*Skim Race* (F-1), *VP* (virtual player), *MPPM*, *SOAP*, *golf scoring*.

Several are defined in passing somewhere — `SCENES.md` defines lava lamp vs freestyle, `CLAUDE.md`
defines domain vs colour and cell vs biome — but a reader has to already know which doc to open.

**Prompt**

```
Create Docs/GLOSSARY.md as the repo-wide canonical term list. There is currently no such doc; the
only glossary is Docs/MultiplayerArchitecture/src/content/91-appendix-glossary.md, which is a PDF
build input, multiplayer-scoped, and unreachable from any index.

Do NOT invent definitions. Harvest each from where it is already defined and cite that doc as the
deep reference. Where a term has no definition anywhere, mark it "(undefined — needs an owner)"
rather than writing one.

Cover at least: domain (and why "colour" is the non-canonical synonym), cell (vs "biome"),
prism / block (see finding F-2), prismscape, HyperSea, mass, elemental / element level / the
[-5,15] band / overcharge, skim / skimmer, danger prism, shielded / super-shielded, nucleus /
control zone, volume vs count, heart, wither, joust, lava lamp vs freestyle, toy, vessel, captain,
intensity, golf scoring, VP, MPPM, SOAP, the clock-material law, the speed tunnel, the occlusion
corridor.

Format: Term | Canonical meaning (1-2 lines) | Non-canonical synonyms to avoid | Deep reference.

Fold in the multiplayer terms from 91-appendix-glossary.md so the PDF appendix can be regenerated
from this file instead of maintaining its own list.

Link it from CLAUDE.md's Documentation Index and Docs/README.md, and reference it from the top of
Docs/ECOSYSTEM.md (which uses the most specialised vocabulary).
```

---

# G. Structural and process

## G-1 🟡 No status, ownership, or freshness convention

**Evidence.** Doc headers are inconsistent: `SCENES.md` has "Last updated June 2026" (and is stale);
`DENSITY_PARTITIONING_AUDIT.md` has a rich **Status/Branch/Companion artifacts/Acceptance criteria**
block; most docs have nothing. Git dates cannot substitute — history is squashed, so
`git log` reports the same date for nearly every file:

```
2026-08-07  Docs/Analytics/DATA_ARCHITECTURE.md
2026-08-07  Docs/BRANCHING_AND_RELEASE.md
2026-08-07  Assets/_Scripts/Tests/UNIT_TESTING_GUIDE.md   ... (identical for ~140 files)
```

So a reader has no way to tell a live reference from an archived brief (E-1) without reading it.

**Prompt**

```
There is no convention for doc status, ownership, or freshness, and git dates are useless here
(history is squashed — nearly every doc reports the same commit date).

  1. Define a standard front-matter block in Docs/README.md under a new "Doc conventions" section:

     > **Status:** Canonical | Living | Archived | Generated
     > **Covers:** <system/area>   **Updated:** <YYYY-MM-DD>   **Verify against:** <code paths>

     Semantics: Canonical = the single source for its area; Living = a tracker that changes as work
     lands (BUGS/TODOS/REFACTOR/CHANGELOG); Archived = history, do not act on it (finding E-1);
     Generated = built from another source, never edit directly (finding D-1).

  2. Apply it to every doc under Docs/ and the per-mode/per-ability docs under Assets/_Scripts/.
     Determine "Updated" from the newest concrete fact in each doc (a dated entry, a referenced
     commit) rather than from git. Where undeterminable, write "Updated: unknown".

  3. "Verify against" is the load-bearing field: name the code paths a reader should check when the
     doc looks wrong. Most findings in this audit would have been caught in seconds with it.

  4. Adopt it in the templates the skills use so new docs are born with it.

Do this AFTER findings A-*, B-* and E-* so the dates you stamp are honest.
```

---

## G-2 🟡 The "shared conventions" contract covers 4 of 7 subsystem folders

**Where:** `Docs/README.md:65–71` — "The `PartySystem/`, `PresenceSystem/`, `NetworkDiagnostics/`,
and `ScoringSystem/` folders share a consistent shape…" and, in the next section, "These apply
across all three folders" (which then lists four — an internal miscount).

Subsystem folders now number seven: the four above plus `TournamentSystem/`, `ShuffleSystem/`,
`ElementalAbilitySystem/`, `ToySystem/`, `SettingsSystem/`, `EnvironmentSpawning/` — none of which
follows the ARCHITECTURE/REFACTOR/BUGS/TESTS/TODOS shape, and none of which is said to be exempt.

**Prompt**

```
Docs/README.md's "shared conventions" section is inconsistent and out of date:
  - line ~65 names four folders as sharing the ARCHITECTURE/REFACTOR/BUGS/TESTS/TODOS shape
  - line ~95 says "These apply across all three folders" and then lists four (internal miscount)
  - seven-plus subsystem folders now exist (TournamentSystem, ShuffleSystem, ElementalAbilitySystem,
    ToySystem, SettingsSystem, EnvironmentSpawning) and none is mentioned

Fix the miscount, then add a table listing every subsystem folder under Docs/ with which of the five
standard files it has and whether the gaps are deliberate:

  Folder | ARCHITECTURE | REFACTOR | BUGS | TESTS | TODOS | Notes

State the actual rule — most likely "the five-file shape applies to actively-refactored networked
subsystems; feature-area folders need only ARCHITECTURE" — and say so, so nobody creates four empty
files for a new folder or assumes a missing BUGS.md means zero bugs.

Verify each folder's contents with `ls Docs/*/` before writing the table.
```

---

## G-3 🔵 No automated doc-drift gate

**Evidence.** Every check in this audit is scriptable and none of it runs. The project already has
the pattern — `Tools/Build/check_conditional_compilation.py` is a fast, Unity-free CI gate that
`CLAUDE.md` mandates before committing guarded scripts. Findings A-1, A-2, A-5, A-6, A-12 and C-1
would all have been caught by an equivalent for docs.

**Prompt**

```
Add Tools/Build/check_doc_references.py — a fast, Unity-free doc-drift gate modelled on the existing
Tools/Build/check_conditional_compilation.py (read it first and match its CLI, exit codes, and
output style).

Checks, each independently toggleable and each reporting file:line:

  1. Dangling file paths — every `path/to/file.ext` in backticks resolves to a real file. Support an
     allowlist file for deliberate historical references (see finding A-12).
  2. Dangling C# symbols — every backticked PascalCase identifier that looks like a type resolves to
     a class/struct/interface/enum declaration. Needs an allowlist for Unity/UGS/third-party types
     (NetworkManager, SceneManager, ParticleSystem, ...) and for method names.
  3. Orphan docs — every .md is reachable from CLAUDE.md, Docs/README.md, README.md, or an explicit
     exempt list (finding C-1).
  4. Dangling section anchors — every `<DOC>.md §N` citation resolves to a real numbered header.
  5. Duplicate paragraphs — blocks >=180 chars appearing in more than one doc (finding D-4).

Exit non-zero on 1, 3 and 4; warn on 2 and 5 (both have false positives).

Seed the allowlists from the current state so the gate starts green, and add a
Tools/Build/doc_allowlist.txt with a comment explaining that each entry is a deliberate exception.

Document it in Docs/TOOLING.md and add it to CLAUDE.md's pre-commit guidance next to
check_conditional_compilation.py.
```

---

## Suggested order

Correctness first — those are the findings that make someone write wrong code:

1. **A-2, A-1, A-3** — the ghost class, the wrong HUD table, the superseded ecology spine.
2. **D-3, F-1** — the reverted designs living in `GameModes.cs`, and the undefined mode names.
3. **B-1, A-4** — the display-name contradiction and the reverted Sparrow upgrade.
4. **A-5 – A-12** — the remaining code/doc drift, mechanical once the pattern is set.
5. **C-1, C-2, C-3** — make everything reachable before reorganising anything.
6. **E-1, E-2, E-3** — separate history from guidance.
7. **F-2, F-3, F-4, G-1, G-2** — vocabulary and conventions.
8. **D-1, D-2** — the structural de-duplication. Last, deliberately: `CLAUDE.md` is loaded into
   every session, so it should be split only once the facts in it are correct.
9. **G-3** — the gate, so this audit does not need repeating by hand.

**One caveat on D-2.** It is the highest-value item for long-term maintenance and the highest-risk
single edit in the list. Splitting `CLAUDE.md` should be its own branch with its own review, and the
LOCKED sections (Prime Directive, Ecosystem Design Principles, Design Philosophy, Anti-Patterns,
Never-Do) must stay inline and complete regardless of length — they are the sections whose whole
value is that no pointer has to be followed to reach them.
