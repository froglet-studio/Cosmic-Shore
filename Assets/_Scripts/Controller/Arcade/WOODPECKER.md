# Woodpecker (GameModes.Woodpecker = 110)

The Honeyguide's **Space** game (`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 20, hint toast 185).

## The loop
A pecking race through Rampage's cactus forest. RT fires a burst of Pecks (3 shots at Space 0, 6 at Space 10,
piercing from Space 5) that chips hostile prisms. The first **domain** to the hostile-prism target wins (default
150, `woodpeckerPrismTarget` in End Game Conditions). A prism milestone toast (83) fires every 50.

## Reused vs owned
- **Reused:** Rampage's four cactus-forest cell configs (read-only), Rampage's metric
  (`ScoringMetric.PrismsDestroyed`) through `WoodpeckerScoringRuleSO : ShatterplaneScoringRuleSO`, Sirocco's prism
  turn monitor shape, `DomainRaceToasts`, the comeback toast, `HostileMassObjectiveProvider` on the HUD.
- **Owned:** `WoodpeckerController`, `WoodpeckerSettingsSO`, `WoodpeckerPrismTurnMonitor`, the hint toast.
- **Not changed:** the vessel.

## AI
Steering only: the AI flies to `arenaCell.GetExplosionTarget(domain)`, re-read every `aiRetargetSeconds` (4 s). The
crew fires its own Pecks.

## Assets
`Tools/Build/author_woodpecker_assets.py` (`--check`), same set as every arcade mode.

## Verification status
Authored headless and passing every generator gate. **Not compiled in Unity and not run in the editor.** Card art
is the placeholder until `/cardart` renders it.

## Known limitations
- The target and intensity ladder are first guesses (loop step 2 not done).
