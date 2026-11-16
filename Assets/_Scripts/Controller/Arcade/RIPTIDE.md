# Riptide - the Goby & Pistol's Space game

`GameModes.Riptide = 116`, hint toast `RiptideHint = 191` (both reserved in `Docs/ElementalLoop/GAME_KIT_ROSTER.md`,
row 26). Authored by `Tools/Build/author_riptide_assets.py` (shared pieces in `goby_games_lib.py`). Built 2026-11-15.
**Authored headless, not yet run in the editor.**

## The loop, in the pair's own terms

Shatterplane's field of wide neutral plates. The **Goby** darts its islands past and through the plates. The
**Pistol**'s **Undertow** (the Space ability, always on) pulls loose mass either side of its plane into it (40 -> 200 u
reach with Space; at Space 5, Deep Undertow, shielded prisms too), and its vortex rings (**Snap**) gather up to 30
prisms each and smash them as they collapse; its body breaks what it touches. Every hostile prism broken is one point;
first domain to the target wins.

## What it owns, and what it reuses

| Decision | Riptide |
|---|---|
| Metric | `PrismsDestroyed` (5) through `RiptideScoringRuleSO`, Shatterplane's rule (Rampage's) inherited with its own words: every prism the pair breaks that is not its own colour - the plates' tiles, a rival pair's islands, perches and dens. |
| Target | `EndConditionOverridesSO.riptidePrismTarget` (600), read by `RiptidePrismTurnMonitor`. The plates never regrow, so the generator asserts three domains' worth fits the smallest breakable field (I4: 3,232 tiles). An estimate: a ring takes at most 30 and reloads in 4 s. |
| Comeback | 0.01 per prism behind: a quarter of the target behind buys 1.5 element levels. |
| Arena | **Shatterplane's four cells, referenced.** Neutral plain tiles are exactly what the undertow moves and the ring breaks; I4's super-shielded ribs and danger screens are what it never moves. Shatterplane's crystal field and nucleus sizing belong to that mode's controller, not the cell, so they are not here. |
| AI | **None of the mode's own.** The Goby AI routes each island through the most breakable rival mass (`AiPlanRoute`, the path map's score) and the Pistol AI snaps at what comes in. |
| Min players / domains | 1 / 1: a race against the plates, Shatterplane's. |

Nothing about the pair changes. The scene is Shatterplane's, cloned through `lib.committed_scene`: controller, monitor
and rule swapped, the donor's settings and arena-cell fields stripped, the four AI templates turned from Brittle Stars
into Gobies.

## Verification status

Authored headless: the generator's `--check`, the reference compile and the static gates. **Not run in Unity.** What
only a person can judge: the rate the undertow brings tiles into reach of a ring, and so whether 600 is a 90 s or a
5 min race.

## Known limitations

- The Shatterplane plates were sized for the Brittle Star's ring blast. Whether their spacing suits the Goby's
  900-1800 u islands is the first thing a playtest will say.
