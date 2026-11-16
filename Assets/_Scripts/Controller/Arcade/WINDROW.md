# Windrow - the Rhino's Mass game

`GameModes.Windrow = 122`, hint toast `WindrowHint = 197` (both reserved for it by
`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 32, 2026-11-15). Generator: `Tools/Build/author_windrow_assets.py`.

## The loop, in the Rhino's own terms

A timed war (90 s) over **standing mass**: the score is the trail volume a domain has standing at the whistle
(`ScoringMetric.VolumeRemaining`, Tapestry's metric). Every Rhino lays trail as it flies. The only force in the cell that
takes mass away is the **plough**: the Rhino's hull destroys nothing, but a prism it throws that runs into another
destroys **both** (`PrismMotion.Annihilate`, scored to the Rhino's pilot). So every row you lay is your stock, and every
pass a rival makes along it is a raid.

**Mass is the Heft** (`element_ability_table.py Rhino`): Mass adds 0 u at rest to 60 u at Mass 10 to every throw, and
the triggers **steer** it (hold LT and everything the hull ploughs goes left, RT right). A heavy, steered plough driven
along a rival's row throws its prisms into each other, two lost for every strike; flown along your own row it banks it
to one side, out of the rival's line. **Berm** (Mass L5) lands every ploughed prism shielded, and a shielded pair bounces
instead of breaking, so at Mass 5 re-ploughing your own row (past the ~1 s self-trail grace) turns it into armour. A
careless pass along a rival's row at Mass 5 armours theirs too - the steer is the whole decision.

> The roster row said "shove the field's prisms into your zone and hold them". A plough never changes a prism's owner and
> nothing counts mass by place, so a zone would need a new metric. Windrow scores the mass the platform already counts
> (your trail standing) and makes the plough the only way to take it away.

## What is reused, and why

| Piece | From | Why |
|---|---|---|
| Arena | the bare Barren cell (Tapestry's, referenced) | No food web on purpose (Tapestry's reason: herbivores graze what the leading colour does not own, an anti-comeback current in a war over contested mass). |
| Controller | `WindrowController`, Tapestry's copy without the Butterfly mode driver | Tapestry's timed shape, its crystal scatter and its lead toast; an AI Rhino only steers. |
| Rule | a `TapestryScoringRuleSO` asset (`WindrowScoringRule`) | VolumeRemaining, timed, highest wins. |
| Settings | a `TapestrySettingsSO` asset (`WindrowSettings`) | `aiDustHover` and `aiModeRetrySeconds` are the Butterfly's and are not read. |
| Monitor | `WindrowTimeTurnMonitor` | Reads `EndConditionOverridesSO.GetWindrowRoundSeconds()`. |
| Lead toast | the shared 130, posted by the controller's sampler | A timed mode has no target for `DomainRaceToasts`. |
| Objective arrow | `BendsObjectiveProvider` | The nearest rival is where their freshest row is. |

**Intensity** is scarcity of Mass: 24 / 16 / 10 / 6 elemental crystals scattered in the cell, so the Heft and the Berm
come later, or not at all, up the ladder.

**Comeback** is sized against a volume read off the shipped Rhino (`Rhino.prefab` BaseScale, XScaler, wavelength,
cruise), Tapestry's method: a quarter of a reference domain's standing trail behind buys 1.5 element levels.

## The AI

Steering only. On a 6 s clock each AI Rhino decides whether to **lay** (a slow orbit of the cell on its own great circle,
detouring for crystals) or **raid** (fly straight through the densest opposing mass, `Cell.GetExplosionTarget`) - raid
while its domain trails by more than 10 % and in the last 25 s. Nothing presses the steer (it has no AI policy yet, a
follow-up `RHINO_PLOUGH.md` names), so an AI's throws go to the side each prism already sat on.

## Verification status

Authored headless, **not yet run in the editor**. To check in Unity: two domains of Rhinos, confirm a ram along a rival
row breaks prisms in pairs and their `VolumeRemaining` falls, and that at Mass 5 a re-ploughed own row lands shielded.
