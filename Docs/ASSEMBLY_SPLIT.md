# Splitting the single-assembly monolith

Every first-party runtime script in this project compiles into one predefined assembly,
`Assembly-CSharp`. That has three costs, and they compound:

1. **Every one-line edit recompiles everything.** 1,481 files and ~235,000 lines rebuild
   because a tooltip changed, and the domain reload that follows throws away every static
   field in the project.
2. **Nothing enforces a dependency direction.** `Utility` is free to reach into `Cell`,
   `Prism` and `Flora`, and it does (see § Phase 2). A layer that can be called *by* anything
   and can call *anything* is not a layer.
3. **Tests are forced into `Editor/` folders as a workaround.** An asmdef cannot reference
   `Assembly-CSharp`, so a test asmdef would be blind to every gameplay type. The workaround
   is documented and correct — but it is a symptom, not a design (see § Tests).

This document records the split: how it is measured, what has been extracted, and what the
next layer would cost. **The order is forced and non-negotiable — see § The one-way rule.**

---

## The one-way rule

> A predefined assembly (`Assembly-CSharp`, `Assembly-CSharp-Editor`) **automatically
> references every auto-referenced asmdef.** An asmdef references **nothing** unless it says
> so, and can never reference a predefined assembly at all.

That asymmetry is the whole method:

- **Moving code OUT of `Assembly-CSharp` into an asmdef is transparent.** Everything left
  behind keeps seeing the extracted types with no edit anywhere — no `using` change, no
  reference wiring, no big-bang. This is why extraction can proceed one leaf at a time.
- **Moving code out that still needs to reach BACK into `Assembly-CSharp` is impossible.**
  There is no reference that can express it.

So extraction only ever works **bottom-up from the leaves**, and the forcing function is
built into the compiler rather than into a review checklist: a layer that still depends on
gameplay simply will not extract, and the failure is a compile error rather than a slow
erosion.

Two corollaries worth stating because they are the things that actually bite:

- **A namespace may span assemblies; a *type* may not.** Renaming is what breaks scene and
  prefab references — a `.meta` GUID move does not. Never rename a class while relocating it.
- **`internal`, `partial` and extension methods do not survive the boundary.** `internal`
  becomes invisible, a `partial` type cannot be split across two assemblies, and an extension
  method is only found if its declaring namespace is `using`-ed. Check all three before
  drawing a boundary; the phase-1 commit verified each explicitly.

- **Each new assembly needs its own `InternalsVisibleTo` grant if its internals are tested.**
  `Assets/_Scripts/AssemblyInfo.cs` carries `[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]`,
  which is what lets the edit-mode tests reach `Prism.SpatialIndexId` and friends. That attribute
  applies to **`Assembly-CSharp` only** — an extracted assembly does not inherit it, so a layer that
  has `internal` members reached by tests needs its own `AssemblyInfo.cs` inside the new assembly's
  folder. `CosmicShore.Data` declares no `internal` members, so phase 1 needed no grant; do not
  assume that of the next layer.

---

## Measuring

The split is paid for in edit-cycle seconds, so the seconds have to be measured the same way
before and after rather than asserted. **FrogletTools ▸ Diagnostics ▸ Compile Timing** — the third
tab of the Diagnostics window (`CompileTimingMonitor`, `Docs/DIAGNOSTICS.md`) — records them.

It is off by default and writes only to the gitignored `Logs/CompileTiming/compile-timing.csv`.

**Protocol** — run it identically on both sides of a change, on one machine, with the editor
already warm:

1. Open the project, let all imports settle, enter no play mode.
2. Press **Start** on that tab.
3. Make **the same one-line edit** — a whitespace line in a file inside the assembly under
   test — save, and wait for the reload to finish. Repeat **5 times**.
4. Read the **median** total (the tab prints it). The first compile of a session and any
   compile that races a background import are outliers large enough to swamp a mean over five
   samples, which is why the tool reports a median.
5. Press **Stop**.

Record the *rebuild set* as well as the seconds. The tool logs which assemblies Unity actually
rebuilt, because that is the quantity extraction moves: an edit inside `Assembly-CSharp`
rebuilds `Assembly-CSharp` no matter how the project is arranged, but an edit inside an
extracted leaf should rebuild only that leaf and its dependents.

### Structural baseline (measured 2026-08-21)

These are counted from the tree and are exact. They are the *compile surface* — the input the
seconds are a function of — not a substitute for the seconds.

| | files | lines |
|---|---:|---:|
| First-party C# total | 1,702 | 280,895 |
| …of which in `Editor/` folders (`Assembly-CSharp-Editor`) | 175 | 43,193 |
| **`Assembly-CSharp` before phase 1** | **1,527** | **237,702** |
| `CosmicShore.Data` (extracted, phase 1) | 46 | 2,712 |
| **`Assembly-CSharp` after phase 1** | **1,481** | **234,990** |

Phase 1 moves **3.0% of the files** (1.1% of the lines - enums are short) out of the
runtime compile surface. That is deliberately small: the
value of the first extraction is that it proves the mechanism and establishes the direction,
not that it wins back seconds. The seconds arrive when the layers in § Phase 2 follow it out.

### Wall-clock (to be filled in on a developer machine)

Not measurable in the CI/agent container — it has no Unity install and no `Library/`, so
there is no compile to time. **This table is deliberately empty rather than estimated.**

| | compile s | reload s | total s | assemblies rebuilt |
|---|---:|---:|---:|---:|
| Before (edit in `Assembly-CSharp`) | | | | |
| After (edit in `Assembly-CSharp`) | | | | |
| After (edit in `CosmicShore.Data`) | | | | |

The third row is the interesting one, and note what it will and will not show. `Data` is a
leaf that everything depends on, so an edit *inside it* still rebuilds its dependents —
`Assembly-CSharp` included — and will look roughly like the other two. That is expected and
is not a failure: the win from a leaf is that edits **elsewhere** stop rebuilding it. The row
that gets faster is a future one, once a *dependent* layer is extracted and can be edited
without dragging its siblings.

---

## Phase 1 (shipped)

### `CosmicShore.Data` — `Assets/_Scripts/Data/`

46 files: the enums, structs and small interfaces that everything reads and that read nothing
back. Package references: `Unity.Netcode.Runtime`, `Unity.Collections`. `autoReferenced: true`,
so the 1,481 files still in `Assembly-CSharp` — and the edit-mode tests in
`Assembly-CSharp-Editor` — see it with no change.

**One file was relocated to make the folder a true leaf.** `Captain.cs` reads `SO_Captain`,
`SO_Vessel` and `SO_Element`, so it moved to `Assets/_Scripts/ScriptableObjects/Captain.cs`
(`git mv` of the file *and* its `.meta`, so the GUID and every serialized reference survive).
It keeps its name and its `CosmicShore.Data` namespace — a namespace may span assemblies, and
the rename is what would have broken references.

Verified before the boundary was drawn: no `internal` members, no `partial` types, no
extension-method use crossing it; no Editor-only code in the folder; and the one other
first-party asmdef (`CosmicShore.PlayFabTests`) uses no `Data` type.

**Known wart, not fixed here:** `RoundStats.cs` is a 900-line `NetworkBehaviour` filed under
`Data/Enums/`. It is the only reason this assembly references Netcode at all. Relocating it is
a phase-2 item — moving it in phase 1 would have been a refactor riding along with a build
change, and those are the commits nobody can bisect.

---

## Phase 2 (not started — this is where it stops)

**Phase 1 stopped here deliberately.** The next two candidate layers both drag gameplay code
with them, so neither extracts without a refactor first. Forcing either would mean dragging
`Cell`, `Prism`, `Flora` and `PrismSpatialIndex` into a "leaf" assembly, which is how a split
turns into a rewrite.

### `CosmicShore.Utility` — blocked, 46 of 176 files dirty

| subfolder | files | dirty | blocked by |
|---|---:|---:|---|
| `PerformanceBenchmark` | 35 | 11 | `PrismSpatialIndex`, `Fauna`, `Flora` |
| `DataContainers` | 25 | 10 | `Flora`, `FloraSiteKind`, `ElementalCrystalSetSO` |
| *(root files)* | 54 | 9 | `SpawnableBase`, `SegmentSpawner`, `Skimmer`, `Player`, `CameraManager` |
| `Effects` | 12 | 5 | `PrismRenderService`, `PrismTimerManager`, `PrismFactory` |
| `Tools` | 14 | 5 | `ExplosionImpactor`, `PrismSpatialIndex`, `AOEExplosion` |
| `ChoppingBlock` | 2 | 1 | `Cell` |
| `DataPersistence`, `DisplayName`, `Email`, `PoolsAndBuffers`, `Recording` | 9 | 5 | one type each |
| **clean today** — `ClassExtensions`, `Network`, `Interactive`, `Internal`, `Reporting`, `SOAP`, `ScreenShots` | **24** | **0** | — |

"Utility" is not one layer, it is at least three wearing one folder name: genuinely
dependency-free helpers (24 files, extractable today), gameplay-coupled data containers
(`GameDataSO`, `CellConfigDataSO`, `CellRuntimeDataSO`), and prism/ecology systems that are
gameplay in everything but filing (`PrismExplosion`, `PrismDebris`, `PrismOcclusionCorridor`,
`VesselSpeedTunnel`). The 24 clean files could be carved out immediately, but where that
boundary belongs is a phase-2 design question, not something to answer by grabbing the
subfolders that happen to compile.

### `CosmicShore.SOAP` — blocked, but cheaply

19 of 63 files dirty, and the blockers are unusually concentrated. **Five type declarations
block the entire layer:**

| blocker | declared in | blocks |
|---|---|---|
| `CrystalStats`, `PrismStats`, `CombatHitStats`, `AbilityStats` | `Controller/Managers/StatsManager.cs` (4 plain structs, lines 21–66) | 8 files |
| `GameplaySFXCategory` | `System/Audio/AudioSystem.cs` (1 enum, line 58) | 2 files |

All five are payload types with no behaviour, sitting in files that are otherwise systems.
Moving them into `CosmicShore.Data` is a small, mechanical, independently-reviewable commit —
and it takes the SOAP layer from 19 dirty files to about 9.

The rest are real coupling and need real decisions: `IVessel` / `IVesselStatus` /
`VesselImpactor` (`ScriptableClassType`), and `MiniGameHUD` / `ShipHUD`
(`ScriptableVesselHUDData`) — a SOAP channel that carries a *HUD MonoBehaviour* as its payload
is the coupling, and re-pointing it at an interface is a design change, not a move.

### Suggested phase-2 order

1. Move the five payload types above into `CosmicShore.Data`. Mechanical.
2. Relocate `RoundStats.cs` out of `Data/` (it is a `NetworkBehaviour`, not data).
3. Extract `CosmicShore.SOAP`, minus the vessel/HUD channels.
4. Decide where `Utility` actually divides, then extract the dependency-free part.
5. Only then: `Assembly-CSharp-Editor` gets split, and tests get real asmdefs (§ Tests).

---

## Tests

Tests still live under `Editor/` folders and **that is still correct today** — see the
"Assembly Definitions" section of `CLAUDE.md` for why (an asmdef cannot see
`Assembly-CSharp`, and a test that touches a gameplay type therefore cannot live in one).

What changes as the split proceeds: a test that touches **only extracted assemblies** can
have a real test asmdef, referencing those assemblies plus the test-runner assemblies. That
is a phase-2 win and should be taken per-suite as its dependencies come out, never as a
project-wide flip. `CosmicShore.PlayFabTests` is already this shape and shows the pattern.

---

## Not enforced automatically

The repo-root `.editorconfig` plus `Assets/Analyzers/Microsoft.Unity.Analyzers.dll` give the
compiler teeth for a set of rules CLAUDE.md already states — Unity message signatures that
silently never run, dropped coroutines, allocating physics queries, `[MenuItem]` on a
non-static method, empty `Update()`. Severities are capped at **warning**; a build must never
fail on a lint rule.

**Three rules CLAUDE.md states are NOT covered, and it is worth being explicit about that**
rather than assuming the analyzer picked them up. No rule exists for any of them in this
package:

| CLAUDE.md rule | occurrences (2026-08-21) | why it is not covered |
|---|---:|---|
| no `async void` | 31 | no UNT rule; needs `Microsoft.VisualStudio.Threading.Analyzers` (VSTHRD100) |
| no `FindObjectOfType` in hot paths | 13 legacy + 61 `…ByType` | no analyzer rule exists; "hot path" is not statically decidable anyway |
| per-frame `Update()` cost | 147 `Update()` methods | having one is not a defect; UNT0001 catches only the *empty* ones |

The cheap honest option for the first two is a grep-based gate beside
`Tools/CI/validate_project.py` and `Tools/Build/check_conditional_compilation.py` — the
project already uses exactly that shape for rules a compiler cannot express. Not built here:
it is a separate concern from the assembly split and deserves its own review.

Two rules with a large existing backlog (`UNT0026` TryGetComponent, and the
`?.`/`??`-on-Unity-objects family) are deliberately held at `suggestion`. They are correct and
worth fixing, but landing them as warnings would add hundreds of Unity console entries on day
one and bury the warnings that indicate real bugs. Promote each to `warning` once its backlog
is cleared; the counts are recorded in `.editorconfig` next to each rule.

---

## Adding an asmdef — the checklist

1. **Prove the folder is a leaf.** Every first-party type it names must be declared inside the
   candidate set. Do not eyeball this on a folder of any size.
2. **Check what does not cross a boundary:** `internal` members, `partial` types, extension
   methods used from outside their namespace.
3. **Check for Editor-only code** in the folder; it needs its own asmdef with
   `includePlatforms: ["Editor"]`, not a platform-mixed one.
4. **Relocate what blocks it, do not refactor it.** `git mv` the file *and* its `.meta`. Never
   rename a type to make a boundary work.
5. **`autoReferenced: true`**, always, for a runtime assembly — that is the property that
   keeps everything left in `Assembly-CSharp` compiling untouched.
6. **One asmdef per commit**, so a bisect can name the boundary that broke something.
7. Run `python3 Tools/CI/validate_project.py` and
   `python3 Tools/Build/check_conditional_compilation.py`.
