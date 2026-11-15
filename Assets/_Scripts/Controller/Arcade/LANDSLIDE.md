# Landslide - the Rhino's Space game

`GameModes.Landslide = 123`, hint toast `LandslideHint = 198` (both reserved for it by
`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 33, 2026-11-15, under the working name "Avalanche"; renamed because
Avalanche is the Rhino's retired Charge L5). Generator: `Tools/Build/author_landslide_assets.py`.

## The loop, in the Rhino's own terms

A demolition race through **Cleave's four arenas** (I1 the Panes, I2 the Swell, I3 the Cage, I4 the Twistbands; 14,000
to 16,500 prisms each, no nucleus, no fauna). The Rhino's hull destroys nothing, so every prism it scores is a
**strike**: the plough throws a prism sideways, and a thrown prism that runs into another destroys **both**, scored to
the Rhino's pilot (`RhinoPloughPrismEffectSO` -> `PrismMotion.Annihilate`). Cleave's arenas are corduroys of ribs packed
a prism apart, so nearly every throw is a strike. Danger ribs still sting when ploughed; super-shielded mass only
jiggles until **Unstoppable** (Space L5) lets the plough move it too.

**Space is the Blade** (`element_ability_table.py Rhino`): one hull collider across the nose, 14 u wide at rest to 40 u
at Space 10. A wider blade catches more ribs per pass, and every rib it catches is a throw. Charge's Carry (a thrown
prism still strikes up to 40 u past its landing point) and Mass's Heft (a longer throw) add reach, but the width is
Space's, and it is what the score rides on. First **domain** to 500 hostile prisms wins (golf-timed, Cleave's shape).

## What is reused, and why

| Piece | From | Why |
|---|---|---|
| Match, milestones, AI | `CleaveController` (`LandslideController` subclasses it) | Written for the Rhino before the Swordfish split took the sword: AI cage-breakers on stations OUTSIDE the arena, so every transit ploughs straight through it; steering only. Landslide adds only its opening hint. |
| Rule | a `CleaveScoringRuleSO` asset (`LandslideScoringRule`) | HostilePrismsDestroyed race, golf-timed. |
| Arena | Cleave's four cell configs, referenced | Packed ribs make the plough's strike the verb; each rung is a different place. |
| Monitor | `LandslidePrismTurnMonitor` (Cleave's, with its own scalar target) | One target for every rung: the plough scores two prisms a strike wherever the ribs are. |
| Toasts | Cleave's 50-52, worded for Landslide in its own config; 198 the hint | |
| Objective arrow | `HostileMassObjectiveProvider` | The densest hostile mass still standing. |

The generator asserts three domains' targets fit under a quarter of each rung's breakable pool (the arenas never
regrow), and that the plough still scores its strikes to the Rhino's pilot.

## Verification status

Authored headless, **not yet run in the editor**. To check in Unity: a Rhino ramming a pane at cruise breaks prisms in
pairs and `HostilePrismsDestroyed` rises; raising Space visibly widens the band a pass breaks; the AI Rhinos plough the
arena from outside in. The rate (and so the 500 target) is a first guess until someone flies it.
