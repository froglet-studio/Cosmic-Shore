# Rampage — Technical Documentation

## Overview

Rampage is the **Dolphin-only demolition race**, and the destructive analog of Crystal
Capture ("Scurry"): every domain races to be the first to DESTROY **2,000 hostile
prisms**. A forest of big cacti and other breakable flora fills the cell from just
outside the nucleus out to the membrane, and the arena's **contested crystals** respawn
inside the nucleus at the centre of it all — **how many is what intensity means here**,
falling from twice the roster at intensity 1 to a single one at intensity 4.

**The loop is the Dolphin's own economy, made into a sport.** Nothing here is scripted —
the mode simply arranges the arena so the vessel's existing spine becomes the game:

| the vessel already does this | Rampage makes it the game |
|---|---|
| Energy is banked **only by skimming** (+0.006667/skim, 150 skims fills it) | a cactus forest is the charging ground — and every prism you clip on the way through scores |
| Touching a **crystal** spends the whole meter as one conic jaw blast | the arena carries **fewer crystals than pilots** at the top intensities, so cashing out is contested |
| Energy owns the blast's **GAPE** (4.76° empty → 23.43° full) | arriving charged is worth ~5× the swath of arriving empty |
| The cone reaches **2,400 units** down-range | taking the crystal at the nucleus and turning outward sweeps a full radius of forest |
| Ramming a prism **halves** the meter | flying *through* the thicket instead of *into* it is the skill |

So a round reads: **graze the forest to charge → dive to the crystal in the nucleus →
aim back out at the thickest stand → fire.** See `DOLPHIN_ENERGY_ECONOMY.md` §1 for the
economy itself; this file only arranges around it.

- **Only hostile mass scores, and hostile means COLOUR.** The metric is
  `IRoundStats.HostilePrismsDestroyed`. Anything wearing one of the two domains that
  are not yours scores — **flora, fauna bodies, rival trails, laid structure, no
  distinction** — and anything wearing your own colour scores nothing, whether it is
  your teammate's trail or a cactus that happens to have grown Jade. Neutral
  (`Domains.Blue`) mass is hostile to everyone and always scores. Since the forest
  seeds uniformly across all three domains, roughly a third of it is worthless to you
  at any moment, which makes reading colour part of choosing a target rather than
  decoration. Shattering your own trail is worthless *by construction*, so there is
  still no lay-and-smash farming loop.
- **Destruction is the sanctioned mass sink.** The conserved-mass law says prisms are
  removed only by an *active* force — vessel abilities or fauna consumption. Rampage
  is that law played as a sport: every scoring act is a vessel ability consuming mass.
  No decay, no timers, no cullers anywhere in the mode.
- **The arena restocks itself.** As players carve the forest down, the cell drops below
  its phase thresholds and flora planting + growth resume — the food web and the
  demolition derby feed each other.

**Key architectural facts:**

- **Scene**: `Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity` (single unified
  scene — no separate singleplayer variant; solo play is a party of one + AI backfill)
- **GameMode enum**: `GameModes.Rampage = 2` — repurposed from the legacy
  single-player arcade entry (whose `MinigameRampage` scene never shipped; nothing
  playable depended on the old meaning)
- **Controller**: `RampageController : MultiplayerDomainGamesController` — structural
  clone of `MultiplayerCrystalCaptureController` (1 round / 1 turn, HasEndGame=false,
  server winner detection in `OnTurnEndedCustom`, snapshot `SyncFinalScores_ClientRpc`)
- **Scoring**: `RampageScoringRuleSO` (`metric = ScoringMetric.PrismsDestroyed`,
  golf-timed like HexRace/Scurry) — `TargetCount => GameDataSO.PrismTargetCount`;
  winning-domain players `Score = finish time` (displayed mm:ss:cs), losers the
  `GolfScoreSentinels` remaining-prisms sentinel (displayed "N Prisms Left", with
  individual prisms smashed on the secondary line); TEAM-major by construction
- **Turn monitor**: `RampagePrismTurnMonitor` — resolves the prism target from
  `EndConditionOverridesSO.GetRampagePrismTarget()` at StartMonitor (default **2000**,
  FrogletTools ▸ Game Modes ▸ End Game Conditions — never a per-scene field), syncs it via
  NetworkVariable → `GameDataSO.PrismTargetCount`, ends the turn via
  `rule.IsObjectiveReached`
- **Domains**: free-for-all like Scurry (`MinDomainsAllowed`/`MaxDomainsAllowed`
  defaults 1/3); players 1–4 with AI backfill
- **Intensity**: **4 levels** — **fewer crystals, more wildlife**, over a forest that is
  identical at every level. `CellTypeChoiceOptions.IntensityWise` over four cell configs.
  See "Four intensities" below.
- **Vessels**: **Dolphin only** — see "Why Dolphin-only" below
- **Objective arrow**: `RampageObjectiveProvider` — points at the **nearest** contested omni
  crystal and **nothing else, ever**. The filter is the point: `Crystal.Active` also holds every
  lifeform heart the food web is constantly dropping (this mode's whole verb is killing
  flora) and every team crystal a Dolphin seeds, so a nearest-live-crystal scan would
  spend the match swinging onto whichever cactus just died. Only a MANAGER-SPAWNED
  crystal (`Crystal.CrystalManager != null`, set solely by `CrystalManager.SpawnWithDomain`)
  is the arena's; hearts and seeded crystals are plain `Instantiate`s and carry no manager.
- **Config**: `_SO_Assets/Games/ArcadeGameRampage.asset` (registered in
  `GameLists/OrganicRematchGames.asset` + the pre-existing arcade lists)

## Why Dolphin-only, and how it is enforced

`ArcadeGameRampage.Vessels` holds ONE entry (`SO_Class_Dolphin`). The restriction is
**not** implemented in this mode — it is the platform's two-place clamp, exactly as in
Ribcage / Dog Fight / Wildlife Liberation:

1. `GameDataSO.SyncFromArcadeGame` clamps `selectedVesselClass` into the game's allowed
   set. This covers the machine that pressed Start, on every route (modal, rematch,
   Tournament chain).
2. `ServerPlayerVesselInitializer.ResolveSpawnVesselType` re-clamps **server-side at
   spawn**. This is the one that matters in multiplayer: `Player.NetDefaultVesselType`
   is an OWNER-write NetworkVariable each client sets from its OWN local config, and
   `SyncFromArcadeGame` never runs on a client, so a joining client walks in still
   wearing the hull it last flew.

The scene's four AI backfill templates are `vesselClass: 2` (Dolphin) so the AI flies
the same ship — an AI class comes from `aiInitializeDatas`, not from the clamp.

**The mode is Dolphin-only because a mixed roster would break the premise, not to be
exclusive.** A scarce crystal is only a contested object if it is the only way to
discharge a blast. A Rhino or Sparrow in the arena would ignore it entirely and shoot
the forest down on its own clock, so the crystal would stop being worth fighting over
for anyone — and the whole intensity ladder, which is *made of* that scarcity, would
stop meaning anything.

**The Dolphin can still make its own crystals, and that is deliberate.** Crystal
Seeding (its Charge ability) produces a TEAM crystal only the pilot's domain can collect,
on a **30 s** cooldown (→ ~15 s at Charge 10; **two crystals per cycle** at Charge 5). So
the arena crystal is not the *only* trigger — it is the **free, immediate,
uncontested-by-cooldown** one, which is what makes taking it a tempo play rather than a
necessity. Do not nerf the seeding ability for this mode; the tension between "my crystal
on a timer" and "the crystal, right now, if I can get there first" is the interesting half.

**Since 2026-08-14 that ability is PASSIVE**, which sharpens the tension rather than
softening it (`_Scripts/Controller/Vessel/R_VesselActions/DOLPHIN_CRYSTAL_SEEDING.md`).
It used to be a hold-to-plant on the right trigger, so a pilot could place a crystal
directly in front of themselves — free *and* positioned. Now the cooldown runs on its own
and drops each crystal at a random point in the cytoplasm, so the pilot's own supply costs
FLIGHT TIME to reach. The arena crystal keeps the one advantage that made it worth
contesting: it is the one you can already be standing on.

Two couplings to keep in mind when tuning this mode's crystal ladder:

- `DeployTeamCrystalAction.maxLiveSeeded` (**8**) caps how many of a Dolphin's own crystals
  may stand at once. At the top intensities the arena carries **one** contested crystal for
  the whole lobby, so a large seeded stock is the thing most able to dilute that scarcity —
  it is the first knob to look at if cashing out stops feeling like a race. Lower the cap
  or lengthen the cooldown; do not remove the ability (see above).
- The **objective arrow is already correct** and needs no change: `RampageObjectiveProvider`
  filters on `crystal.CrystalManager == null`, and a seeded crystal is instantiated with no
  manager, so the arrow keeps pointing exclusively at the contested omni crystal.

## The destruction → score pipeline (zero bespoke tracking)

The stat was already fully plumbed platform-wide; Rampage adds only the metric
mapping and the race framing:

```
Dolphin blast / ram destroys a prism
  └─ Prism.Damage / Prism.Explode / Prism.Implode
      └─ SetupDestruction → onTrailBlockDestroyed.Raise(PrismStats{OwnName, Volume, AttackerName})
              │  (SOAP channel — StatsManager.prefab listener)
              ▼
StatsManager.PrismDestroyed
  ├─ victim ROSTERED (a trail — exists on every peer) → server records, as always
  │    same domain? → Friendly… stats (never scores)   else → HostilePrismsDestroyed++
  └─ victim UNROSTERED (environment — flora/fauna/structure, per-peer positions)
       ├─ credited by whoever SIMULATES the attacker (StatsManager.OwnsAttacker):
       │    server for its own player + every AI; the owning client via
       │    Player.ReportEnvironmentPrismDestroyed_ServerRpc for its own kills
       └─ hostile iff the prism's colour is not the attacker's
            (StatsManager.IsFriendlyEnvironmentPrism; Blue is hostile to all)
              │
              ▼
ScoringMetrics.Read(stats, PrismsDestroyed) → SumByDomain
  ├─ MultiplayerDomainGamesController.SyncDomainSumsRoutine → HUD domain panels
  ├─ RampagePrismTurnMonitor.CheckForEndOfTurn → rule.IsObjectiveReached  [server]
  └─ ElementalComebackSystem (source PrismsDestroyed) → trailing-team elemental buff
              │  turn end
              ▼
RampageController.OnTurnEndedCustom                [server]
  ├─ rule.ResolveWinner / AssignScores
  │    winners: Score = finish time (Time.time - TurnStartTime)
  │    losers:  Score = DnfThreshold + team prisms remaining
  └─ SyncFinalScores_ClientRpc → WinnerName/WinnerDomain, Results, MiniGameEnd
```

**The jaw blast credits its pilot.** `VesselExplosionByCrystalEffectSO` hands the firing
vessel to `AOEConicExplosion`, and `PrismSpatialIndex.ResolveDamage` reads
`vessel.VesselStatus.Player.Name` off it, so every prism a cone shatters lands on that
pilot's `HostilePrismsDestroyed`. (A blast constructed with a null vessel is *anonymous*
and credits `🔥GuyFawkes🔥` instead — that is the failure mode to check first if a mode
ever reports blasts scoring nothing.)

**A client's kills count on the client's own screen.** Flora and fauna are spawned per-peer
from local `Random` rolls, so the server's copy of a cactus is somewhere else entirely —
recorded server-only, a client scored nothing for the entire living world and could only ever
score off the other pilot's trail (which IS laid identically on both peers). Environment mass
is now credited by whichever machine simulates the attacker, and the collecting pilot runs
their own crystal effects so the blast exists on their machine at all. Full record:
`Docs/ECOSYSTEM.md §27.8`–`§27.9`.

## Four intensities — fewer crystals, more wildlife, one forest

Ribcage's intensity adds rinds inward from a fixed outer radius. Rampage's used to thicken the
forest; **it no longer touches the forest at all.** Every intensity grows intensity 4's arena,
prism for prism — the one that was play-tested — and intensity instead moves the two things the
mode's loop is actually made of, in opposite directions:

| | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| **omni crystals** | 2 × players | players | players − 1 (min 1) | **1** |
| …for a 4-player lobby | 8 | 4 | 3 | **1** |
| **wildlife** (`FaunaPopulationScale`) | 1× | 2× | 3× | **4×** |
| …tadpoles / sharks at cap | 6 / 2 | 12 / 4 | 18 / 6 | **24 / 8** |
| seeded forest | 9,830 prisms | 9,830 | 9,830 | 9,830 |
| phase ladder | identical at all four (frenzy 1,630,000 / 1,260,000 vol) | | | |

**Why crystals are the difficulty axis.** The crystal is not a pickup in this mode, it is the
Dolphin's **only** blast trigger (`DOLPHIN_ENERGY_ECONOMY.md` §1) — the meter fills by skimming
and discharges only on contact with one. So the crystal count *is* how often a charged pilot
gets to cash out, and how much of that is a race against the other pilots. Two crystals per
player and you fire whenever you are full; one crystal for the whole lobby and every discharge
is a contest, with denial plays (taking it empty to move it away from a charged rival) worth
making. Nothing implements that escalation — it falls out of the count.

Everything defining the arena's silhouette and rules is still one constant at all four levels:

| held constant at every intensity | value |
|---|---|
| `MembranePrefab` | `CapsuleMembrane`, r **1200** |
| `NucleusPrefab` | `HalfNucleus`, world r **100** — and therefore the crystal respawn volume, the flora band's inner clamp, and the 600 spawn ring |
| the forest | 59 plants / 9,830 seeded prisms / ~1.62M volume at full growth |
| `PhaseThresholds` | one ladder, shared |
| prism target | 2000 — vary the arena, not the finish line |
| fauna + flora species assets | unforked (the fauna are the SHARED Blob assets) |

**Collider impact.** The worst case is unchanged: intensity 4's forest was already the shipped
one, at 2.8× the Blob envelope as documented headroom. Intensities 1–3 rise *to* it rather than
the top rising past it, and the `FrenzyEnter` 10,000 count backstop is the same at all four.
The fauna ladder is the cheap dimension — a tadpole is one body prism plus its heart, a shark a
small spindled body, so 8 → 32 creatures is tens of prisms against 9,830, and creature sensing
rides the Burst density grid, not physics. At most 8 crystals (one trigger each) at intensity 1.

### How it is authored

**The cell half** — `Cell.CellConfigs` holds four `CellConfigDataSO`s with
`cellTypeChoiceOptions: 1` (`IntensityWise`), **list order is semantics**: index = intensity − 1.
Each points at its own `SpawnProfileSO`. The four configs are now identical apart from
`Difficulty`, their description, and which profile they name; the four profiles differ in exactly
one field:

| profile | `FloraPopulationScale` | `FloraPlantBudgetScale` | `FaunaPopulationScale` |
|---|---|---|---|
| 1 | 1.00 | 1.00 | **1.0** |
| 2 | 1.00 | 1.00 | **2.0** |
| 3 | 1.00 | 1.00 | **3.0** |
| 4 | 1.00 | 1.00 | **4.0** |

`FaunaPopulationScale` is a **platform** capability, the twin of the flora scalars: it multiplies
every species' `InitialSpawnCount`, `PopulationSize` **and `MaxLivePopulation`**. Rampage is why
it has to exist rather than being nice to have — its two species are the shared Blob assets, so
editing them to stock this arena would restock Menu_Main's lava lamp with it. It scales the CAP
as well as the floors on purpose: the cap is what bounds a standing population, so a scalar that
moved only the floor would be clamped away above ~1.5× and read as doing nothing. Full record,
including why every producer resolves it through `Cell` rather than the config:
`Docs/ECOSYSTEM.md §29`.

**The scene half** — the crystal ladder is authored on the scene's `NetworkCrystalManager`, not
in a cell config, because it is a function of the ROSTER as well as the intensity:
`crystalCountMode: IntensityScaled` (2) plus four `crystalCountByIntensity` entries of
`max(1, round(players × CrystalsPerPlayer) + ExtraCrystals)`:

| intensity | `CrystalsPerPlayer` | `ExtraCrystals` | reads as |
|---|---|---|---|
| 1 | 2 | 0 | twice as many crystals as players |
| 2 | 1 | 0 | one each |
| 3 | 1 | −1 | one fewer than players (floored at 1, so a solo pilot still has one) |
| 4 | 0 | 1 | exactly one, whatever the roster |

The count is resolved **server-side only** and reaches clients as the replicated slot-list
length, so — unlike the sticky cell-config choice (`Docs/ECOSYSTEM.md §28.2`) — it needs no
`GameConfigSynced` gate. The roster may be incomplete when the first crystals spawn
(`spawnOnClientReady: 1`); `NetworkCrystalManager` re-asks on every `OnPlayerAdded` and again at
turn start, growing the list as players arrive. AI backfill counts.

**The model lives in `Tools/Build/rampage_intensity.py`**, not in the assets. It computes the
forest's prism count and full-grown volume from the same numbers the game reads, derives the
eight thresholds, emits all eight assets, and **self-tests by reproducing intensity 4's shipped
ladder to the digit** — plus, now that the forest is flat, that all four intensities land on that
same ladder. Regenerate with `--write`; `--check` fails if an asset was hand-edited. The one soft
input is the three phyllotactic prism volumes (those species size prisms by role, so there is no
single authored field to read) — `CALIBRATION` in that script is where one in-editor measurement
corrects the ladder.

**`RampageIntensityLadderTests`** (edit-mode) guards the other end: the two pure formulas, and the
authored data in the scene and the four profiles. The generator's `--check` cannot see the scene,
and an inverted sign there is silent — the mode still runs, it just stops meaning what it says.

## The arena — a forest filling the cell, a clear nucleus

`_SO_Assets/Cell Configs/Rampage Cell/`. Membrane radius **1200** (`CapsuleMembrane`),
nucleus world radius **100** (`HalfNucleus.prefab` at scale 200).

### The forest

Five species, each with its own planting **band** (a min/max fraction of the membrane
radius). Plants are drawn **volume-uniformly** inside the band, not on a shell, so density
is even through the whole volume rather than crowded onto one radius:

| species | script | band | world radii | plants seeded | prisms/plant | leaf prism vol | scale/level |
|---|---|---|---|---|---|---|---|
| **Cacti** (hero) | `BranchingFlora` | 0.10–0.95 | 120–1140 | 26 | 160 | 5×5×3 = **75** | **1.30** |
| Spire | `PhyllotacticFlora` | 0.30–0.97 | 360–1164 | 10 | 190 | ~15 | 1.25 |
| Pine | `BranchingFlora` | 0.14–0.90 | 168–1080 | 10 | 150 | 4×4×1 = 16 | 1.25 |
| Rosette | `PhyllotacticFlora` | 0.40–0.96 | 480–1152 | 7 | 170 | ~17 | 1.25 |
| Coral | `PhyllotacticFlora` | 0.10–0.80 | 120–960 | 6 | 180 | ~10.6 | 1.25 |

Seeded total ≈ **9,830 prisms** across 59 plants, and planting continues past the seed
batch until the cell tops out (below).

**The forest fills the cell, it does not ring it.** The bands run from just outside the
nucleus all the way to the membrane, so a pilot leaving the core immediately has mass in
reach and the arena never has a dead middle-distance. Because the draw is volume-uniform,
most plants still land in the outer reaches — that is simply where the space is — while a
real handful sit in close.

**The nucleus stays clear, and that is enforced in code, not by authoring.**
`Flora.ResolvePlantRadius` clamps every band's inner edge to `Cell.ExpectedNucleusWorldRadius`
(100 here), so an author can write `0.10` — or `0` — and get "from the nucleus outward"
rather than plants inside the core. Nucleus-interior mass is excluded from the fauna
targeting grids and shares its volume with the crystal respawn, so a plant in there would
be food the web can never reach *and* clutter in the one volume that has to stay legible.

**The sizes are a range, not a size.** `Levels {1..5}` with `RarityFalloff 1.6` (flatter
than the usual 2.0, so big plants are common rather than rare) crossed with
`LeafScalePerLevel` 1.25–1.30 gives a **1.0× → 2.4×** linear span on the ordinary species
and **1.0× → 2.9×** on the cacti. A level-5 cactus is a landmark; a level-1 one is a bush.
Every config also runs `SpreadElements` over the species' four canonical element assets,
and `CellLifeSpawnerBase.SpawnFlora` rolls the DOMAIN uniformly across all three — so the
forest is a genuine mixture of colours, elements and sizes, and there is hostile mass in
every direction for every pilot (no-domain-asymmetry invariant).

Each config's own `Variant` block is **disabled on purpose**: the element palette owns
every plant's identity (leaf prism shape, growth tempo, shield cadence), and the cell
asserts only its layout facts — the band and the per-plant budget — through
`PlantRadiusCellFractionMinOverride` / `MaxOverride` / `MaxTotalSpawnedObjectsOverride`,
which are applied after the roll and therefore survive it. Authoring the band in the
`Variant` block instead would be silently discarded by `SpreadElements` and the whole
forest would collapse onto each species' authored 0.5–0.6 shell.

Cacti are the hero for a reason: `BranchingFlora` at 85–95° branch angles gives the
right-angled arms, and its leaf prism is 5×5×3 = **75 volume, 4.7× nominal** before level
scaling — so a single hit is a chunky, legible piece of destruction, while still counting
exactly 1 toward the 2,000.

### The phase ladder rides the forest's real volume

**Volume is the spine, and this forest is NOT made of nominal prisms** — the cacti alone
are 4.7× nominal each, so the inherited `count × 16` derivation was wrong by ~3× and
would have pinned the cell at Frenzy (planting frozen) almost immediately, leaving a
sparse arena that never regrew. Authored explicitly:

```
RestlessEnterVolume   113000   RestlessExitVolume    81000
FrenzyEnterVolume    1630000   FrenzyExitVolume    1260000
RestlessEnter 700  RestlessExit 500  FrenzyEnter 10000  FrenzyExit 8000   (count backstop)
```

- **Frenzy volume ≈ the full-grown forest** (est. ~1,616,000: Σ plants × prisms × leaf
  volume × E[level volume multiplier], which at falloff 1.6 is 3.21 on the ordinary species
  and 4.31 on the cacti). Planting and growth freeze there — a growth gate, never a culler, so mass stays conserved.
- **Frenzy exit at ~77%** of enter: regrowth resumes once roughly a quarter of the forest
  is gone, which at these numbers is a few hundred prisms of destruction — fast enough
  that the endgame never starves of targets.
- **Restless at ~7% of Frenzy**, the same proportion Blob uses, so fauna start hunting
  early rather than waiting for a full arena.
- **The count fields stay the perf backstop**: 10,000 prisms forces Frenzy regardless of
  volume. The seeded forest lands just under it on purpose, so player trails are what push
  the arena into its cap.

**These volume numbers are an estimate and must be checked in-editor** — phyllotactic
prisms are shaped per role (stem spans its segment, leaf spans its reach), so their
volume cannot be read off a single authored field, and the level spread multiplies
whatever it is. Watch `Cell.LiveVolume` on the DiagnosticsHUD through the first minute:
if the forest tops out well under `FrenzyEnterVolume` the arena will keep planting past
its intended density (watch the prism count and the collider budget), and if it hits
Frenzy before the forest looks full, raise `FrenzyEnterVolume`/`FrenzyExitVolume`
together, keeping the ~77% ratio.

**Collider-budget note:** 10,000 prisms is ~2.8× the Blob envelope (3,600) and well
above the masterplan's ≤1500 active-collider target — deliberate design headroom for a
demolition arena, unchanged from the previous Rampage. Mitigations: collider-LOD by
phase, Burst density-grid queries (no new physics queries — scoring rides the
StatsManager SOAP channel and AI targeting rides the density grid), and the mode's whole
verb actively removes mass. The seeded flora instantiate one-per-frame
(`FloraSpawnIntervalSeconds: 0`), so the opening batch costs ~2.3 s of spread-out spawn
rather than one hitch.

### Fauna — the second half of the intensity ladder

Tadpole (grazer) + shark (predator), referenced from the Blob folder and **unforked**. They
are the food web, and both drop elemental crystals on death — skimmable powerups
mid-rampage, and one more thing worth shooting.

**Intensity is how many of them there are**: `FaunaPopulationScale` 1× / 2× / 3× / 4× on the
per-intensity SpawnProfile, so the cell holds 8 creatures at cap at intensity 1 and 32 at
intensity 4. Because the scalar lifts the seed floors *and* `MaxLivePopulation` together, a
high-intensity arena is genuinely busier rather than just refilling faster: more grazers
working the forest, more sharks hunting them, more hearts dropping mid-match.

It is a **production** knob, not a cull — a lowered scale stops the seeder topping up and stops
reproduction filling, and every creature already alive lives until starvation or predation takes
it (`Docs/ECOSYSTEM.md §0`, §29.1).

**The tuning risk to watch, and it is the interesting one.** This cell has a nucleus, so
herbivores graze **any** domain's mass outside it (the voracious-exterior rule,
`Docs/ECOSYSTEM.md §13`) — which is the same forest the pilots are racing to destroy for points.
At intensity 4 that is 24 grazers working an arena of 9,830 prisms. The homeostasis is designed
for it: grazing pushes volume below `FrenzyExitVolume` (1,260,000) and planting plus growth
resume, so the arena restocks rather than emptying. But whether it restocks *fast enough to keep
the arena reading dense* at 4× population is a play-test question, not an arithmetic one. If
intensity 4 looks visibly thinned mid-match, the knob is `FaunaPopulationScale` on
`Rampage Spawn Profile 4` (via `FAUNA_SCALES` in `Tools/Build/rampage_intensity.py`, then
`--write`) — **not** the phase thresholds, which are pinned to the forest's real volume.
Watch `Cell.LiveVolume` on the DiagnosticsHUD at intensity 1 and 4 for the comparison.

## The contested crystals

`NetworkCrystalManager` on the scene's Game object:

- `crystalCountMode: IntensityScaled`, four `crystalCountByIntensity` entries — **2 × players /
  players / players − 1 / 1**, by intensity. See "Four intensities" above for the table and why
  the count is the mode's difficulty axis.
- `spawnCrystalWithPlayerDomain: 0` — they are neutral (`Domains.Blue`), so
  `Crystal.CanBeCollected` lets **any** pilot take one. That is the contest.
- **The spawn volume is the NUCLEUS**, and the scene authors nothing for it. That coupling
  is platform-wide and locked (see below), so a crystal respawns somewhere inside the
  **100-unit** core every time it is collected — the nucleus is the visible marker of where
  to look, and it does not lie. More crystals means more of them sharing that core, not one
  roaming to make room; each respawn still draws its own point and keeps its distance from
  where that crystal last sat (`PickSpawnPointAwayFromLast`).
- The objective arrow names the **nearest** collectable managed crystal
  (`RampageObjectiveProvider`), so at low intensity it points at your closest opportunity and
  at intensity 4 there is only ever one answer.
- The cell points at **`HalfNucleus.prefab`** (world radius 100) rather than `Nucleus.prefab`
  (200), which is the platform's one sanctioned way to resize a Cell-owned visual — the same
  move Scurry makes. Because the two are coupled, halving the nucleus halves the crystal's
  respawn volume with it: the prize is pinned to a tighter, more contested point.

### The crystal volume IS the nucleus — platform-wide, not a Rampage choice

`CrystalManager.GetAnchorlessSpawnRadius()` resolves **nucleus → `noNucleusSpawnRadius` →
crystal `SphereRadius`**, in that order. A cell WITH a nucleus always spawns its crystals
inside it and no per-scene field can say otherwise; `noNucleusSpawnRadius` is the fallback
for a cell that genuinely has no core at all (Dog Fight's Boneyard, 420).

The precedence used to be inverted — the serialized field beat the nucleus — and this mode
is what surfaced why that is wrong. Rampage briefly authored a 900-unit roam radius to make
the crystal a chase, which decoupled it from the marker players actually read as "the
middle", and left every mode free to teach its own answer to "where is the crystal". A mode
that wants a different crystal volume **resizes its nucleus** (author a `CellConfigDataSO`
pointing at a resized `NucleusPrefab`, per CLAUDE.md) so the two move together and stay
coupled.

That also gives the arena its shape: the crystals are at the centre, the forest is
everywhere else, and the cone is 2,400 units long — so a charged pilot who takes a
crystal and turns outward sweeps a full radius of trees.

Because the meter is spent on contact regardless of how full it was, taking a crystal
empty is a legitimate **denial** play: it costs you nothing and moves the prize away from
a rival who is fully charged. That falls out of the existing rules; nothing implements it —
and it is exactly the play that gets sharper as intensity removes crystals from the core.

### Spawn ring

`arrangeSpawnPointsAroundCell: 1`, `spawnFormation: Symmetric`,
`spawnDistanceOutsideNucleus: 500` → pilots start on a sphere of radius **600**
(`ExpectedNucleusWorldRadius` 100 + 500 = 600), symmetric by count, all facing the cell —
i.e. out in the forest, looking back at the crystal. Previously the four authored
transforms put everyone inside a ±50 box at the arena centre: four Dolphins nose to
nose, sitting on the crystal with an empty meter. The authored transforms remain as the
fallback the platform uses if the cell can't be resolved.

## The AI

**This controller installs no AI targeting of its own** — and that is the design, not an
omission. `AIPilot`'s default behaviour already IS the mode's loop:

1. **It flies to the crystal.** `UpdateCellContent` targets the nearest collectible cell
   item; in this arena that is the one contested crystal, in every domain's own colour
   terms (the crystal is neutral, so everyone may take it).
2. **It drifts once lined up, and looks at mass while it does.** With `drift` on (the
   Dolphin prefab authors it), an AI that has the crystal lined up locks
   `VesselStatus.Course` on it and swings its NOSE somewhere else — which is how a drifting
   vessel lays trail, skims and fires along an axis that is not its heading. What it points
   at is now **a cluster of hostile mass**, resolved through `Cell.GetExplosionTarget` — the
   exact Burst density-grid query aggression-1 fauna hunt prey with, sampled on
   `massClusterRetargetInterval` (1.5 s) and flown at in between.

That second half is a platform change, not a mode one. The drift look-direction used to be
a flat **180° flip** away from the objective (`desiredDirection *= -1`), which aims at
nothing in particular and reads as the AI spinning on the spot. Pointing it at mass makes
the same maneuver productive in every mode that drifts, and it means "go where the mass is"
is ONE system on this platform (`Cell.GetExplosionTarget`) rather than a per-mode
re-derivation. The grid it reads already excludes nucleus-interior and shielded mass, so it
can only ever aim at mass the AI is allowed to attack. It falls back to the legacy flip when
there is no cell, no mass, or the cluster lies in the same direction as the crystal (where
the drift would not turn the vessel at all).

**A mode-local two-phase provider was written here and removed.** It read the pilot's
Energy and switched between "graze the densest mass" and "break for the crystal" via
`AIPilot.SetExternalTargetProvider`. It worked, but an external provider **overrides crystal
seeking outright**, which is the one thing the AI must not stop doing in a mode whose
objective is a crystal — and it duplicated, per-mode, the drift/mass behaviour the platform
could carry for everyone. If a future mode genuinely needs bespoke AI objectives, the hook
is still there (Astro League uses it for ball striking).

## End condition

Authored ONLY through **FrogletTools ▸ Game Modes ▸ End Game Conditions**
(`EndConditionOverridesSO.rampagePrismTarget`, 0 = default 2000). Applies wherever
the mode runs. Live/Build split + build auto-restore work like every other mode.

## Comeback

`ArcadeGameRampage.asset` sets `ComebackRatePerScoreDeficit: 0.01` (not the default
1.0): prism deficits run ~100× larger than Scurry's crystal deficits (target 2000 vs
20), so 0.01 keeps the buff curve proportionate — a ~1000-prism team deficit maxes
the comeback ceiling the way a ~10-crystal deficit does in Scurry. The scene-authored
`ElementalComebackSystem` uses `ScoreDifferenceSource.PrismsDestroyed` (Score only
lands at game end in this mode, so the Score source would be inert live).

It lands especially well on this vessel: Space widens the cone's reach, Charge shortens
the seeding cooldown, and Mass fattens the drift trail — so a trailing Dolphin visibly
hits harder rather than just moving faster.

## Strategy surface (why it's a race, not a grind)

- **Arrive charged.** Empty is a 4.76° gape, full is 23.43° — the same trip, ~5× the
  swath. The whole skill expression is judging whether you have time for one more pass
  through the thicket before someone else takes the crystal.
- **Fly through, not into.** A skim banks energy; a ram halves it. The forest rewards
  threading gaps at speed and punishes bulldozing — and a ram still scores, so the
  greedy line is genuinely tempting.
- **Aim before you touch.** The blast fires along the hull's gape axis at the moment of
  contact, so which way you are pointing when you take the crystal decides whether the
  cone eats empty space or a full radius of forest — and since the crystal sits in the
  nucleus, the good answer is always "outward".
- **Target choice.** Dense flora clusters score fastest (any colour — environment mass
  is bounty for everyone); opposing trails score too and simultaneously deny the
  opponent skim/boost infrastructure. Only your own team's trails are dead weight.
- **Denial is real.** Taking the crystal at zero charge costs you nothing and moves it.
- **Destruction feeds the enemy comeback.** Pull far ahead and the trailing domains
  get all-element buffs — rubber-banding without scripting.
- **Fauna are jackpots with teeth.** Opposing fauna are multi-prism bodies worth
  several points that fight back; their crystal drop pays an elemental buff on top —
  and, being a crystal, it *also* discharges your blast, so a shark kill can hand you a
  free trigger at the wrong moment.
- **Regrowth keeps late game honest.** A picked-clean forest regrows below the phase
  thresholds, so the endgame never starves of targets.

## Shared-Code Touchpoints

Added when the mode first shipped:

| Site | Change |
|---|---|
| `ScoringMetric` | `PrismsDestroyed = 5` |
| `ScoringMetrics.Read` | `PrismsDestroyed => stats.HostilePrismsDestroyed` |
| `GameDataSO` | `PrismTargetCount` (+ both runtime resets) |
| `EndConditionOverridesSO` (+ window + asset) | `rampagePrismTarget` live/build/getter, default 2000 |
| `ElementalComebackSystem` | `ScoreDifferenceSource.PrismsDestroyed` + `GameModes.Rampage` default-source case |
| `GameModes` | doc comment on the repurposed `Rampage = 2` |

Added by the Dolphin rework. The three ecology items are **platform fixes, not mode
special-cases** — full record and rulings in `Docs/ECOSYSTEM.md §27`:

| Site | Change |
|---|---|
| `Flora.ResolvePlantCenter()` | New shared helper — the planting band is measured from the **cell centre**, not the crystal. All three `Plant` implementations call it. (`§27.1`) |
| `Flora.plantRadiusCellFractionMin` + banded `ResolvePlantRadius` | A species plants in a volume-uniform BAND instead of on one shell, with the inner edge clamped outside the nucleus. Default 0 = single shell, so every existing cell is unchanged. (`§27.5`) |
| `CrystalManager.GetAnchorlessSpawnRadius` | Precedence inverted: **nucleus first**, serialized radius only as the no-nucleus fallback (renamed `anchorlessSpawnRadius` → `noNucleusSpawnRadius`). Platform-wide coupling. (`§27.6`) |
| `AIPilot.ResolveDriftLookDirection` | The drift look-direction is a hostile-mass cluster from `Cell.GetExplosionTarget` instead of a flat 180° flip. Platform-wide. (`§27.7`) |
| `MiniGameHUD.RefreshObjectiveProviderForCurrentMode` | Re-resolves the objective arrow's provider once the config ClientRpc has landed, so a client that reached `Start()` early can't keep another mode's provider (e.g. one that points at PLAYERS). |
| `BranchingFlora.Initialize` | Resolve `CrystalTransform` once and fall back to the growth axis — it returns null in a crystal-less cell and `.position` on it threw. |
| `BranchingFlora` / `PhyllotacticFlora` `.ApplyVariantTuning` | Honour `FloraVariantTuning.MaxTotalSpawnedObjects`, which only `AssembledFlora` read. **Changes existing cells** — 45 authored assets were writing into an inert field. (`§27.2`) |
| `FloraConfigurationSO.PlantRadiusCellFraction{Min,Max}Override` / `MaxTotalSpawnedObjectsOverride` (+ `TryBuildCellOverrideTuning`, called from `CellLifeSpawnerBase.SpawnFlora`) | Cell-level overrides applied AFTER the variant roll, so a cell can use the canonical per-element assets and still choose its own planting band and plant size. All default −1 = off. (`§27.3`) |
| `MiniGameHUD.CreateObjectiveProviderForGameMode` | `GameModes.Rampage` → `RampageObjectiveProvider`. |

The short version of why the flora ones were needed at once: the forest is authored as
"canonical element identity, **this cell's** layout". The planting anchor decided *where
the band is measured from*, the band itself decided *whether a species can fill a volume
rather than a shell*, the budget fix decided *whether a per-plant cap works at all on
branching/phyllotactic species*, and the overrides decided *whether a cell's layout
survives `SpreadElements`*. Any one of them missing puts the forest on one radius in the
middle of the arena, or lets twenty-six cacti grow to 5,000 prisms each.

The element/cell split worth carrying forward: **the ELEMENT owns identity** (leaf prism
shape, growth tempo, shield cadence, per-element density) and **the CELL owns layout**
(where a species plants, how big one plant may get in this arena). They shared one
`Variant` block because they were authored together, not because they are the same kind
of fact.

## Assets

| Asset | Notes |
|---|---|
| `ArcadeGameRampage.asset` | `Mode 2`, `IsMultiplayer 1`, players 1–4, **Dolphin only**, `SceneName MinigameRampage`, comeback rate 0.01 |
| `RampageScoringRule.asset` | `metric 5 (PrismsDestroyed)`, `golfRules 1` (finish-time scoring) |
| `MinigameRampage.unity` | Dolphin AI templates, cell spawn ring; the crystal volume is the nucleus and is deliberately un-authored. In `EditorBuildSettings` |
| `Rampage Cell Config.asset` | forest-tuned `PhaseThresholds` (volume authored, not derived) |
| `Rampage Spawn Profile.asset` | the five forest species + tadpole/shark fauna |
| `Rampage {Cacti,Rosette,Spire,Pine,Coral} Flora Config Data.asset` | per-species planting BAND + budget + size-range configs (new) |
| `GameLists/OrganicRematchGames.asset` | Rampage listed (party-games list) |

## In-editor verification checklist

Authored headless; every item below needs a play-mode pass.

1. **Open `MinigameRampage.unity`** — no missing script refs on the Game or Cell objects;
   the Cell shows **4** CellConfigs and `Cell Type Choice = Intensity Wise`.
2. **Solo launch with AI backfill** (1 human + 3 AI). Confirm every hull is a **Dolphin**,
   including the AI, and that pilots start spread on a 600-radius sphere facing the cell
   rather than clustered at the centre.
3. **The forest** — after ~60 s, flora fill the cell in all directions from just outside
   the nucleus (~200 u) out to the membrane, in mixed colours, elements and visibly
   different SIZES (a level-5 cactus should read as a landmark). Nothing planted inside
   the nucleus; nothing outside the membrane.
4. **The ladder** — watch `Cell.LiveVolume` and the live prism count on the
   DiagnosticsHUD as the forest fills. It should approach ~1.62M volume / ~9.8k prisms
   and stop growing at Frenzy, not top out early. Retune `FrenzyEnterVolume` /
   `FrenzyExitVolume` together (keeping ~77%) if it lands far off.
5. **The economy** — skim the forest and watch the HUD jaw gauge / hull jaws open; take
   the crystal and confirm the cone fires at the gape the meter had earned, and that the
   crystal respawns somewhere else **inside the nucleus**.
6. **Scoring** — prisms destroyed by the blast increment the local pilot's domain panel
   (i.e. the blast is credited, not anonymous). Own-trail prisms score nothing.
7. **Objective arrow** — points at the crystal and re-acquires after each collection.
8. **The AI** — an AI flies AT the crystal (not at other players), and once lined up it
   drifts with its nose swung onto a stand of trees rather than spinning 180°.
9. **End + replay** — reaching the target ends the turn, the scoreboard ranks by finish
   time with "N Prisms Left" for the losing domains, and replay reloads the scene clean.
10. **Every intensity.** Launch 1 and 4 back to back. `Cell.LiveVolume` / prism count on the
    DiagnosticsHUD should settle near **569k / 3,500** and **1.62M / 9,830** — they must look and
    profile obviously different. If intensity 1 settles far from 569k, put the measured
    per-species ratio into `CALIBRATION` in `Tools/Build/rampage_intensity.py` and re-run it;
    all four ladders move together.
11. **MPPM, 1 host + 1 client, intensity 4 — the race regression test, do not skip.** Both peers
    must show the same forest and the same prism count. A client that ends up at ~3,500 prisms
    while the host has ~9,830 is the `AssignConfig` race (below) having regressed. Console may
    log `IntensityWise config choice DEFERRED` once on the client — that is the gate working.
12. **Regression: other cells' flora density.** Honouring `MaxTotalSpawnedObjects` on
    branching/phyllotactic flora switches on 45 previously-inert authored budgets. Take a
    pass through **Hesperides** (the densest flora cell) and Wildlife Blitz cells 1–3 and
    confirm `Cell.LiveVolume` still sits inside its thresholds. Expected effect is ≈ −6%
    prisms per plant on average with restored per-element variety — see
    `Docs/ECOSYSTEM.md §27.2`.

## Known limitations / follow-ups

- **Legacy training asset**: `SO_TrainingGame_Rampage.asset` was **de-listed** from
  `TrainingGames.asset` (the daily-challenge pool) and Rhino's hangar training
  slots (repointed to WildlifeBlitz, matching Sparrow) — `Arcade.LaunchTrainingGame`
  launches scenes unconfigured (no GameMode/player-count/backfill), which is wrong
  for a multiplayer mode. The training SO itself remains on disk with pre-rework
  score tiers (10000+); re-tune to prism counts if that surface returns.
- **Menu unlock**: `Rampage(2)` is in `ProgressionConfig.asset` `alwaysUnlockedModes`
  so the card is clickable on fresh accounts.
- **No UGS stats reporter yet**: Scurry has `CrystalCaptureStatsReporter`; a
  `RampageStatsReporter` (most-prisms-smashed leaderboard) is a clean follow-up.
- **No Rampage `GameToastConfigSO`** — no mode-specific toast copy for "crystal taken",
  "forest regrowing", or milestone rungs. Ribcage's progress-milestone pattern would port
  cleanly if the race wants more mid-match texture.
- **Density is even, not clumped.** The band draw is volume-uniform, so plants are spread
  evenly rather than gathered into thickets with clear lanes between them. Clustering
  (draw a handful of grove centres, then scatter around each) would give the forest more
  readable structure and better hiding places, and is the natural next extension of
  `Flora.ResolvePlantRadius`.
