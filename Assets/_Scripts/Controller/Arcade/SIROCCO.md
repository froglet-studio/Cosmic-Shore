# Sirocco (`GameModes.Sirocco = 61`)

> **Flown by the Tiger Moth since 2026-10-29**, when it replaced the Butterfly (`R_VesselActions/TIGER_MOTH.md`): the
> same airframe, dust and modes. "Butterfly" below
> means that airframe.

The Butterfly's **SPACE** game — an erosion race through Rampage's cactus forest. On the Butterfly
**Space is the reach of the Scale Dust**: the capsule hanging below the hull in Dust mode is 60
units long at rest and 150 at Space 10. On opposing mass the dust destroys one prism in three it
touches (it shrinks or steals the others), so the mode is flying **low and long** over the forest,
and a longer capsule is a wider swath. First **DOMAIN** to destroy the hostile-prism target wins.

One of the Butterfly's four element games: **Waystation** (Time, a race), **Dustup** (Charge,
working pilots over), **Tapestry** (Mass, making mass), and this (Space, destroying mass).

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameSirocco.unity` |
| Controller | `SiroccoController : MultiplayerDomainGamesController` |
| Metric | `ScoringMetric.PrismsDestroyed` (5) — **reused** |
| Rule | `SiroccoScoringRuleSO : RampageScoringRuleSO` — Rampage's rule, only the reveal wording differs |
| Turn monitor | `SiroccoPrismTurnMonitor` (Wrecking Ball's shape, its own getter) |
| Target | `EndConditionOverridesSO.siroccoPrismTarget` (**600**) |
| Comeback | rate **0.01** — a quarter behind (150 prisms) buys 1.5 element levels |
| Arena | **Rampage's cactus forest**, referenced read-only (four intensity configs) |
| Objective arrow | `HostileMassObjectiveProvider` — the densest standing hostile stand (the point the AI's erosion runs steer to), never the crystal |
| Generator | `Tools/Build/author_sirocco_assets.py` (`--check`) |

## The loop

1. **Dust mode** (right trigger): the wake narrows and the capsule switches on below you.
2. **Skim low over the forest.** Every prism the capsule enters rolls one outcome,
   deterministically per prism so every peer agrees:
   - **another colour's plant** (or neutral mass): destroyed (scores), shrunk, or stolen;
   - **your own colour's plant**: grown, armed (danger) or shielded — your side of the forest gets
     *harder* as you pass, and nothing of your own ever scores.
3. **Come back over the same stand.** A shrunk prism re-rolls (the roll hashes its size), so a
   stand erodes pass by pass rather than in one blast — hence the name.
4. **The hearts are your progression.** When the dust reaches an opposing plant's heart it
   withers the plant the way a joust does, and the heart is **auto-collected** by you
   (`Lifeform.Jousted`). A Space heart lengthens your capsule; a Charge heart sharpens the bite.

`ModeGenre` reads PrismsDestroyed as **Space** — the card's genre petal and the element that scales
the verb are the same element, which is the design of the whole four-game set.

## Reused, and why

- **The metric and rule are Rampage's.** What counts is exactly what Rampage counts: prisms not
  wearing your own colour, credited by whichever machine simulates the attacker (environment mass
  is per-peer, `Docs/ECOSYSTEM.md §27`). The dust already calls `PrismEffectHelper.DamageProportional`
  with the pilot's status, so every destroyed prism was already counted in every mode.
- **The arena is Rampage's**, the same four configs The Bends and Bloomrush reference, and its
  intensity ladder comes with it: the forest's **density** (295 / 217 / 137 / 59 plants). The target
  is one number across the ladder and sits under a quarter of the sparsest rung's forest, so a match
  ends with forest standing — asserted by the generator.
- **The scene** is cloned from Undertow (`butterfly_games_common.py`), with Rampage's own spawn ring
  (500 outside the nucleus) read off The Bends.

## AI

Every AI Butterfly flies **erosion runs**: Dust mode for the whole turn
(`ButterflyAutopilotModeDriver`, see `DUSTUP.md`) and steering to a point **above** the densest
hostile mass (`Cell.GetExplosionTarget`), re-read every 4 s so a pass is finished rather than
abandoned halfway through a stand on a 45°/s hull. Crystal seeking is deliberately overridden — the
objective is mass, not a crystal (the Wrecking Ball shape; the reverse of Rampage's rule, where the
crystal IS the weapon).

### AI trained (2026-11-21, elemental loop step 3)

Raced on the Brood Rush volume books (VC + VD, `brood_judge.py --mode Sirocco`, Tiger Moth against Tiger Moth). In a
1v1 the 600-prism target is never reached in 200 s (every match timed out), and a seat's score is its own wake
(~7k, deterministic) plus what its dust STEALS off the forest (1.8k-14k, the noisy half; neutral forest destroyed is
not VD). None of the erosion-run settings moved it beyond that noise (seeds 1-4, 8 seats): dust hover 0 / 30 / 55 / 90
12.9k / 12.6k / 13.2k / 13.4k, retarget 2 / 4 / 8 s 14.6k / 12.6k / 14.8k, and a run that overshoots the stand by 150 /
400 u (tried in code, not shipped) 16.0k / 14.8k against 15.0k. So `SiroccoSettings.asset` is unchanged.

The one lever that reaches it is the generic AIPilot throttle (0.6 -> 1.0, shipped through the Mass-Space AI plan row,
not the prefab): the faster hull lays ~25% more wake every match; its steals stay noisy.

| Mean seat VC + VD (seeds 11-12) | I1 | I2 | I3 | I4 |
|---|---|---|---|---|
| shipped | 11.7k | 19.1k | 12.9k | 12.8k |
| plan-row throttle 1.0 | 14.0k | 15.3k | 13.3k | 17.5k |

Seeds 1-4 at I2: 15.0k -> 16.6k (+10%). What is still wrong is the kit's, not the AI's: a dust pass destroys one prism
in three it touches, and destroyed neutral forest scores nothing on the volume books, so the hull's Space game pays
mostly for its wake.

## Verification status

**Authored headless; nothing has been opened in the editor.** Proved: the generator's `--check`
(watched failing on a mutated asset and passing once restored), the stand-down path, the clone's diff
against its donor, and the standing out-of-editor gates. Not proved: that it plays, and the C# has
had a syntax pass and an API-surface read but no type check.

## Known limitations

- **The target is reasoned, not measured.** A pass through a cactus catches a fraction of it and
  destroys a third of that; how many prisms a minute that is depends on how low a pilot dares fly.
  600 is a guess at ~2-3 minutes for a two-pilot side. Play-test it.
- **Charge plants are shielded** (the Charge armour law), so the dust sheds their shield first and
  takes them on a second pass.
- **The AI's run is the DENSEST hostile stand, not the nearest.** A bot may cross the forest to
  reach it.
- **Rampage intensity 1 is the heaviest cell in any arcade mode and is not profiled** (CLAUDE.md,
  Rampage) — inherited, not added.
- **Card art** is rendered by `/cardart` (MODEL tier over Rampage's intensity-2 forest, with a gold erosion
  swath cut through one stand). It is staging, not a screenshot.
