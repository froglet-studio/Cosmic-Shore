# Tapestry (`GameModes.Tapestry = 60`)

The Butterfly's **MASS** game — a **timed** painting war. The Butterfly is the hull that makes the
2D prismscape, and on it **Mass is the width of the wake**: Mass mode lays keys 5× the narrow line
at Mass 0 and 20× at Mass 15. The score is the prism **volume a domain has standing when the clock
runs out**, so the right trigger is the whole game: **Mass mode paints**, **Dust mode raids**, and
you cannot do both at once.

One of the Butterfly's four element games: **Waystation** (Time, a race), **Dustup** (Charge,
working pilots over), **Sirocco** (Space, destroying mass), and this (Mass, making mass).

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameTapestry.unity` |
| Controller | `TapestryController : MultiplayerDomainGamesController` |
| Metric | `ScoringMetric.VolumeRemaining` (12) — **new**, see below |
| Rule | `TapestryScoringRuleSO` — timed, highest wins, `IsObjectiveReached` never fires |
| Turn monitor | `TapestryTimeTurnMonitor : NetworkTimeBasedTurnMonitor` |
| Round length | `EndConditionOverridesSO.tapestryRoundSeconds` (**150**) |
| Comeback | rate **0.00002** per unit of volume — derived, see below |
| Arena | the bare **Barren** cell (no environment, no flora, no fauna) |
| Objective arrow | `BendsObjectiveProvider` — the nearest rival, whose freshest paint is behind them |
| Generator | `Tools/Build/author_tapestry_assets.py` (`--check`) |

## The loop

1. **Paint.** Mass mode (the Butterfly spawns in it) lays a wide key every 9 units of flight.
   Everything you leave standing is score. Fly broad arcs; the keys are a surface, not a line.
2. **Grow the brush.** Elemental crystals are scattered through the cell at the start. A **Mass**
   crystal widens the wake; at **Mass 5** (*Gilded Wake*) Mass mode lays **shielded** keys, which a
   rival's dust can only shed, not take.
3. **Raid.** Flip to Dust mode (right trigger) and fly **over** a rival's painting. The dust rolls
   one outcome per prism it touches — destroy, shrink, or **steal** — deterministically per prism,
   so every peer agrees. A steal moves the volume straight onto your score: it is worth twice a
   destroy. Dust on your **own** paint grows it, arms it (danger) or shields it.
4. **The whistle.** The domain with the most volume standing wins.

## Why a new metric, and why timed

**The score has to be volume, not a count.** `PrismsRemaining` already existed and is the obvious
candidate, and it deletes the mode: the Butterfly lays one key per wavelength whichever mode it is
in, so a count pays a narrow Dust-mode line exactly as much per second as a 5×-20× Mass-mode brush.
`VolumeRemaining` is the volume twin (`IRoundStats.VolumeRemaining`, already replicated and already
maintained platform-wide by `StatsManager`: `+` on lay, on growth — the prism's growth delta is
credited to its owner — and on steal; `−` on destroy, on shrink and on being stolen from). The
metric is one enum member and one `ScoringMetrics.Read` row; genre **Mass** (`ModeGenre`), objective
label "Hold volume".

**It is a LIVE stock, so the mode is timed.** Every other domain race counts up and can only count
up; this one falls when a rival raids you, so a first-past-the-post target would end a match on a
number that was true for one frame. "Most standing at the whistle" is the only end condition that
means what it says. Bloomrush is the shape (timed, points mode, `UseGolfRules = false`); the round
length lives in `EndConditionOverridesSO` and `TapestryTimeTurnMonitor` reads it at `StartMonitor`
on every peer, so it is not a per-scene field.

## No food web — deliberately

The arena is the Barren cell for Hijack's reason. With fauna in a cell, herbivores graze whatever
the controlling colour does not own, and the leader's colour is by definition the most abundant —
so a swarm would preferentially eat whatever the **trailing** team just painted, an anti-comeback
current in a mode whose whole economy is contested mass. Here the only forces that remove mass are
the pilots' own dust.

Because Barren grows no lifeforms, no hearts drop, so element progression comes from a **crystal
scatter** (Bloomrush's recipe — peer-local and deterministic off a seed). Its size is the
**intensity** dial: **24 / 16 / 10 / 6** crystals. Plenty of progression at 1 (wide brushes, shields
early); scarce at 4 (a narrower, more contested board).

## The comeback rate is derived from the hull

`bonusLevels = deficit × rate`, and here the deficit is a volume, so the rate is a function of how
much volume the Butterfly paints. The generator reads the wake off the **shipped** assets —
`BaseScale.x × XScaler / 2 × massModeWidth.Min × BaseScale.y × BaseScale.z` = **265 volume per key**,
`topSpeed / initialWavelength` = **7.44 keys/s**, so **~1,974 volume/s** per painting pilot — and
sizes the rate so a domain a quarter of a reference match behind (two pilots painting half of a
150 s round, ~296k) buys ~1.5 element levels. A vessel retune moves the rate with it, or fails the
assert.

## AI

On a slow clock (6 s — a mode switch costs the wake's 1.5 s blend each way) each AI decides:
**raid** while its domain trails the best rival by more than 10%, and in the last 25 s regardless;
**paint** otherwise. Painting is a slow orbit of the cell on the bot's own great circle (so two
painters do not lay one arc twice), detouring for any elemental crystal within 450 units. Raiding
flies **above** the densest opposing mass (`Cell.GetExplosionTarget`) so it passes through the dust
hanging below the hull. The mode switch is `ButterflyAutopilotModeDriver` (see `DUSTUP.md`) — a
replicated press, read back rather than counted.

## Verification status

**Authored headless; nothing has been opened in the editor.** Proved: the generator's `--check`, the
wake model's arithmetic against the shipped assets, the scene clone's diff against its donor (the
identity, the monitor's `duration`, the four AI hulls, the spawn ring, the crystal volume and the
cell), and the standing out-of-editor gates. Not proved: that it plays, and the C# has had a syntax
pass and an API-surface read but no type check.

## Known limitations

- **Six-figure scores.** A domain's standing volume reaches the hundreds of thousands, and the HUD's
  domain columns were laid out for counts. The goal row is a clock (the monitor publishes seconds),
  so it is unaffected; the columns may clip. If they do, the fix is a display unit on the metric,
  not a smaller score.
- **The paint rate model assumes full speed and resting Mass.** Boost scaling on the key
  (`ApplyBoostScale`) and in-flight growth are not modelled; the comeback is conservative against
  both, since either only makes volume bigger.
- **The AI's raid target is the DENSEST opposing mass, not the nearest.** A bot may cross the whole
  cell to raid. `Cell.GetExplosionTarget` is the query the platform has.
- **Card art** is rendered by `/cardart` (MODEL tier: arcs of wide keys in two domains across the bare cell,
  with one raided gap). It is staging, not a screenshot.
