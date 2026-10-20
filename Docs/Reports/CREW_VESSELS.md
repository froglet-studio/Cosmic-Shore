# Crew vessels: where they stand (report for Garrett)

**From:** Claude sessions on `cece/compassionate-babbage-gc13io`, 2026-10-14 to 2026-10-18.
**Branch:** `cece/compassionate-babbage-gc13io`. Nothing is on `bleeding-edge` yet.
**Status:** designed in a browser lab and signed off, ported to Unity, then directed by play through rounds 4-4f. It is compiled and parity-checked offline,
but **it has not been played in the Unity editor**. That playtest is the next step (section 4).

## 1. In one minute

- **The first multiplayer vessel: one hull, two seats ("Maverick and Goose"), named the Wrasse; its card reads
  "Wrasse and Grouper"**, after the cleaner wrasse and the reef fish it cleans. The pilot flies the hull (the Grouper)
  on two sticks. The **navigator** (the Wrasse) plays a one-thumb 2D game on a board, which is the hull's own x-z plane
  with the world projected onto it.
- **The navigator flies a kite.**
  - Its **head** pops hostile prisms and rams rival hulls.
  - Its **tail** is a lasso. Loop motes of **one** element and that element fires. Each extra mote doubles the effect:
    - **Space**: a prism-destroying blast (the game's `AOEExplosion`).
    - **Mass**: a cluster of your domain's prisms.
    - **Time**: boosts saved for the pilot, spent on the right trigger.
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
2. Open the Toybox, fly into the **Vessel Changer** and fly the **Wrasse and Grouper** station (it looks like a Sparrow). Fly with two
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
| The seat on the hull: board, motes, shadows, thumb, effects, swap, saved boosts, L5s, Omni burst | `KiteNavigator.cs` |
| The two views, drawn by the game's renderer (the top-down kite camera, the picture-in-picture swap, the five-petal motes) | `KiteNavigatorView.cs` |
| Every number, cloned from the lab, plus the rounds 4-4f fields | `KiteNavigatorConfigSO.cs`, `Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset` |
| The Wrasse hull (`VesselClassType.Wrasse = 19`, card "Wrasse and Grouper"): the Sparrow without its kit, two sticks, the seat on its root, in the Toybox Vessel Changer | `Assets/_Prefabs/Spacevessels/Wrasse.prefab`, authored, retrofitted and registered by `Tools/Build/author_wrasse_assets.py` (`--check`, `--self-test`) |
| The HUD counts on the four ability cards | `Assets/_Scripts/UI/Controller/WrasseHUDController.cs` |
| Out-of-plane prisms: depth from the plane and a dither cone, for the kite camera only | `Assets/_Scripts/Utility/PrismDepthPlane.cs`, `Assets/_Graphics/Materials/Graphs/PrismDepthPlane.hlsl`, `PrismOcclusionCorridor.BeginViewOverride` |
| Two names on one card, for any crew vessel | `SO_Vessel.CrewNames` / `CardName` |
| The Omni burst | `KiteOmniCrystalEffectSO.cs` in `WrasseImpactorDataContainer` |
| The play-mode menu, which seats the navigator on any hull in a match | `Assets/_Scripts/Editor/Studios/KiteNavigatorHarnessMenu.cs` |
| The parity gate: C# core against the page, and asset against the page | `Tools/Build/kite_navigator_lab/parity.cjs` |
| Tests | `Assets/_Scripts/Tests/Editor/KiteNavigatorCoreTests.cs`, `KiteNavigatorConfigTests.cs` |

**Checked offline:**
- The player compile against Unity reference assemblies (`unity_refcompile`) is clean. The editor compile has 3 errors,
  all of them already on `bleeding-edge`.
- The parity gate passes: 3120 frames, 11 loops, and the asset equals the lab on 21 fields. Every planted change is
  caught.
- `author_wrasse_assets.py --check` and `--self-test` pass, and `verify_swarm_member_pose.py` passes with the new
  shader include.

**Where the game differs from the lab:** rounds 4-4f were directed in Unity (the views, two thumbs, saved boosts, the
HUD, the flowers, the L5s, the Omni burst, the out-of-plane look). The real blast can also hit rival hulls, and it all
runs on one machine. Each is explained in `Assets/_Scripts/Controller/Vessel/Crew/KITE_NAVIGATOR.md`.

## 4. Open items, and what is asked of you

- **Fly it in the editor** (`T-20261018-5c29`; Yash can run the editor pass). Let the shaders re-import, check the pilot
  view's prisms look unchanged, then follow the Kite Navigator entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
  `T-20261017-aca2` is the original playtest task.
- **Decide how the seat goes online** (`T-20261017-b06e`, Garrett with Yash):
  - which machine decides each effect;
  - who owns the swap gesture in an arena match.
- **Register the seat as a duo design** when your duo-vessel branch (`cece/sharp-tesla-j0kd7z`) lands
  (`T-20261017-efca`, Garrett). The Goby there still uses enum value 16 and has to move; 18 is left for it.
- **Card icons:** the Wrasse still shows the Sparrow's four ability icons (`T-20261018-8ff7`).
- **Sounds:** eight empty FMOD slots (`T-20261017-3cbe`).
- **Catch the lab up** to rounds 4-4f (`T-20261018-3247`).

## 5. Latest activity (generated)

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-18 from git (last 14 days), who_works_where.py and Docs/TEAM_TASKS.md: `python3 Tools/Build/team_report.py area crew-vessels --days 14`._

### Recent changes (16)
- 2026-10-18 `d5b555ef1` feat(crew): the Wrasse's motes in the totem's proportions; Mass holds three in, one Charge bolt — on cece/compassionate-babbage-gc13io
- 2026-10-18 `3e189603b` feat(crew): name each subvessel - the card reads "Wrasse and Grouper" — on cece/compassionate-babbage-gc13io
- 2026-10-18 `bad724760` refactor(crew): name the crew hull the Wrasse - a cleaner wrasse and its host — on cece/compassionate-babbage-gc13io
- 2026-10-18 `5df4a0bfc` feat(crew): Kite round 4e - Omni drops motes, petals set apart, a more agile pilot — on cece/compassionate-babbage-gc13io
- 2026-10-18 `90aacdda9` feat(crew): Kite round 4d - two thumbs, HUD counts, plane-depth prisms and a dither cone — on cece/compassionate-babbage-gc13io
- 2026-10-18 `5c5a0e8dc` wip(crew): five-petal mote flowers with their element's motion; discrete saved boosts; blast count — on cece/compassionate-babbage-gc13io
- 2026-10-18 `d1d6b3e96` fix(crew): strip the Sparrow's kit off the Kite; each mote wears its crystal's effect — on cece/compassionate-babbage-gc13io
- 2026-10-18 `464a457b0` fix(crew): the Kite's petals drawn visibly, boost on the pilot's trigger, unlit out-of-plane prisms, 50% longer lasso — on cece/compassionate-babbage-gc13io
- 2026-10-18 `2d9662a53` feat(crew): the Kite's round 4 - one-thumb pilot, game-rendered kite view, L5 upgrades, Omni zoom — on cece/compassionate-babbage-gc13io
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
- T-20261017-3cbe [open] content: Author FMOD events for the Kite Navigator's eight empty EventReference slots on KiteNavigatorConfig.asset: loop closed, loop fizzled, prism popped, Mass cluster, Time boost, Charge, steal, fauna struck. (who: anyone who does audio)
- T-20261017-aca2 [open] test: Playtest the Wrasse in the Unity Editor: open the Toybox, fly the Wrasse station in the Vessel Changer, then the 9 verify steps of its checklist entry (board draws, AI loops, 2/D-pad right swaps to the kite, each element's loop fires, a charged ram steals and scores, 1 swaps back with no stuck input or boost, the changer swaps cleanly in and out of the Wrasse). It has only been compiled headlessly. (who: Yash (offered), anyone with the Editor)
- T-20261018-3247 [open] dev: Bring the Crew Vessel Lab up to the Wrasse's rounds 4-4f, which were directed in Unity on 2026-10-18 (KITE_NAVIGATOR.md "Where it stands"). They cover: the kite view and the pilot view swapping (the small view lower left); the kite following the mouse or stick with no click; a two-stick pilot who spends saved Time boosts on the right trigger (2 s cooldown, none at Time L5); lime five-petal motes in the totem's proportions, each moving like its element; mote frequency weighted by the hull's petals; Charge rams on fauna; Eject below Charge L5 and Steal at it; shielded Mass clusters at Mass L5; Space L5 blasts through shields and super shields; the Omni crystal's burst of motes; and out-of-plane prisms seen with depth from the plane. Then re-run parity.cjs and verify_lab.cjs and republish. (who: any session; Garrett to sign off)
- T-20261018-8ff7 [open] content: Draw the Wrasse's four ability-card icons. Since round 4d the cards read the navigator (WrasseHUDController: Time = saved boosts, Charge = kite rams, Space = last blast's kills, Mass = flash on a cluster) and ElementalAbilityMaps/Wrasse.asset names them Space Blast, Saved Boosts, Mass Cluster and Kite Ram, but the sprites on the HUD (SparrowHUDVariant) are still the Sparrow's gun, missile and stance icons. (who: Garrett (vessels, icons); any session for the variant wiring)
- T-20261018-5c29 [open] test: Fly the Wrasse's round 4d in the Unity Editor: let BlockGraph and the swarm member shaders re-import with the new PrismDepthPlane.hlsl include, confirm the pilot's own view of prisms is unchanged, then run Docs/UNITY_VERIFICATION_CHECKLIST.md's Wrasse steps 2-4 (two-thumb flight, the HUD counts, the five-petal motes, out-of-plane prisms opening with distance from the plane and dithering above it). (who: Garrett (sign-off); Yash can run the editor pass)
<!-- team_report:end -->
