# Riposte - the Swordfish's Charge game

> **Status.** Authored headless on 2026-11-15 (`claude/swordfish-game-kit`), for Garrett's ask to build every missing
> elemental game, on the ids `Docs/ElementalLoop/GAME_KIT_ROSTER.md` reserved for it (row 7: `GameModes` 97, hint toast
> 172). **Nothing here has been run in the Unity editor, and nobody has flown it.**

`GameModes.Riposte = 97` is the **Swordfish's Charge card**: working other pilots over (`CombatPoints`).

## 1. Why this game, for this hull

The Swordfish's **Charge** card is **Energize** and its PvP: a sword hit on a rival hull takes x1 -> x2.5 petals across
Charge 0-10, knocked loose as crystals at the strike, and at Charge 5 **Sword Steal** pockets them instead
(`SwordfishSwordStealBySkimmerEffect`). The hit itself is `VesselCombatHitBySword`: a **Strike**-class combat hit,
once per victim per 1.4 s, and **only while the Swordfish is faster than its victim** (`requireFasterThanVictim`).

The Swordfish's speed is its **ramp**, which winds up to 840 u/s only on a straight line. So the duel is about lines:
wind up somewhere straight, then run the bill through a rival who is slower than you.

Riposte puts that duel in **Jackstraw's heap of pick-up sticks** (the Thresher's Charge arena, `JACKSTRAW.md`, its four
cells referenced read-only). The sticks break straight lines, and every one of them is something the Swordfish answers
differently:

| In the heap | What the Swordfish does |
|---|---|
| a plain stick | the blade cuts it, and the hull rams it for free: cut a lane and wind up in it |
| a shielded jack knot (I2+) | two strokes: the first pops the shield |
| a red (danger) tip (I3+) | ramming it debuffs every element for 4 s (the fleet's danger debuff) |
| ironwood (I4, super-shielded) | binds a blade that is not energized, dragging the turn to 30%; an energized blade with Shield Breaker (Space 5) pops it |

The heap thins as the match goes on (cut sticks do not grow back), so the late game has longer lines and faster duels.
The crystals floating in the clearings are where a struck (silenced) pilot refuels.

The vessel is untouched (the LOCKED mode rule). The mode's levers are the heap, the crystals and the AI.

## 2. What is reused, and the one seam

- **`RiposteController : JackstrawController`.** Everything Jackstraw's controller does (1 round, 1 turn, the crystals
  laid on every peer, the hunters armed, the server winner, the snapshot with the breakdown, the replay) is the same
  for a sword duel. The one seam added to Jackstraw for it is `protected virtual GameToastSituation HintSituation`, so
  Riposte posts its own hint. *Affects: the Thresher's Jackstraw (the seam only; Jackstraw's behaviour is unchanged).*
- **`RiposteScoringRuleSO : JackstrawScoringRuleSO`**, Kelpwood's pattern: a Strike pays its points, every other class
  nothing, every line an authored template; Riposte's words are on its own asset.
- **`RiposteSettings.asset` is a `JackstrawSettingsSO`** with the hunters re-tuned for a bill that LEADS the hull (the
  Thresher's ball trails, so Jackstraw aims its runs off the rival): miss radius 24 / 14 / 6 / 0 u by AI level (Jackstraw
  45 / 30 / 15 / 5) and a 120 u overshoot so the hull keeps its speed through the pass.
- **`RiposteStrikeTurnMonitor`**: the shared combat-point monitor on `EndConditionOverridesSO.riposteStrikeTarget` (6).
- The scene is Jackstraw's, cloned with the identity, the settings and rule, the monitor, fresh Netcode ids and four
  Swordfish AI seats.

## 3. The AI

Steering only, Jackstraw's hunters (`JackstrawHunter`): pick the cheapest rival, run through their led position, break
off for a crystal when silenced; difficulty is the seat's AI level, never the intensity. The sword is the Swordfish's
own hand (`SwordfishSwordAutopilot`, on the prefab), which swings at hostile prisms in reach and energizes in a crowd,
so an AI cuts sticks on its way. **Watch item:** the hunters steer straight at a rival, which keeps the ramp only if
nothing is in the way; an AI that keeps losing the speed check against human rivals needs a "wind up first" phase in
the mode's AI, not a vessel change.

## 4. Ladder

The heap's own (`JACKSTRAW.md` §2): loose sticks, then jacks, then red tips, then ironwood, 408 / 1,122 / 2,114 /
3,566 prisms. The target is the same 6 strikes at every rung.

## 5. Verification status

- Authored headless: `author_riposte_assets.py --check`, the reference compile and the static gates (see the PR).
- **Not run in Unity, not flown, no card art rendered yet.** Hand test: four seats at I1, confirm a sword hit scores
  only while faster, the heap cuts and rams, and the crystals regrow; at I4 confirm ironwood binds an unlit blade.
- Card art: `/cardart` needs a `Riposte` recipe (Jackstraw's heap, a Swordfish in it).
