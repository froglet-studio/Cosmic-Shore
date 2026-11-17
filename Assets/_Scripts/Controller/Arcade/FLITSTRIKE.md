# Flitstrike (GameModes.Flitstrike = 108)

The Honeyguide's **Charge** game (`Docs/ElementalLoop/GAME_KIT_ROSTER.md` row 18, hint toast 183).

## The loop
Hold LT to see the Flit's lane, let go to dart along it, and every rival hull inside the strike radius takes a
Strike. Every strike is one point. The first **domain** to the strike target wins (default 5, authored in
FrogletTools > Game Modes > End Game Conditions, `flitstrikeStrikeTarget`). Ties go to whoever got there first
(golf-timed, the Dustup shape).

## Reused vs owned
- **Reused:** the platform (`MultiplayerDomainGamesController`), Dog Fight's Boneyard cells (read-only), the
  Jackstraw scoring rule (`FlitstrikeScoringRuleSO : JackstrawScoringRuleSO`), `CombatPointTurnMonitorBase`,
  `DomainRaceToasts` (128, 129, 130), the comeback toast (30), `BendsObjectiveProvider` on the HUD.
- **Owned:** `FlitstrikeController`, `FlitstrikeSettingsSO`, `FlitstrikeStrikeTurnMonitor`, the hint toast.
- **Not changed:** the vessel. `HoneyguideCrew.StrikeVessel` already raises a Strike-class combat hit in every
  mode; this mode only pays for it (LOCKED rule, a mode never changes the vessel). The generator asserts it.

## AI
The AI only steers, through `AIPilot.SetExternalTargetProvider`: an intercept point on the nearest opponent
(`aiInterceptLeadSeconds` ahead, retargeted every `aiRetargetSeconds`, humans weighted by `aiHumanFocus`). The
Honeyguide crew presses its own triggers. No AI is trained here yet (elemental loop step 3).

## Assets
`Tools/Build/author_flitstrike_assets.py` (`--check`) writes the card, settings, scoring rule, toast config,
preview, scene (cloned from Undertow through `honeyguide_games_common.py`, Honeyguide AI templates), roster rows,
build-scene entry and end-condition value.

## Verification status
Authored headless and passing every generator gate. **Not compiled in Unity and not run in the editor.** The card
uses the shared placeholder until `/cardart` renders it (`render_card_backgrounds.py Flitstrike`, which needs a
dotnet 8 SDK; the recipe rows are in place).

## Known limitations
- The target is a first guess; the I1-I4 ladder (loop step 2) has not been tuned.
