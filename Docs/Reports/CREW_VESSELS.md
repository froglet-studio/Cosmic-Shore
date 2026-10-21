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

**Since 2026-10-20 (`cece/loving-fermat-ntdnbu`):** the kite is held by **the Sling** on a thumb or the mouse
(picked in the Wrasse Lasso Lab, https://claude.ai/artifact/T5EBvmTvFw4KNRMQtwXDQb); it **threads switches** (every
race's rings; Toybox toys are the Thresher's ball's, not the kite's); every input and the AI hold it the same
way; an AI Wrasse is two AIs (pilot and navigator); and **Wrasse Run** (`GameModes` 68, `WRASSERUN.md`) is its race: a ring counts
only when the kite threads it, and Time loops fuel the Grouper's boosts. Not editor-verified (`T-20261020-1187`,
`T-20261020-ec68`).

**Round 7, the same day:** the Wrasse and the Grouper are real `/vessel-model` hulls (`Grouper.fbx`, `WrasseKite.fbx`)
puppeted by `WrasseCrewBodies`, and every player sees both (the Wrasse's pose is replicated). The kite flies the whole
kite view, which is zoomed out 30% and centred on the Grouper. The board turns to the hull's heading over distance
flown, so a yaw in place no longer drags the motes. The cord is drawn while held. A held click or trigger lets go, as a
lifted thumb does. Top speed is 364 (70%) and the lasso is twice as long (4.8). Not editor-verified (`T-20261020-d09f`);
the hit box, jets and engine flash are still the Sparrow's (`T-20261020-a9f5`).

**Rounds 8-10 (2026-10-20/21):**
- **Layout:** the hand stops at a line just above the totems and a small margin inside the screen; the Wrasse is held only by
  its cord. The Grouper's view fills the box left of the totems and below the line, and no longer pumps.
- **Colours:** the four lasso elements are four green-yellow limes, derived by `Tools/Build/kite_mote_colors.py` (Charge
  `#a0ff00`, Mass `#5ec600`, Space `#40ff00`, Time `#9bcf00`).
- **Juice:** every effect ring is gone and each loop's motes become its effect: a fizzle sputters out, Space explodes with
  the blast, Mass blocks morph into the prisms, Time streaks onto the Grouper, and Charge orbits onto the Wrasse.
- Not editor-verified: `T-20261020-8dbc`, `T-20261021-3d24`, `T-20261021-d063`.

**2026-10-21, two seats, either device (decided by Garrett):** "wrasse and grouper, are two seats. players can select,
change seats etc. both can be played on mobile or pc." This closes `T-20261017-b06e`, and it replaces the audit line
that one phone runs one human plus an AI.
- **Today, one human:** keys 1 / 2 pick the Grouper or the Wrasse, and the D-pad or a double tap on the small view
  steps to the other seat. The pick is remembered per hull class (`CrewSeatChoice`).
- **The phone boost:** lifting both thumbs now really speeds the Grouper up (idle reads a released pad, 72.5 against
  35 u/s at a full spread). It is spent only after a thumb has touched in the pilot seat.
- **The online plan:** `KITE_NAVIGATOR.md` "Two humans, two seats (plan, 2026-10-21)":
  - the hull is owned by the pilot seat's machine;
  - the Wrasse is simulated on its holder's machine, which sends each decided effect through a server-checked relay;
  - the seat owns the swap gesture on a crew hull: seats first, then the arena's ring;
  - the work order is on the board as `T-20261021-aa19` (Yash with Garrett, on `cece/sharp-tesla-j0kd7z`'s
    `CrewSeat`).
- **A gap it fixes first:** pops, blasts and Mass clusters happen only on the Wrasse's own machine today, even with one
  human online.

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
- **Build the two human seats** (`T-20261021-aa19`, Yash with Garrett). The decision (`T-20261017-b06e`) was made on
  2026-10-21: two seats, either device. The plan is in `KITE_NAVIGATOR.md`.
- **Register the seat as a duo design** when your duo-vessel branch (`cece/sharp-tesla-j0kd7z`) lands
  (`T-20261017-efca`, Garrett). The Goby there still uses enum value 16 and has to move; 18 is left for it.
- **Card icons:** the Wrasse still shows the Sparrow's four ability icons (`T-20261018-8ff7`).
- **Sounds:** eight empty FMOD slots (`T-20261017-3cbe`).
- **Catch the lab up** to rounds 4-4f (`T-20261018-3247`).

## 5. Latest activity (generated)

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-21 from git (last 14 days), who_works_where.py and Docs/TEAM_TASKS.md: `python3 Tools/Build/team_report.py area crew-vessels --days 14`._

### Recent changes (25+)
- 2026-10-21 `a859581c4` fix(orca): shots fly down the aim; sniper keeps the Serpent's pip scope open (lab round 7) — on another branch
- 2026-10-21 `6fd866b2d` feat(crew): the Wrasse's juice - every effect is its motes' own petals, no rings; a steady small view — on another branch
- 2026-10-21 `e0050f8e9` feat(orca): its own hull model with three code-aimed turrets (/vessel-model) — on another branch
- 2026-10-21 `abdec3ecf` feat(orca): seats are elements, scoped sniper, one-at-a-time bomb, the fast gun's pen, crosshairs (lab round 6) — on another branch
- 2026-10-21 `920bc7e5d` feat(crew): the Wrasse's four limes are green-yellows - no blue, no orange — on another branch
- 2026-10-21 `072a96353` feat(crew): only the cord holds the Wrasse; its four lasso elements are four limes — on another branch
- 2026-10-21 `c5985f500` feat(orca): three distinct guns per seat, per-seat missiles, carried super-shield (lab round 5) — on another branch
- 2026-10-20 `b4fc7ddbf` feat(crew): the Wrasse's hand line above the totems; the Grouper's view fills the corner — on another branch
- 2026-10-20 `d42f8b177` feat(labmaker): Honeyguide Lab round 1 and the engine-free Dynamo core — on another branch
- 2026-10-20 `2adae812d` feat(crew): the Orca, the 4-seat vessel, in the Toybox Vessel Changer — on another branch
- 2026-10-20 `01c24e802` feat(crew): the Wrasse and Grouper get real bodies; the kite flies the whole screen — on another branch
- 2026-10-20 `47c0d54ca` feat(crew): AI Wrasses are two AIs; the flail trips toys, not the kite; one Sling for every input — on another branch
- 2026-10-20 `b6293d5a3` feat(crew): the Wrasse's Sling, switches, and Wrasse Run (GameModes 68) — on another branch
- 2026-10-20 `91f878645` feat(elements): a strip or a steal silences its victim's harm — on another branch
- 2026-10-19 `27a4e1a4e` docs(ship): re-measured Shader.Find count, card taps in the HUD topic file, refactor rows — on bleeding-edge
- 2026-10-19 `35ddff56f` fix(crew): no stray material when the kite view falls back to Shader.Find — on bleeding-edge
- 2026-10-19 `04dfaf3c7` docs(hud,crew): record the phone round - tap-to-press cards, the Wrasse's shader, the PIP swap — on bleeding-edge
- 2026-10-19 `df14a3121` feat(crew): double-tap the small view to swap seats on the Wrasse — on bleeding-edge
- 2026-10-19 `1543a6e93` fix(crew): the Wrasse's kite, tail and motes draw in a player build — on bleeding-edge
- 2026-10-18 `ea77f28d5` feat(goby): the vortex ring and flares are lit faceted volumes; ship-pass fixes — on another branch
- 2026-10-18 `c2d559666` feat(crew): one notion of a crew vessel - the Goby and Pistol joins the Wrasse and Grouper — on another branch
- 2026-10-18 `9ad432e2f` docs(crew): ship polish - the Wrasse's current state, the report, follow-ups, lab capture — on bleeding-edge
- 2026-10-18 `d5b555ef1` feat(crew): the Wrasse's motes in the totem's proportions; Mass holds three in, one Charge bolt — on bleeding-edge
- 2026-10-18 `3e189603b` feat(crew): name each subvessel - the card reads "Wrasse and Grouper" — on bleeding-edge
- 2026-10-18 `bad724760` refactor(crew): name the crew hull the Wrasse - a cleaner wrasse and its host — on bleeding-edge

### Also in flight on other branches
- `cece/happy-noether-m54hz6` (unattributed, 2026-10-21): fix(orca): shots fly down the aim; sniper keeps the Serpent's pip scope open (lab round 7)
- `cece/sleepy-shannon-9qk646` (unattributed, 2026-10-21): fix(silence-lab): unbreak the lab script (apostrophe in a quoted string)
- `claude/honeyguide-ratel` (unattributed, 2026-10-21): feat(honeyguide-lab): round 4 - every pattern gets its own tabletop card's effect
- `cece/loving-fermat-ntdnbu` (unattributed, 2026-10-21): feat(crew): the Wrasse's juice - every effect is its motes' own petals, no rings; a steady small view
- `cece/sharp-tesla-j0kd7z` (unattributed, 2026-10-19): fix(team-tasks): --check fails on merge conflict markers left in the board

### Open tasks (Docs/TEAM_TASKS.md)
- T-20261017-b06e [open] decision: Decide how the Kite Navigator seat goes online: today the board, motes, AI and every effect run on the hull's local human's machine. A second human on another machine needs the seat replicated, and a choice of which machine decides pops, blasts, Mass clusters, boost and steals (vessel rule 48). Also decide the arena case: the seat's swap uses PilotSwapGesture, which in an arena match also asks the arena to swap hulls while the human flies. (who: Garrett with Yash (party / multiplayer))
- T-20261017-efca [open] dev: When the duo-vessel system (cece/sharp-tesla-j0kd7z) reaches bleeding-edge, register the Kite Navigator as a duo design so a hull can carry it from a mode or a menu, not only from the play-mode FrogletTools menu; keep its one swap (PilotSwapGesture) and its AI-in-every-empty-seat rule. (who: Garrett (duo-vessel owner))
- T-20261017-3cbe [open] content: Author FMOD events for the Kite Navigator's eight empty EventReference slots on KiteNavigatorConfig.asset: loop closed, loop fizzled, prism popped, Mass cluster, Time boost, Charge, steal, fauna struck. (who: anyone who does audio)
- T-20261017-aca2 [open] test: Playtest the Wrasse in the Unity Editor: open the Toybox, fly the Wrasse station in the Vessel Changer, then the 9 verify steps of its checklist entry (board draws, AI loops, 2/D-pad right swaps to the kite, each element's loop fires, a charged ram steals and scores, 1 swaps back with no stuck input or boost, the changer swaps cleanly in and out of the Wrasse). It has only been compiled headlessly. (who: Yash (offered), anyone with the Editor)
- T-20261018-3247 [open] dev: Bring the Crew Vessel Lab up to the Wrasse's rounds 4-4f, which were directed in Unity on 2026-10-18 (KITE_NAVIGATOR.md "Where it stands"). They cover: the kite view and the pilot view swapping (the small view lower left); the kite following the mouse or stick with no click; a two-stick pilot who spends saved Time boosts on the right trigger (2 s cooldown, none at Time L5); lime five-petal motes in the totem's proportions, each moving like its element; mote frequency weighted by the hull's petals; Charge rams on fauna; Eject below Charge L5 and Steal at it; shielded Mass clusters at Mass L5; Space L5 blasts through shields and super shields; the Omni crystal's burst of motes; and out-of-plane prisms seen with depth from the plane. Then re-run parity.cjs and verify_lab.cjs and republish. (who: any session; Garrett to sign off)
- T-20261018-8ff7 [open] content: Draw the Wrasse's four ability-card icons. Since round 4d the cards read the navigator (WrasseHUDController: Time = saved boosts, Charge = kite rams, Space = last blast's kills, Mass = flash on a cluster) and ElementalAbilityMaps/Wrasse.asset names them Space Blast, Saved Boosts, Mass Cluster and Kite Ram, but the sprites on the HUD (SparrowHUDVariant) are still the Sparrow's gun, missile and stance icons. (who: Garrett (vessels, icons); any session for the variant wiring)
- T-20261018-5c29 [open] test: Fly the Wrasse's round 4d in the Unity Editor: let BlockGraph and the swarm member shaders re-import with the new PrismDepthPlane.hlsl include, confirm the pilot's own view of prisms is unchanged, then run Docs/UNITY_VERIFICATION_CHECKLIST.md's Wrasse steps 2-4 (two-thumb flight, the HUD counts, the five-petal motes, out-of-plane prisms opening with distance from the plane and dithering above it). (who: Garrett (sign-off); Yash can run the editor pass)
- T-20261020-d09f [open] test: Round 7 of the Wrasse on cece/loving-fermat-ntdnbu: reimport Grouper.fbx and WrasseKite.fbx, open Wrasse.prefab, then fly it - the Grouper and Wrasse bodies and their puppetry, the whole-screen kite, the 30% zoom centred on the Grouper, yaw in place, the drawn cord, let go by lift / held click / held trigger, top speed 364 and the doubled lasso, and two machines seeing each other's Wrasse (who: Yash (offered) runs the Editor pass; Garrett judges the feel)
- T-20261020-a9f5 [open] dev: Fit the Wrasse hull's parts to the Grouper model: a hit box sized to Docs/VesselModels/Grouper.md (it is still the Sparrow's flat 10.3 x 0.46 x 4.4 box, the Grouper is 5.6 u tall), the two jets re-homed to the engine mouths (+-0.78, -0.30, -2.62), FlareEngine pointed at the Grouper's Engine slot, and the Wrasse's CrystalHullFusion bakes re-baked on the Grouper mesh (who: Yash (offered) or anyone with the Editor; Garrett signs off the fit)
- T-20261020-8dbc [open] test: Round 8 of the Wrasse on cece/loving-fermat-ntdnbu: on the kite, the hand stops at a line just above the ability cards while the kite swings past it and off the bottom; the Grouper's view fills the box left of the cards and below the line; the Grouper's view keeps a larger faint Wrasse stand-in. Check a phone and a wide monitor (who: Garrett (feel); Yash (offered) can run it)
- T-20261021-3d24 [open] test: Rounds 8b and 9 of the Wrasse on cece/loving-fermat-ntdnbu: the hand stops a small margin inside the screen's sides and top while the Wrasse swings past every edge on its cord; the four lasso elements' motes are four green-yellows with no blue or orange (Charge #a0ff00, Mass #5ec600, Space #40ff00, Time #9bcf00); say if any two are hard to tell apart in play (who: Garrett (feel and colour call); Yash (offered) can run it)
- T-20261021-d063 [open] test: Round 10 of the Wrasse on cece/loving-fermat-ntdnbu: the juice. No effect rings; each loop's motes become its effect (fizzle sputters out grey, Space explodes with the blast, Mass blocks morph into the prisms, Time streaks onto the Grouper, Charge orbits onto the Wrasse); the lasso cinches; the Grouper's small view no longer pumps in and out of its box (who: Garrett (feel: is it bespoke, unique, dynamic and polished?); Yash (offered) can run it)
<!-- team_report:end -->
