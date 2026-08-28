# The Game-Mode Top Bar

**Applies to:** every multiplayer domain mode — all 11 scenes that instance
`_Prefabs/GameCanvas-HexRace.prefab`. One prefab, one bar, no per-scene forks
(`Docs/GAMECANVAS.md` § "Shared prefabs are single sources of truth").

---

## 0. What the bar says, in one line each

| Region | Says | Source |
|---|---|---|
| **Left** — objective readout | *this mode's objective, and how much of it is left* | glyph from `ScoringMetric`, number from the turn monitor |
| **Centre** — domain score bar | *every team's score, and who is on each team* | `GameDataSO.GetDomainMetricSum` per domain |
| Right | volume / pause | unchanged |

Nothing else. The bar carries no player names, no per-player score card, and no
chrome that is not one of those two readouts.

---

## 1. Left — the objective readout

```
[glyph]  1840
```

`ObjectiveReadout` (`_Scripts/UI/Elements/ObjectiveReadout.cs`) on the `RoundTime`
object, 32 px in from the top-left corner, 260 × 56.

**It adds no plumbing.** The NUMBER is the same `TMP_Text` that
`MiniGameHUDView.UpdateCountdownTimer` has always written — every turn monitor
already raises `onUpdateTurnMonitorDisplay` with the metric **remaining**
(`NetworkCrystalCollisionTurnMonitor`, `JoustCollisionTurnMonitor`,
`RampagePrismTurnMonitor`, `WildlifeKillTurnMonitor`, `CombatPointTurnMonitorBase`,
`ScarabScrambleGoalTurnMonitor`, `NucleusRushWaveTurnMonitor`,
`RibcagePrismTurnMonitor`, …). No turn monitor, no event, and no end condition
changed. This component owns only the GLYPH.

> **The ring was drawing a clock face over an objective count.** The number lived
> inside `BigCircle` + three `JustRotate` rings + a `Timer` face — 90 × 90 of
> stopwatch chrome around a value that has nothing to do with time. Both ring
> clusters (`RoundTime` and `LifeFormCounter`, 10 objects / 50 documents) are gone.
> The field they fed, `MiniGameHUDView.roundTimeDisplay`, keeps its name because
> renaming a serialized field is a separate, sweep-able change (§5).

### 1.1 The glyph is keyed on the METRIC, never on the mode

`ObjectiveIconSetSO` (`Resources/ObjectiveIconSet`) maps `ScoringMetric` → sprite.
That is the whole design: `ScoringMetric` is already the platform's single answer
to *"what is this mode scored on"* — it drives the HUD number, the remaining
count, the end condition and the scoreboard secondary — so a new mode that picks
an existing metric gets its objective glyph **for free**, and only a genuinely new
metric ever needs new art.

A per-mode override table would re-open the exact divergence `ScoringMetric` exists
to close. Do not add one.

| Metric | Glyph | Modes today |
|---|---|---|
| `Crystals` | faceted gem, hollow | HexRace, Crystal Capture |
| `OmniCrystals` | the same gem, **solid** | — |
| `ElementalCrystals` | the gem carrying one element rhombus | — |
| `Jousts` | two lances meeting, with the spark | Joust |
| `Goals` | a **switch** ring with the dart mid-thread | Scarab Scramble, Brood Rush |
| `PrismsDestroyed` | a prism split along a jagged crack | Rampage, Peel the Cage, Salvo |
| `PrismsRemaining` | one intact prism run | — (metric available, unused) |
| `LifeformsKilled` | an angular creature, struck | Wildlife Liberation |
| `CombatPoints` | a gunnery reticle | Dog Fight, The Bends |

**Blank is the honest state.** No metric resolved yet (a client whose game config
is still replicating) → the `Image` is switched OFF, not left showing a white box
and not showing another mode's objective. Same rule the ability lockup's control
chip follows.

The icon is refreshed from the two places the objective ARROW's provider is
resolved — `MiniGameHUD.OnMiniGameTurnStarted` and the post-config-sync
`HandleClientReady` pass — because both answer the same question and both have to
survive a client whose mode is still arriving.

### 1.2 The art

`Tools/Build/author_objective_icons.py` (`--check`, CI-clean). Nine 256 × 256
pure-white silhouettes with the shape in the ALPHA channel, tinted at runtime
(`iconTint`, Light `E6E9FF`).

- **Line-weight monochrome, angular, zero corner radius** — matched to the
  in-game vessel-HUD ability icons (the Sparrow's bullet/fire/missile/swap glyphs,
  the Squirrel's boost-ring cross-section) and to Style Foundation §5/§9.
- **Form disambiguates before hue does** (§1.2): the three crystal metrics share
  one silhouette and are told apart by FILL — hollow / hollow + centre mark /
  solid. That read survives being seen small, in peripheral vision, and by a
  player who cannot separate the tints.
- **A HARD 0/1 shape function on a 4 × 4 grid per pixel**, so every soft edge is
  real coverage rather than a feathered distance field — the quality bar the
  offline lamp icons were rebuilt to (14.4× more accurate per edge pixel than a
  fixed-width feather).
- The generator asserts a 24 px clear margin and a plausible ink coverage per
  glyph, so a shape function that escapes its box fails the build rather than
  shipping clipped.

Every glyph was judged **at the size it renders** (40 / 64 / 150 px sheets), not
blown up — four of the first nine read as mush at 40 px and were redrawn
(`Docs/PALETTE.md` §4.3, "judge a candidate at the size it will be judged").

---

## 2. Centre — the domain score bar

```
   12       7       3        <- team score, in that team's colour
  [][]     []      []        <- that team's player icons
  ────    ────    ────       <- 3px accent, the divider
```

**One centred row divided into a column per active domain.** `DomainScoreBar`
(a `HorizontalLayoutGroup`, 740 × 100, 16 px gaps) holds up to three
`DomainScorePanel` columns of 220 × 96: score (56) over icons (34) over accent (3).

The local player's domain is **always the first column**.

### 2.1 What was removed, and why

| Removed | Reason |
|---|---|
| `Scoreboard.png` — the solid triangle + outline triangle + parallelogram plate | It was a frame around one player's own score; the bar now shows every team's, and the plate drew a boundary the column layout already states |
| The centred per-player score card | Superseded by the local player's own domain column |
| Player NAMES under the avatars | An icon already identifies a player. A name under one avatar and not the others made that column a different HEIGHT from the rest, so the row stopped reading as one divided block — and it was the only text in the bar carrying no number |
| The `DomainScorePanel` background plate (`Image` + `CanvasRenderer`) | Three columns side by side ARE the division; a plate behind each re-draws a boundary the arrangement states |

`Scoreboard` and `MultiplayerPlayerScoreCard` are **switched off, not deleted** —
`scoreDisplay` / `playerScoreContainer` stay valid references, so the legacy
per-player fallback path still resolves and nothing NREs.

### 2.2 Which column is mine, now that names are gone

The local player's avatar chip takes the domain colour at **full strength**;
teammates sit at `DomainScorePanel.teammateChipAlpha` (0.45). Style Foundation §3
("your avatar chip, team colour") expressed in the one channel that survives at
chip size.

### 2.3 The layout needs no branch in the HUD

`MultiplayerHUD` builds the local domain into `AllyDomainContainer` and the others
into `OpposingDomainsContainer`, in enum order. In the single-bar layout **both
accessors resolve to the same transform** (`MultiplayerHUDView.domainBarContainer`),
so the existing build order lays the columns out left-to-right in one row with no
new code path — and a HUD still wired the old way (two groups flanking a centred
player card) keeps working unchanged.

---

## 3. The lifeform counter shows itself

`MiniGameHUDView.UpdateLifeFormCounter` now activates its own root on a meaningful
value and hides it on empty / `"0"` — the same idiom the drone counters already use
(`MiniGameHUD.OnMoundDroneSpawned`). Only the two Wildlife Blitz turn monitors ever
raise it, and **no shipping scene wires either**, so on every domain mode it sat in
the corner reading a stale `0` inside a second ring cluster.

---

## 4. Adding a mode, or a metric

- **New mode, existing metric** → nothing to do. The bar is correct on first run.
- **New metric** → add the enum member, draw its glyph in
  `author_objective_icons.py`, run it, and add the `entries` row to
  `Resources/ObjectiveIconSet.asset`. A metric with no entry draws nothing.
- **Never** add a per-mode icon override, and never point the readout at anything
  but the mode's `ScoringRule.Metric`.

---

## 5. Known limitations

- **Tabular figures are not applied.** Style Foundation §4 wants every
  live-updating numeric wrapped in `<mspace=Xem>`; `ScoreNumberAnimator` still
  writes a bare `value.ToString()`. That is a fleet-wide change touching
  `PlayerScoreEntry` / `PlayerScoreCard` / `DomainScorePanel` and it needs T5's
  measured per-face digit advance, which has not landed. The domain scores will
  jitter by a fraction of a digit width as they tick.
- **`MiniGameHUDView.countdownDisplay` is now `{fileID: 0}`** — the `Image` it
  named was one of the deleted rings. The field has no reader and no accessor
  anywhere in the project (the 3-2-1 countdown is `CountdownTimer.cs`'s own
  field, wired to `MiniGameHUD/CountDownDisplay`, untouched). Retiring the dead
  field is a separate sweep.
- **`roundTimeDisplay` is a misnomer** — it has always carried the objective
  remaining, not a time. Renaming a serialized field means sweeping scenes,
  prefabs **and `Tools/**.py`** (see the asset-surgery skill's rename trap), so
  it is deliberately left alone here.
- **Not editor-verified.** Authored in a remote session with no Unity. See the
  PR body's *Verification status*.
