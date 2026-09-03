# The Game-Mode Top Bar

**Applies to:** every multiplayer domain mode — all 11 scenes that instance
`_Prefabs/GameCanvas-HexRace.prefab`. One prefab, one bar, no per-scene forks
(`Docs/GAMECANVAS.md` § "Shared prefabs are single sources of truth").

**Scope:** the **centre** is §1. The **left** is §2 — the goal stack, which
replaced the ring cluster. The right volume/pause button is untouched.

---

## 1. The domain score bar

```
   12       7       3        <- team score, 64px, in that team's colour
  [][]     []      []        <- that team's player icons
  ────    ────    ────       <- 3px accent, the hard bottom edge
  ~glow~  ~glow~  ~glow~     <- team-coloured light rising off the accent
```

**One centred row divided into a column per active domain.** `DomainScoreBar`
(a `HorizontalLayoutGroup`, 620 × 108, **4px** gaps) holds up to three
`DomainScorePanel` columns of **168 × 108**: score (70) over icons (30) over
accent (3).

The local player's domain is **always the first column**.

### 1.1 Light, not a plate

The column carries **no background rectangle**. Three columns packed side by side
already state the division; a plate behind each one re-draws a boundary the
arrangement makes on its own.

What it carries instead is **light** — `topbar_domain_glow`, a soft glow rising
off the accent strip, tinted that team's `BrightCrystalColor` (the same colour the
number wears, so the column reads as one lit object rather than a number on a
differently-tinted wash). Light says "this column is Jade" without adding an edge,
and unlike a plate it can **move**:

| | |
|---|---|
| **Breath** | continuous yoyo, ±28% of rest alpha over 3.2s, `InOutSine`. Starts from the DIM end so a freshly built bar brightens into view rather than fading out of it. The bar is alive while nothing is happening. |
| **Punch** | on a score change the glow jumps to full and eases back over 0.45s, so the team that just scored is the one that catches your eye. |

The punch **pauses** the breath rather than killing it, so a rapid run of scores
re-triggers cleanly instead of stacking tweens, and the light can never end up
stuck at full. Both tweens are `SetLink`ed to the panel and killed in `OnDestroy`.

The glow sprite's shape is authored, not eyeballed: a raised-cosine window
horizontally (**exactly zero at both side edges**, so a row of columns can never
show a seam) times an exponential rise from the bottom edge. Authored by
`Tools/Build/author_topbar_glow.py` (`--check`), which asserts all three
properties — zero at the edges, brightest at the bottom, monotonic up the centre.

> **The bottom-edge rule is load-bearing and easy to get backwards.** A PNG's row 0
> is the TOP, so the vertical term has to grow *with* the row index. Written the
> intuitive way round it produces an upside-down glow that looks plausible in
> isolation and is obviously wrong the moment it sits above the accent strip. The
> generator's assert is what caught it.

### 1.2 What the centre no longer has

| Removed | Reason |
|---|---|
| `Scoreboard.png` — the solid triangle + outline triangle + parallelogram plate | It framed one player's own score; the bar now shows every team's, and the plate drew a boundary the column layout already states |
| The centred per-player score card | Superseded by the local player's own domain column |
| Player NAMES under the avatars | An icon already identifies a player. A name under one avatar and not the others made that column a different HEIGHT from the rest, so the row stopped reading as one divided block — and it was the only text in the bar carrying no number |
| The `DomainScorePanel` background plate (`Image` + `CanvasRenderer`) | Replaced by the glow, per §1.1 |

`Scoreboard` and `MultiplayerPlayerScoreCard` are **switched off, not deleted** —
`scoreDisplay` / `playerScoreContainer` stay valid references, so the legacy
per-player fallback path still resolves and nothing NREs.

### 1.3 Which column is mine, now that names are gone

The local player's avatar chip takes the domain colour at **full strength**;
teammates sit at `DomainScorePanel.teammateChipAlpha` (0.45). Style Foundation §3
("your avatar chip, team colour") expressed in the one channel that survives at
chip size.

### 1.4 The layout needs no branch in the HUD

`MultiplayerHUD` builds the local domain into `AllyDomainContainer` and the others
into `OpposingDomainsContainer`, in enum order. In the single-bar layout **both
accessors resolve to the same transform** (`MultiplayerHUDView.domainBarContainer`),
so the existing build order lays the columns out left-to-right in one row with no
new code path — and a HUD still wired the old way (two groups flanking a centred
player card) keeps working unchanged.

---

## 2. The left — the goal stack

```
  ┌──────────────────────────────────┐
  │ ◈  COLLECT CRYSTALS      18/30   │   <- primary: 400x48, the win condition
  │ ▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁▁                │   <- progress hairline, reward green
  └─────────────────────────────────╱
    ┌────────────────────────────┐
    │ ◈  DESTROY PRISMS  240/2000│       <- secondary: 400x37, 60% alpha
    └───────────────────────────╱
```

Anchored top-left at **(16, −52)** as a `VerticalLayoutGroup` (spacing 6,
`ContentSizeFitter` on preferred height), so the stack grows DOWNWARD and nothing below
it moves.

**The whole top bar drops by ONE amount** (`TOP_BAR_DROP` 39), so the left and the centre
keep their relationship and neither hugs the screen edge. The size is set by the left:
`DiagnosticsHUD` builds its own `ConstantPixelSize` canvas and parks the FPS panel at
`(8, −8)` with height `TopY 8 + ~18 + Pad 10`, so it owns roughly the **first 44 screen
px**. The ring cluster's old top margin of 13 sat inside that, which is why the FPS
readout was drawn over the word "COLLECT". 52 clears it with 8 units of air; the centre
score block moves 3 → 42 by the same delta.

### 2.0 The plate is GENERATED, and that is what "crisp" means

The first cut of this stack shipped a **112×36 PNG stretched to 312×48** and read
exactly as blurry as that arithmetic predicts. The fix is not a bigger export — it is
the ability lockup's own law (`Docs/ABILITY_LOCKUP.md`): **a trapezoid has no 9-slice**,
because slanted edges do not tile, so a sprited one freezes the slant into the art and
is only crisp at the single size it was exported at. Generated, the slant is a float and
the shape is exact at any resolution and any DPI.

So the plate is a **`TrapezoidGraphic`** — the same component the lockup draws its cards
with, so the two surfaces are visibly one product rather than two people's idea of a dark
plate. `Assets/_Graphics/UI/Goals/goal_plate.png` is deleted; nothing references it.

Three parts, in sibling order, which IS draw order:

| child | what it is |
|---|---|
| **Glow** | `LockupBloom.png` (the lockup's own bloom sprite), 9-sliced, inset **−28 px** on every side so it overhangs the plate. It is the FIRST child — a bloom drawn after the plate covers the thing it is meant to light. |
| **Plate** | `TrapezoidGraphic`, top edge full width, bottom edge inset **14 px** each side (a ~16° slant), with the lockup's antialiased slant band down both slopes, wrapping 28 px onto the horizontals and grading to nothing there. |
| **Track / Fill** | the slider bed and the bar that fills over it (§2.6). |

Two numbers that look arbitrary and are not:

- **The chamfer is authored in PIXELS and converted**, because `TrapezoidGraphic` takes
  widths as FRACTIONS of the rect. The lockup's own `trapezoidInset 9` on a 104-wide card
  is 8.7% — on a bar three times as wide that fraction is 27 px of wedge, which reads as
  a parallelogram. 14 px over the 48 px height is the same *angle*, not the same fraction.
- **The bloom's `m_PixelsPerUnitMultiplier` is 0.6**, shrinking its authored 48 px 9-slice
  border to ~29 px. At 1 the two 48 px borders sum to 96 in a 104 px-tall glow, leaving a
  4 px middle — the glow reads as two blobs with a seam across it. *A 9-slice border is a
  constraint on the smallest rect the sprite can be drawn into, and a short wide element
  is exactly where that bites.*

### 2.1 Why the rings went

`RoundTime` was never a clock. **Every** turn monitor raises
`onUpdateTurnMonitorDisplay` with the metric **REMAINING** — `WildlifeKillTurnMonitor`,
`RampagePrismTurnMonitor`, `RibcagePrismTurnMonitor`, `ScarabScrambleGoalTurnMonitor`,
`NucleusRushWaveTurnMonitor`, `SalvoPrismTurnMonitor`, `CombatPointTurnMonitorBase`,
`JoustCollisionTurnMonitor` and `NetworkCrystalCollisionTurnMonitor` all send
`ScoringRule.Remaining(...).ToString()`. So BigCircle + three `JustRotate` rings + a
timer face were drawing a clock over an unlabelled objective count with no target.
The stack shows the same number with the two things the ring could not: **what you
are counting, and how many it takes.**

`LifeFormCounter` was already inactive — only `WildlifeBlitzHUD` ever wrote to it.

**Both clusters are switched OFF, not deleted.** `roundTimeDisplay` and
`lifeFormCounter` stay valid references and are still written, so nothing NREs and a
HUD wired the old way is unaffected. Re-activating the two GameObjects restores the
previous UI exactly.

### 2.2 It adds no plumbing

| What | Where it comes from |
|---|---|
| glyph | `ObjectiveIconSetSO.For(metric)` |
| label | `ObjectiveIconSetSO.LabelFor(metric)` — "Collect crystals" |
| target (denominator) | `ScoringRuleSO.TargetFor(gameData)` |
| count (numerator) | `target − remaining`, off the monitor channel the ring was already on |

Keyed on **`ScoringMetric`, never on the game mode** — that enum is already the
platform's single answer to "what is this mode scored on", so a new mode picking an
existing metric gets a correct goal line for free, and the row can never disagree
with the condition that ends the turn. A per-mode override table would re-open the
exact divergence the metric exists to close.

`TargetCount` stays `protected` (it is the extension point, overridden by all eleven
concrete rules); `TargetFor` exposes the VALUE without widening that contract.

**A metric that has not resolved yet draws nothing** rather than another mode's
objective — the same rule the ability lockup's control chip follows.

### 2.3 The clock row

Six scenes put seconds in that channel, through `TimeBasedTurnMonitor` /
`NetworkTimeBasedTurnMonitor`: Cellular Duel (single + multiplayer), Wildlife Blitz
(single + co-op), 2v2 Co-op vs AI, and BenchmarkStressTest.

**The payload cannot be told apart by looking at it, and assuming otherwise is a real
bug that shipped in this branch's first pass.** `GetTimeToDisplay()` returns
`((int)duration - (int)elapsedTime).ToString()` — a bare `"72"`, not `"1:12"` — so
every monitor on this channel publishes an integer string. A row that decided by parsing
would render seconds as an objective count in **Cellular Duel multiplayer**, which has a
time monitor *and* a scoring rule with a target.

So the monitor declares it: **`TurnMonitor.PublishesSecondsRemaining`** (virtual, false;
`TimeBasedTurnMonitor` overrides it true). `MiniGameHUD` resolves the scene's monitor once
— the same one-shot lookup `EnsureReadyButtonWiring` already makes for the controller,
twice a turn rather than per frame — and passes the answer to `GoalStack.SetObjective`.
The clock row then formats the seconds as `m:ss`, gets the `Time remaining` label and no
glyph, and shows no target and no hairline.

A payload that is a count the stack cannot NAME (config not yet synced, or a rule with no
target) draws **nothing** — an unlabelled number under a borrowed label is the thing the
ring was retired for.

### 2.4 Both canvases, because the fork is real

`GameCanvas-HexRace.prefab` is a hard **copy**, not a variant, so propagation is
severed (`Docs/GAMECANVAS.md`) — and it is the one **12** domain scenes instance
(HexRace, Rampage, Crystal Capture, Dog Fight, Salvo, Ribcage, Bends, Joust, Scarab
Scramble, Astro League, Nucleus Rush, Wildlife Liberation), against **10** for
`CORE/GameCanvas.prefab`. Authoring only the latter would have shipped a feature
invisible in every modern mode. `Tools/Build/author_goal_stack.py` does both, resolving
its anchors by NAME and script guid rather than by literal fileID because the two
canvases number everything differently.

### 2.5 Known gap — secondary goals have no producer

The stack authors **three** rows and the layout handles any number, but a
`ScoringRuleSO` names exactly ONE objective: the one that ends the turn. Rows 2 and 3
therefore ship INACTIVE and nothing fills them. `GoalStack.SetGoals(IReadOnlyList<GoalEntry>)`
is the seam a mode-authored list plugs into — that list is the actual work, and it is
not done here.

### 2.5.1 The row is sized to the widest label it can be asked to show

The row shipped at **312 wide with a 128-unit label box**, and 6 of the 10 authored
objectives did not fit — measured against the shipped `ChakraPetch-Regular.ttf` at font
16:

| label | units | |
|---|---|---|
| COLLECT OMNI CRYSTALS | 186.3 | wrapped |
| COLLECT ELEMENTALS | 165.6 | wrapped |
| COLLECT CRYSTALS | 144.5 | wrapped |
| PRISMS REMAINING | 142.2 | wrapped |
| DESTROY PRISMS | 128.8 | wrapped |
| HUNT LIFEFORMS | 128.1 | wrapped |
| TIME REMAINING | 120.5 | fit |
| JOUST RIVALS / SCORE GOALS / SCORE HITS | ≤ 104 | fit |

A wrapped label does not look like an overflow — it looks like **two goals**. So the row
is **400 wide** with a 196-unit label box (the widest label plus air) and a 132-unit value
column ("1997/2000" needs 124.6 at font 22, and Rampage / Ribcage / Salvo all run to 2000;
120 was not enough either).

**Word wrapping is OFF**, and `author_goal_stack.py` asserts the fit against the shipped
TTFs and the shipped `ObjectiveIconSet.asset` before it writes anything. The two go
together: wrapping off means an overflow is loud instead of quietly becoming a second
line, and the assert means it is caught at author time instead of on screen. Adding a
longer objective label fails the build until the box is widened or the label shortened.

### 2.5.2 The bloom pulses when the objective advances

The plate's bloom flares to full and eases back to its rank's rest alpha whenever the row's
count goes UP. It is the same punch `DomainScorePanel` gives the score columns, and it
carries that section's lesson: **kill and re-fire rather than stack**, or a burst of scores
leaves the plate stuck lit instead of pulsing three times.

Two differences from the columns, both deliberate. There is **no breath** — the columns
breathe so an idle bar is alive, but the goal row is a readout and a permanently moving
glow beside a number reads as an alert; here the pulse means *something just happened*.
And the punch runs on **unscaled time**, because a score can land on the frame a mode
freezes the clock.

**An increase is what a score is — not a write.** The stack rebuilds the row on every
monitor tick with the same value, so `GoalRow` remembers the last count it DREW and only
punches when the new one exceeds it. `-1` means "nothing drawn yet", so *arriving* at a
value never pulses: a scene re-entry, or the target resolving late over the network, is not
something the player just did. `Hide()` and `ShowText()` reset it, so a new turn cannot
punch off the old total and a clock row leaves no stale count behind.

The one place this needs care is `Apply()`, which re-applies rank styling **every tick**:
writing the glow's alpha unconditionally there would stomp the punch one tick after it
started and the pulse would never be seen. It holds the live alpha while a punch is playing.

### 2.6 The bar is a SLIDER, so it needs a bed

The progress bar is a `Filled` Image over a dim `Track` of the same rect. Without the
track a run at **0/30 draws nothing at all**, so the bar reads as missing rather than as
empty, and the first crystal makes a bar appear out of nowhere instead of moving one. The
bed is what makes 0 a state you can see.

Track and fill share ONE rect, derived once in the generator (`BAR_L/BAR_R/BAR_Y/BAR_H`),
so the bar can only ever sit inside its own bed.

Its inset is **26..374 rather than the plate's full width**, and that is the chamfer's
doing: the slant is widest at the BOTTOM of the plate, which is exactly where the bar
lives, so the clearance has to be measured at the bar's own top edge (x = 11.1) rather
than at the plate's waist. `author_goal_stack.py` asserts it arithmetically before it
writes anything — move the chamfer or the bar and the generator fails rather than
shipping a bar the plate clips.

**The glow is per-rank, not per-state.** `GoalRow` drives the bloom's alpha — 0.30 for the
win condition, 0.12 for context — on top of the `CanvasGroup` that already dims a
secondary row. The lockup's bloom is a STATE (it lights on upgrade); here it is rank, and
the primary row is the one worth lighting because it is the one you are chasing.

## 3. Adding a mode

Nothing to do. The bar reads `GameDataSO.GetDomainMetricSum` per domain, which every
domain mode already publishes.

---

## 4. Known limitations

- **Tabular figures are not applied.** Style Foundation §4 wants every
  live-updating numeric wrapped in `<mspace=Xem>`; `ScoreNumberAnimator` still
  writes a bare `value.ToString()`. That is a fleet-wide change touching
  `PlayerScoreEntry` / `PlayerScoreCard` / `DomainScorePanel` and it needs T5's
  measured per-face digit advance, which has not landed. At 64px the jitter is
  more visible than it was at 44px.
- **Not editor-verified.** Authored in a remote session with no Unity. See the
  PR body's *Verification status*.
