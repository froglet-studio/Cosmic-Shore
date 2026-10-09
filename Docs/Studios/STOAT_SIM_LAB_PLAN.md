# Stoat sim lab — what the AI measured, and the plan from here

Status: **studio round 14** (2026-10-09; the AI and the course ladder were reworked in round 14: §2, "The intensity ladder and the round-14 AI"). Nothing here is in `Assets/` yet; the game still runs the orbit
sling. Companion docs: `STOAT_PLAY_STYLES.md` (the five styles) and `README.md` (the studio rounds).

## 1. The sim lab

The studio's **Sim lab** card lets an AI fly the Stoat while you change settings and watch.

- **The AI uses inputs only.** It writes the same dual-stick mix and analog triggers a player does, never
  the hull or the sling state. This is the rule the game's AI already follows (`AIPilot`, `SkimRacePilot`,
  enforced by `Tools/Build/check_ai_no_state_writes.py`).
- **How it steers.** It pursues a point just before the next ring, along the ring's normal, so it threads
  each ring square. It uses full throttle, less whatever yaw costs on the shared x axes, and rolls to stay
  level.
- **When it slings.** It lays a pair on a long straight (`aiMinRing`) or before a turn sharper than
  `aiTurn`, measured in 3D, and holds the trigger at `aiQ`.
- **Bank to sling (round 14).** The pair is always laid along the wings, so for a turn the AI first **rolls**
  until the turn lies off one wing. A climb or a dive becomes a turn to the side. Then it lays the pair on
  that side.
- **Into the catch.** Until the hole has it, the AI steers to pass the black hole **on its hold circle**.
  Steering for the ring would fly it out of reach, and at Comet's speed the circle is so small that flying
  straight past misses it.
- **The release.** It lets go when its nose is within `aiCone` of the next ring, or the moment the nose
  stops closing on it, whichever comes first. At a tight circle's swing rate a narrow cone is only a frame
  or two wide, and missing it costs a whole lap round. It gives up after `aiGiveUp` seconds if the hole
  never catches it.
- **The rookie.** `aiNoise` makes the AI sloppy:
  - it squeezes off the mark (±0.45 at 1);
  - it lays the pair up to 1.2 s late;
  - it lets go up to 45° off;
  - from 0.5 up, it keeps steering for the ring before the catch, the classic miss.
- **Cameras.**
  - Chase.
  - Follow: wide and level, like the menu's lava-lamp rig.
  - Free: drag to orbit the Stoat, wheel to zoom.
- **Speed and repeat.** The sim runs at 1×, 2× or 4×. The AI restarts each race on its own.
- **Score all five styles.** This runs ~80 races headless in a few seconds and fills the scorecard below.
- **Courses.** The Course tab has two kinds:
  - **Circuit**: 8 rings on a four-level ladder (§2, "The intensity ladder and the round-14 AI").
  - **Hairpins**: 8 rings that alternate direction 120–190 u apart, then a long straight home; it rewards
    tight, accurate turns.

## 2. What it found

### Why high speed was hard

- **The boost faded too fast.** The release boost faded out within its 1.5 s boost time. Speed never stacked
  from sling to sling, and with one pair at a time you could not sling often enough to stay fast.
- **Measured before the fix.** A perfect AI on the shipped tuning averaged **70 u/s** (cruise 60) and topped
  out at **120 u/s**.

### The fix: momentum carry

Each release now keeps a share of its kick as lasting **momentum**, and the kick scales with the speed you
already carry, so a chain of slings stacks. Three new sliders control it:

| Slider | Shipped | Meaning |
|---|---|---|
| `dpCarry` | 0.4 | Share of the kick kept as momentum; the rest is the old punch that fades |
| `dpFlowFade` | 5 s | How long momentum takes to fade |
| `dpFlowCap` | 1.5 × cruise | The ceiling a perfect chain climbs to |

The HUD shows the momentum next to the gravity readout.

### The physics the AI exposed: speed is punished at the catch, not in the turn

- **In the grip, faster means tighter.** A circular hold needs `GM = v²(r − rs)²/r`, so at a higher speed the
  hole holds you on a **smaller** circle (Newton: `r = GM/v²`). Speed is never punished in the turn itself.
- **At the catch, faster means harder.** The capture reach is a multiple of that circle, so a fast Stoat has
  a much smaller window to be caught.
- **The cost lands on rookies.** A rookie Comet is caught on **21%** of its slings, against 88% on Balanced.
  Comet is the expert's style. Nobody designed this; it falls out of the physics.
- **Precision costs speed, also naturally.** The faster you fly, the further off centre you thread a hairpin
  ring (Comet 6.3 u, against Needle 1.5 u).

### A design hole: the tap sling

**The best AI play is often a tap.** It gets caught, its nose already points at the next ring, and it lets go
at once: "SLINGSHOT · 0° round". The release kick does not depend on how far round you went, so on a circuit
the fastest play skips the swing entirely. That contradicts "hold time = rotation" (round 6).

**The test slider.** `dpKickSweep` (Dipole sling card; 0 = off = the shipped rule) makes the kick earned. A
release that was never caught gives no kick, and the full kick needs that many degrees round. Measured:

| `dpKickSweep` | Balanced avg | Balanced 2 laps | Comet avg | Comet 2 laps |
|---|---|---|---|---|
| 0 (shipped) | 108 u/s | 1:08.8 | 168 u/s | 0:44.9 |
| 90° | 74 u/s | 2:22.4 | 126 u/s | 1:17.7 |
| 180° | 70 u/s | 2:22.4 | 124 u/s | 1:22.3 |

(No sling: 56 u/s, 2:11.5.)

**With an earned kick, Balanced slinging is slower than not slinging at all.** A swing round the hole costs
more time than its kick gives back. Before the kick is made earned, the swing itself has to pay. Options:

- a bigger kick per degree;
- turns that save distance on the course (the hairpins already show this);
- momentum that grows with the swing.

**Decision needed (designer):** keep the tap (fast and skill-light), or make the swing earn the kick and
retune it to pay. Every number in this doc is with the tap allowed (`dpKickSweep` 0).

### The scorecard (round 12 AI; seed 7, intensity 2, 2 laps, each style at 1 and the rest at 0.5)

Bold marks the style that owns the column.

| | avg speed, circuit | top speed | ring error, hairpins | rookie slings caught | prisms pulled per sling | circuit 2 laps | hairpins 2 laps |
|---|---|---|---|---|---|---|---|
| No sling | 56 | 60 | 2.8 u | — | — | 2:11.5 | 1:22.1 |
| Balanced | 108 | 228 | 2.2 u | 88% | 979 | 1:08.8 | 1:11.1 |
| Comet | **168** | 329 | 6.3 u | 21% | 690 | **0:44.9** | **0:41.1** |
| Needle | 67 | 110 | **1.5 u** | 74% | 991 | 1:50.0 | 1:19.2 |
| Anchor | 108 | 209 | 2.0 u | **100%** | 970 | 1:08.4 | 1:14.1 |
| Maelstrom | 110 | 191 | 2.2 u | 94% | **1,859** | 1:07.8 | 1:12.1 |
| Flare | 148 | **470** | 5.0 u | 83% | 978 | 0:53.2 | 1:05.4 |

**Each style wins exactly the column it was built for**, and loses something real elsewhere:

- **Comet**: the fastest race, but the hardest catches.
- **Needle**: the most accurate, but the slowest.
- **Anchor**: never drops a sling, but has no speed edge.
- **Maelstrom**: bends twice the world, at middling speed.
- **Flare**: the highest peaks, which fade.

### The intensity ladder and the round-14 AI

The circuit's four intensities now grade the **turning**, not only the ring size:

| Intensity | Course | Rings |
|---|---|---|
| 1 · Plain circle | flat 600 u circle, every ring square to the line | 48 u |
| 2 · Tilted rings | wanders a little; rings tilt ~40° off the line | 40 u |
| 3 · Climbs & sharp tilts | tighter rolling circle; rings tilt ~65°, every third pitched ~30° | 32 u |
| 4 · Side-on & dives | rings nearly side-on (~82°), dive rings pitched ~65° | 26 u |

Measured on seed 7, 2 laps, best of squeezes 0.5–1. "Grip turn" is the total degrees the black hole swung
the Stoat round over the race.

| | No sling | Balanced | Balanced grip turn | Needle | Comet |
|---|---|---|---|---|---|
| 1 | 2:07.5 | 1:34.7 | 766° | 1:53.7 | **0:34.3** |
| 2 | 2:06.6 | 1:34.4 | 1,382° | 2:00.0 | **0:45.9** |
| 3 | 1:59.8 | 1:58.1 | 2,420° | 2:01.9 | **1:18.4** |
| 4 | 1:50.9 | 1:50.5 | 2,209° | 2:18.5 | 1:20.5, but 3 of 4 runs DNF |

Every sling was caught at every level for Balanced and Needle.

- **The ladder works.** The harder the course, the more of the turning the black hole does: Balanced's grip
  turn rises from 766° to about 2,300°.
- **Speed and a hard course do not mix.** Comet dominates the plain circle, but at intensity 4 it fails to
  finish three runs in four: it overshoots side-on rings at 200 u/s and keeps going round for them.
- **Steering alone stays competitive at 3–4.** At cruise speed the Stoat steers so tightly (radius ~30 u)
  that the no-sling AI matches Balanced there. The sling's turning pays when you are carrying momentum,
  which is the design tension.

**The round-14 scorecard** (intensity 2): each style still wins its own column.

| | avg speed | top speed | ring error, hairpins | rookie caught | prisms per sling |
|---|---|---|---|---|---|
| Balanced | 84 | 160 | 1.2 u | 87% | 995 |
| Comet | **191** | 413 | 10.4 u | 39% | 675 |
| Needle | 64 | 103 | **0.6 u** | 72% | 1,002 |
| Anchor | 81 | 157 | 1.1 u | **100%** | 992 |
| Maelstrom | 86 | 193 | 0.9 u | 96% | **1,832** |
| Flare | 142 | **536** | 1.6 u | 80% | 944 |

To keep Flare's peak above Comet's once Comet catches reliably, Flare's buried boost rose from +2.1 to +3.1
(the slider now goes to 5).

**At intensity 4, Anchor's rookie edge disappears.** A rookie Maelstrom is caught as often as a rookie Anchor
(93% vs 91%), because the bigger holes forgive the hard geometry too.

### How fast can a very skilled player go?

Same AI, best squeeze per combination:

| Weights | avg u/s | top u/s | circuit 2 laps |
|---|---|---|---|
| No sling | 56 | 60 | 2:11.5 |
| Balanced (shipped) | 108 | 228 | 1:08.8 |
| Comet 1 | 168 | 329 | 0:44.9 |
| Comet 1 + Flare 1 | 221 | **719** | 0:39.3 |
| Comet 1 + Flare 1 + Anchor 1 | **234** | 608 | **0:34.1** |

So a very skilled player can sustain about **4× cruise** and peak near **12× cruise**. Adding Anchor makes
that player faster, not slower: when every sling catches, the momentum chain never breaks.

## 3. One game mode where all five can win

Five separate modes would mean five scenes, five generators and five configs, and each would favour one
style. Instead, build **one race whose score has a slot for each style**, on a course with a section for
each:

| Section | What it rewards | Who it favours |
|---|---|---|
| Straights with speed traps | instantaneous speed at the trap = a time bonus | Flare (bursts), Comet |
| The whole lap | raw time | Comet |
| Needle gates (small rings inside the big ones) | threading the inner ring = a time bonus | Needle |
| Prism fields | prisms pulled by your hole = crystals → a time bonus | Maelstrom |
| A broken sling (released uncaught) | a time penalty | Anchor (never pays it) |

The final time is raw time minus bonuses plus penalties. Which style wins then depends on the course mix
and how well it is flown, which is exactly the property we want.

**Element crystals still steer the weights mid-race** (`STOAT_PLAY_STYLES.md` §3), so a player can change
style to match the next section.

**Next studio round:** add these sections and the bonus scoring to the stand-in course. The scorecard can
then answer "with this course mix, does each style win somewhere?" before any scene is built.

## 4. Studio or Prisma?

**Keep prototyping in the studio until the numbers settle, then port once.**

| | Studio (web) | Prisma / Unity |
|---|---|---|
| A tuning pass | a slider, seconds | an edit, a recompile, a play-mode run |
| 80 races | 3–5 s headless | minutes in-engine, or a batch runner to build first |
| Phone test, share a link, decision log | built in | a build per device |
| Fidelity | a stand-in flight model, stand-in course, screen-space lens | the real vector flight model, real prisms, real network, real HUD |
| What it proves | the **shape** of the mechanic and the **relative** tuning | the shipped behaviour |

- **Do not import the studio into Prisma.** They are different runtimes (three.js vs Unity). What crosses
  over is the **numbers** (a `StoatPlayStylesSO` and `StoatSlingConfig` preset authored from the studio's
  shipped values) and the **tests** (the scorecard's assertions become edit-mode tests and a benchmark).
- **Per-vessel studios: yes, with a shared shell.** The shell is everything not specific to the Stoat:
  - flight, cameras and the course;
  - the AI pilot and the scorecard;
  - tooltips, types, the decision log and phone play.

  Each vessel then adds one module for its ability. Build the shell by extracting it from this studio when
  the second vessel needs one, not before.

## 5. Taking the AI into the game

1. **`StoatSlingPilot`**, an input-only pilot beside `SkimRacePilot`. It reuses the SkimRace driver's
   pursuit steering and adds the sling policy from §1, writing LT/RT through `InputStatus`.
   `check_ai_no_state_writes.py` must pass.
2. **Personas as config.** A `StoatPilotPersonaSO` per style holds:
   - `aiQ`, `aiCone`, `aiTurn` and `aiMinRing`;
   - a sloppiness value for difficulty tiers. This also fixes `AIPilot`'s no-op skill level, where every
     Low/High pair is equal.
3. **Tuning.** A benchmark runner like `SkimRaceBenchmarkRunner`, and cross-entropy search over the persona
   fields, like `Tools/Build/author_skimrace_ai_config.py`.
4. **Watching.** AI Stoats fly the menu freestyle under `MainMenuCameraController`. The LavaLamp orbit,
   ChaseTight and CinematicTrail rigs give the same three views as the studio's Follow, Chase and Free
   cameras.

**Before any of this ships,** settle the vessel contract question in `STOAT_PLAY_STYLES.md` §4.
