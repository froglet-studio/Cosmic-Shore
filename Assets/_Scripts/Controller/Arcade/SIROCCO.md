# Sirocco (`GameModes.Sirocco = 61`)

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
