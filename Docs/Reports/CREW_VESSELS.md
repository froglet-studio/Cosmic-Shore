# Crew vessels: where they stand (report for Garrett)

**From:** Claude sessions on `cece/compassionate-babbage-gc13io`, 2026-10-14 to 2026-10-17.
**Branch:** `cece/compassionate-babbage-gc13io`. Nothing is on `bleeding-edge` yet.
**Status:** designed in a browser lab and signed off, then ported to Unity. It is compiled and parity-checked offline,
but **it has not been played in the Unity editor**. That playtest is the next step (section 4).

## 1. In one minute

- **The first multiplayer vessel: one hull, two seats ("Maverick and Goose").** The pilot flies the hull. The
  **navigator** plays a one-thumb 2D game on a board, which is the hull's own x-z plane with the world projected onto
  it.
- **The navigator flies a kite.**
  - Its **head** pops hostile prisms and rams rival hulls.
  - Its **tail** is a lasso. Loop motes of **one** element and that element fires. Each extra mote doubles the effect:
    - **Space**: a prism-destroying blast (the game's `AOEExplosion`).
    - **Mass**: a cluster of your domain's prisms.
    - **Time**: boost for the pilot.
    - **Charge**: kite power. The next ram on a rival steals that many petals, as a scored hit.
  - Motes are the navigator's own, drawn only on its board. They are not game crystals.
- **An AI squad mate takes whichever seat is empty.** The arena's swap (D-pad left / right, keys 1 / 2) moves you
  between the hull and the kite.
- **The design came from a browser lab** that ran three rounds. It was signed off on 2026-10-17 ("this is fun, build
  it in Unity").

## 2. Try it

**The browser lab:** [Crew Vessel Lab](https://claude.ai/artifact/JtQptDHQhNdC2CxPmspfct). Private to Garrett until
shared from its Share menu. Fly the kite on a phone with one thumb, or fly the hull while the AI navigates.

**Unity:**
1. Check out `cece/compassionate-babbage-gc13io`, open the project and wait for the compile.
2. Open the Toybox, fly into the **Vessel Changer** and fly the **Wrasse** station (it looks like a Sparrow). Fly with two
   thumbs. The kite view, the real world seen from above your hull, is a small square in the lower left, and the AI flies
   the kite. The right trigger spends the boosts its Time loops save; the ability cards count them.
3. Press **2** (or D-pad right) to take the kite: its view fills the screen and the hull's shrinks to the corner. The
   kite follows the mouse or stick with no click; loop one petal shape of motes. Press **1** to fly again.

Since the 2026-10-17 port, Garrett directed rounds 4 to 4d in Unity on 2026-10-18 (the views, two thumbs, the HUD
counts, five-petal motes, depth measured from the plane and a dither cone in the kite view). The detail is in
`Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md` "Round 4". The lab has not caught up yet
(`T-20261018-3247`).

## 3. What was built

| What | Where |
|---|---|
| The lab (rounds 1-3) and its write-up | `Docs/Studios/CrewVesselLab.html`, `Docs/Studios/CREW_VESSEL_LAB.md` |
| The kite core and its AI, engine-free and ported line for line from the lab | `Assets/_Scripts/Controller/Vessel/Crew/KiteNavigatorCore.cs`, `KiteNavigatorAI.cs` |
| The seat on the hull: board, motes, shadows, thumb, effects, swap | `KiteNavigator.cs`, `KiteBoardGraphic.cs` |
| Every number, cloned from the lab | `KiteNavigatorConfigSO.cs`, `Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset` |
| The Wrasse hull (`VesselClassType.Wrasse = 19`): the Sparrow with the seat on its root, in the Toybox Vessel Changer | `Assets/_Prefabs/Spacevessels/Wrasse.prefab`, authored and registered by `Tools/Build/author_wrasse_assets.py` |
| The play-mode menu, which seats the navigator on any hull in a match | `Assets/_Scripts/Editor/Studios/KiteNavigatorHarnessMenu.cs` |
| The parity gate: C# core against the page, and asset against the page | `Tools/Build/kite_navigator_lab/parity.cjs` |
| Tests | `Assets/_Scripts/Tests/Editor/KiteNavigatorCoreTests.cs`, `KiteNavigatorConfigTests.cs` |

**Checked offline:**
- The player compile is clean.
- The parity gate passes: 3120 frames, 11 loops, and the asset equals the lab on 21 fields. Every planted change is
  caught.
- The 12 core tests pass when compiled outside Unity.

**Where the game differs from the lab:**
- Time boost is burned at once, because the Sparrow's boost is unlimited.
- The real blast can also hit rival hulls.
- It all runs on one machine.

Each is explained in `Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md`.

## 4. Open items, and what is asked of you

- **Playtest in the editor.** Task `T-20261017-aca2`; Yash has offered to take editor tests. It follows the 8 steps of
  the Kite Navigator entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
- **Decide how the seat goes online** (`T-20261017-b06e`, Garrett with Yash):
  - which machine decides each effect;
  - who owns the swap gesture in an arena match.
- **Register the kite as a duo design** when your duo-vessel branch (`cece/sharp-tesla-j0kd7z`) lands
  (`T-20261017-efca`, Garrett).
- **Sounds:** seven empty FMOD slots (`T-20261017-3cbe`).

## 5. Latest activity (generated)

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-18 from git (last 14 days), who_works_where.py and Docs/TEAM_TASKS.md: `python3 Tools/Build/team_report.py area crew-vessels --days 14`._

### Recent changes (8)
- 2026-10-18 `2d9662a53` feat(crew): the Kite's round 4 - one-thumb pilot, game-rendered kite view, L5 upgrades, Omni zoom — on another branch
- 2026-10-18 `7d4752846` wip(crew): the Kite's views drawn by the game's renderer, no-click kite, thumb-lift boost — on cece/compassionate-babbage-gc13io
- 2026-10-17 `ca66d40f0` feat(crew): the Kite - the crew vessel in the Toybox Vessel Changer — on cece/compassionate-babbage-gc13io
- 2026-10-17 `40c4f6704` docs(lab): crew vessel lab round 3 signed off and graduated to Unity — on cece/compassionate-babbage-gc13io
- 2026-10-17 `05ab8630c` feat(crew): the Kite & Tail navigator seat, ported from the Crew Vessel Lab — on cece/compassionate-babbage-gc13io
- 2026-10-17 `02bc74cca` feat(lab): crew vessel lab round 3 - the kite and its tail — on cece/compassionate-babbage-gc13io
- 2026-10-16 `4b9e48e48` feat(lab): crew vessel lab round 2 - one-thumb 2D navigator on the plane — on cece/compassionate-babbage-gc13io
- 2026-10-14 `b5367990c` feat(lab): crew vessel lab round 1 - pilot + 2D navigator seats — on cece/compassionate-babbage-gc13io

### Also in flight on other branches
- None.

### Open tasks (Docs/TEAM_TASKS.md)
- T-20261017-b06e [open] decision: Decide how the Kite Navigator seat goes online: today the board, motes, AI and every effect run on the hull's local human's machine. A second human on another machine needs the seat replicated, and a choice of which machine decides pops, blasts, Mass clusters, boost and steals (vessel rule 48). Also decide the arena case: the seat's swap uses PilotSwapGesture, which in an arena match also asks the arena to swap hulls while the human flies. (who: Garrett with Yash (party / multiplayer))
- T-20261017-efca [open] dev: When the duo-vessel system (cece/sharp-tesla-j0kd7z) reaches bleeding-edge, register the Kite Navigator as a duo design so a hull can carry it from a mode or a menu, not only from the play-mode FrogletTools menu; keep its one swap (PilotSwapGesture) and its AI-in-every-empty-seat rule. (who: Garrett (duo-vessel owner))
- T-20261017-3cbe [open] content: Author FMOD events for the Kite Navigator's seven empty EventReference slots on KiteNavigatorConfig.asset: loop closed, loop fizzled, prism popped, Mass cluster, Time boost, Charge, steal. (who: anyone who does audio)
- T-20261017-aca2 [open] test: Playtest the Wrasse in the Unity Editor: open the Toybox, fly the Wrasse station in the Vessel Changer, then the 9 verify steps of its checklist entry (board draws, AI loops, 2/D-pad right swaps to the kite, each element's loop fires, a charged ram steals and scores, 1 swaps back with no stuck input or boost, the changer swaps cleanly in and out of the Wrasse). It has only been compiled headlessly. (who: Yash (offered), anyone with the Editor)
- T-20261018-3247 [open] dev: Bring the Crew Vessel Lab up to the Wrasse's round 4, which was directed in Unity on 2026-10-18: the kite view and pilot view swap (small view lower left), lime motes in their element's petal shape, the kite following the mouse or stick with no click, a one-thumb pilot whose lifted thumb spends the Time boost (2 s cooldown, none at Time L5), mote frequency weighted by the hull's petals, Charge rams on fauna, Eject below Charge L5 and Steal at it, shielded Mass clusters at Mass L5, Space L5 blasts through shields and super shields, and the Omni crystal's 5 s double-size board. Then re-run parity.cjs and verify_lab.cjs and republish. (who: any session; Garrett to sign off)
<!-- team_report:end -->
