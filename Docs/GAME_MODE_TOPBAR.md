# The Game-Mode Top Bar

**Applies to:** every multiplayer domain mode — all 11 scenes that instance
`_Prefabs/GameCanvas-HexRace.prefab`. One prefab, one bar, no per-scene forks
(`Docs/GAMECANVAS.md` § "Shared prefabs are single sources of truth").

**Scope:** this pass changes the **centre** of the bar only. The left objective
readout and the right volume/pause button are untouched.

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

## 2. Objective icon art — present, deliberately unwired

`Assets/_Graphics/UI/Objectives/` holds **nine line-weight monochrome glyphs**, one
per `ScoringMetric`, authored by `Tools/Build/author_objective_icons.py` (`--check`).

**Nothing reads them.** A first pass replaced the left ring cluster with an
icon-plus-count objective readout; that feature was **removed** and the left side
restored byte-for-byte. The art was kept on request, so a later pass has it ready.

If it is revived, the design that was built and is worth keeping:

- Key the glyph on **`ScoringMetric`, never on the game mode**. That enum is
  already the platform's single answer to "what is this mode scored on", so a new
  mode picking an existing metric gets its icon free; a per-mode override table
  would re-open the exact divergence the metric exists to close.
- The number needs **no new plumbing** — every turn monitor already raises
  `onUpdateTurnMonitorDisplay` with the metric REMAINING, into the `TMP_Text` the
  view writes as `roundTimeDisplay`. (Which also means that field is a misnomer:
  it has never carried a time.)
- **Form disambiguates before hue** (Style Foundation §1.2): the three crystal
  metrics share one gem silhouette and are told apart by FILL — hollow /
  centre-marked / solid.

Reviving it needs a reader (a component that resolves the sprite from the live
`ScoringRule.Metric`) and a catalogue asset; both were written and removed with the
feature, and are recoverable from this branch's history.

---

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
