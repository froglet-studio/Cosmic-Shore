# Stoat play styles — five ways to fly the dipole sling

Status: **prototype in the Stoat Flight Studio** (round 9; retuned in round 12 from the sim lab). The game still runs the orbit sling; nothing
here is in `Assets/` yet. Decisions recorded 2026-10-09 with the designer:

- **The five styles.** Comet, Needle, Anchor, Maelstrom and Flare.
- **The element links.** Time → Comet, Space → Anchor, Mass → Maelstrom and Charge → Flare. Needle is
  earned by skill.
- **How the weights move.** They rise, then drift back to 0.5.
- **The scope.** The sling and the holes only; the Stoat's own flight is never touched.

## 1. Why five styles

One mechanic serves different players only if it can lean toward what each of them came for. The five
styles follow the motivations that recur in player-motivation research (thrill, mastery, comfort, power,
spectacle). Each is a direction the same sling can lean.

| Style | Player motivation | The promise |
|---|---|---|
| **Comet** · velocity | thrill / excitement | "the sling never slows me down" |
| **Needle** · precision | mastery / challenge | "I choose my exit to the degree" |
| **Anchor** · control | comfort / low anxiety | "it catches me, it won't throw me into the hole" |
| **Maelstrom** · gravity | power / domination | "I bend the world around me" |
| **Flare** · surge | spectacle / burst | "every sling is an explosion" |

## 2. The blend

Each style is a list of changes to the sling and hole settings. A weight `w` in [0, 1] applies them by
`s = 2(w − 0.5)`:

- `s = +1` (w = 1) applies them in full;
- `s = 0` (w = 0.5) applies nothing, so all five at 0.5 is exactly the base tuning;
- `s = −1` (w = 0) reverses them, so Comet at 0 is steadier and slower.

For every setting: `effective = clamp(base + Σ sᵢ·Δᵢ, slider range)`. Styles add, so two styles can push
the same setting (Comet and Flare both lengthen or sharpen the boost).

### What each style changes (Δ at w = 1)

Retuned in round 12 against the sim lab's scorecard (`STOAT_SIM_LAB_PLAN.md` §2), so each style wins the
one thing it was built for. The momentum rows are new in round 12.

| | Comet | Needle | Anchor | Maelstrom | Flare |
|---|---|---|---|---|---|
| **Squeeze curve** | | +0.6 (finer at light squeezes) | | | |
| **Laid ahead / to the side (u)** | ahead +60 | | side −10 | side +15 | |
| **Strength, touch / buried** | — / −3 | | +1.5 / — | +1 / +6 | |
| **Horizon, touch / buried (u)** | | −0.4 / −1.5 | −0.3 / −2 | +0.8 / +3 | |
| **Catches within (× circle)** | | | +1 | | |
| **Catches off the nose by (°)** | −10 | +15 | −30 | +20 | |
| **Settles onto the circle (/s)** | +1.5 | +8 | −0.8 | | |
| **Engine holds speed in grip (/s)** | | +2 | +2.5 | −1.8 | |
| **Longest hold (°)** | −90 | +360 | +180 | | |
| **Release boost, touch / buried (× speed)** | +0.05 / +0.1 | −0.15 / −0.4 | | | +0.9 / +2.1 |
| **Boost fade (s)** | +0.6 | −0.4 | | | −0.7 |
| **Release kept as momentum (share)** | +0.45 | −0.35 | | | −0.3 |
| **Momentum fades (s)** | +6 | | | | |
| **Most momentum (× cruise)** | +1.5 | −1 | | | |
| **Force ceiling (u/s²)** | | | −1500 | | |
| **Fastest bend (rad/s)** | −4 | | | −2 | |
| **White hole push** | | | −0.2 | +0.4 | |
| **Prism reach (pull felt down to)** | | | | −0.3 | |
| **Prism spaghettify gain / limit** | | | | +1.2 / +3 | |
| **Lens bend / photon ring glow** | | | | +0.6 / +0.4 | |
| **Pair closes at (u/s)** | +20 | | −15 | | +40 |
| **Longest hold (s)** | | | +6 | | |
| **Birth: black hole / white hole (s)** | −0.3 / −0.35 | −0.3 / — | | | −0.4 / — |
| **Black hole sinks (s)** | | | | | −0.3 |
| **White hole's light leaves at (u/s)** | | | | | +250 |
| **Light shells bend the sky (u)** | | | −3 | | +6 |
| **Gravity wave strength (u)** | | | | | +12 |
| **Prisms pulse in the wave** | | | | +0.4 | +0.6 |
| **View stretches as it passes you** | | −0.3 | −0.6 | | +1 |

In words:

- **Comet** turns its kicks into lasting momentum with a high ceiling, but its holes are lighter and it bends
  slower.
- **Needle** gives up momentum for an exact line: fine squeezes, a fast settle onto small circles, a gentle
  release.
- **Anchor** catches from twice as far and almost as soon as the hole is off the nose, and it holds you longer.
- **Maelstrom** lays the heaviest holes, with the widest pull on the prisms.
- **Flare** spends everything on the punch: up to 3 × speed at a full squeeze, gone in under a second.

### Measured in the studio (round 12 scorecard)

Each preset at 1 and the rest at 0.5, on seed 7, intensity 2, 2 laps. A skilled AI flew squeezes 0.3–1 on
both courses; a rookie AI (sloppiness 0.8) flew three seeds. Each column is the best the skilled AI reached
going for that one thing.

| Preset | avg speed | top speed | ring error, hairpins | rookie caught | prisms per sling |
|---|---|---|---|---|---|
| Balanced | 108 | 228 | 2.2 u | 88% | 979 |
| Comet | **168** | 329 | 6.3 u | 21% | 690 |
| Needle | 67 | 110 | **1.5 u** | 74% | 991 |
| Anchor | 108 | 209 | 2.0 u | **100%** | 970 |
| Maelstrom | 110 | 191 | 2.2 u | 94% | **1,859** |
| Flare | 148 | **470** | 5.0 u | 83% | 978 |

(The round-9 measurements in the git history were taken before momentum carry and the retune.)

## 3. How a race moves the weights

- **Start.** Each race starts all five weights at 0.5.
- **Crystals.** An element crystal raises its style by the crystal step (0.15):
  - **Time** (rate / mobility) → Comet;
  - **Space** (reach / presence) → Anchor;
  - **Mass** (size / volume) → Maelstrom;
  - **Charge** (threat / energy) → Flare.

  These follow the locked element conventions (`Docs/ElementalAbilitySystem/ARCHITECTURE.md`).
- **Needle is earned, not collected.** A clean release raises it by 0.1. A clean release means: you let go
  yourself, out of the grip, with your nose within 20° of the next ring. Precision has to come from skill;
  a pickup cannot grant it.
- **Drift.** Unfed weights drift back toward 0.5 (0.04 /s), so a player's style reflects how they are
  playing now. A player who loves speed keeps taking Time crystals and stays a Comet.

All of these numbers are sliders in the studio's **Play styles** card. **Live** turns the race dynamics on;
off, the weights stay where you set them, which is how to test a preset.

## 4. Taking it into the game (after the studio round is approved)

1. **`StoatPlayStylesSO`** holds the five styles as data: name, element, and a list of
   `{ setting, Δ at full }`. It also holds the crystal step, drift, Needle step and clean cone. Per-mode
   values go in config, never in code.
2. **Weights from the element levels.** Each element's one parameter on the Stoat becomes its style weight:
   an `ElementalFloat` reading 0.5 at the resting level and 1 at level 10, through `EvaluateLive`. This keeps
   "one parameter per element", and the weights are networked for free, because element levels already
   replicate. Drift then rides the element's own decay, if the economy decays levels; otherwise a decay on
   the weight. Either way, nothing holds a level above 10 (the maintained-mechanism law).
3. **Needle** is a server-side skill score on `StoatSlingExecutor`, replicated with a `NetworkVariable`. It
   is raised on a clean release, judged against the next gate from the course's turn monitor.
4. **One read site.** The executor and the black-hole spawn read effective values via
   `StoatPlayStyles.Effective(field)`, never a cached blend, so a crystal taken mid-orbit applies at once.
5. **Decision needed before porting (the vessel contract).** The fleet contract is four abilities, each
   owned by one element, shown as four HUD icons. Today the Stoat's map declares Space → Slingshot and
   Time → Hold Still, and Charge and Mass are open slots. This plan makes all four elements lean the one
   sling. Either the four styles count as the sling's four elemental facets, with four icons showing the
   four weights, or Hold Still keeps Time and Comet needs another source. That is a call for the designer,
   and FLEET_MAPS records it.
