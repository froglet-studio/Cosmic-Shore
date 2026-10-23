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
- **An AI squad mate takes whichever seat is empty.** The swap (D-pad left / right, the left / right arrow keys, a two-finger swipe; `Docs/CREW_STATIONS.md`) moves you
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
- **Today, one human:** the D-pad, the left / right arrow keys (no direct picks since 2026-10-23) or a double tap on the small view
  steps to the other seat. The pick is remembered per hull class (`CrewSeatChoice`).
- **The phone boost:** ~~lifting both thumbs~~ (superseded the same day: Garrett, "The grouper can not use boost on
  mobile"). The Grouper boosts on RT on every device: the pad's trigger, right Shift, or on a phone **lifting the left
  thumb** while the right flies on; the Time card shows that glyph. The Orca's helm had the same gap (boost and shield
  read only the analog triggers) and is fixed the same way. `KITE_NAVIGATOR.md` "The phone boost". Not flown on a phone.
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

## 3b. The Orca: the 4-seat vessel (2026-10-20, branch `cece/happy-noether-m54hz6`)

The second crew vessel, Garrett's design from the **Crew of Four Lab**
([artifact](https://claude.ai/artifact/UaJcpaXzgkza2eHSQCZwmb), `Docs/Studios/CREW_OF_FOUR_LAB.md`). Its card reads
**Orca · 4 seat**: Garrett asked for multiplayer vessels to be named by their seats ("that lets players know it supports
four players or brings in 4 AI"), so `SO_Vessel` gained `SeatCount`, and the Wrasse's card now reads "Wrasse and
Grouper · 2 seat".
- **The seats.** One **helm** flies with two thumbs, boosts from a tank and rings the hull in danger prisms that leave
  the front open. Three **gunners** sit in gyro-stabilized first-person turrets on outriggers. Each gunner launches
  and flies missiles from one shared magazine. Omni crystals refill the magazine. The AI fills every empty seat.
- **The stabilization (lab round 4)** cut a gunner's unsteered view sway from 47–54 °/s to 3–6 °/s (12 seeds, every
  game). The gunner's aim lives in the world; the view's horizon is held, then eased; the turrets sit on outriggers
  that follow a lagged copy of the hull's frame.
- **In Unity:** `VesselClassType.Orca = 20`, `Orca.prefab` (the Sparrow without its kit, on two sticks), `OrcaCrew` and
  its engine-free core, the view, the HUD controller and the omni effect. It is in the Toybox Vessel Changer. The
  author tool is `Tools/Build/author_orca_assets.py`; the parity gate is `Tools/Build/orca_crew_lab/parity.cjs`.
  Design doc: `Assets/_Scripts/Controller/Vessel/Crew/ORCA_CREW.md`.
- **Checked:**
  - The C# core matches the page step by step, and the asset matches the lab's numbers; planted controls are caught.
  - Unity reference compile: no project errors.
  - 14 core tests pass under .NET.
  - All static gates pass.
- **Not checked: it has not been flown in the Unity editor.** It also runs on one machine only, like the Wrasse.
- **Status: a stub, shipped as one** (Garrett, 2026-10-21: "this is a good stub of a vessel"). Every seat, gun and
  missile is built and wired (Garrett flew it through round 7; rounds 8 and 9 are not flown yet); networking, level-5 upgrades, sounds, a prefab fitted to the model and its own
  HUD art are the follow-ups in `ORCA_CREW.md`.
- **Round 9 (2026-10-21).** After Garrett flew round 8: each flown missile is the Sparrow's skyburst rocket with an
  igniting motor, a light, a bank into the stick and a lit faceted plume trail (`OrcaMissileVisual`). The bomb's ring and
  "BURST" text are gone: the prisms its live bomb will break are lit through PrismLit's light bank, never the gunner's
  own. Garrett's "always go for the 3d in world solution over the 2d flat text solution" is now a LOCKED rule
  (`Docs/claude/SHADERS_AND_PERFORMANCE.md`). Not flown yet.
- **Round 8 (2026-10-21).** The minigun's model sat at an odd angle to its shot: the turrets' rest is now read from the
  skin's bind poses. The hull was rebuilt as a hovering assembly like the original vessels (17 floating parts, gaps
  1.6-2.3% of its size, `assembly_report.py`), its plates and engines animated, and the jets remounted on its engine
  mouths. Not flown yet.
- **Round 7 (2026-10-21).** After Garrett flew round 6: every shot now flies down its aim (it keeps only the hull's
  velocity along the aim), and the turret model, the crosshair and the shot share one line (posed and fired after the
  hull moves, from the barrel tips). The sniper looks over its turret with the Serpent's pip scope always open. In the
  lab, gun hits on rival hulls rise 60–130%. Not flown yet.
- **Round 6 (2026-10-21).**
  - **Every seat is an element**: Helm Time, Sniper Space, Fast gun Mass, Bomb Charge. The person's seat lights its
    ability card, and each gunner card shows its own missiles and its gun's recharge.
  - The **sniper is always scoped**; the **bomb** has no cooldown but one at a time (burst it to throw the next).
  - The **fast gun writes prisms like a pen**: clean strokes of thin prisms, 5 u apart, 120 u out, up to 10 pen rounds
    a second.
  - The fast gun and the bomb look **over their turret**; every gun has a **bespoke crosshair** (scope, nib, blast ring).
  - Lab cost: the no-cooldown bomb doubles the hits both ways, and Anchorage falls to 73 (from 120).
  - **Its own hull model** (`Orca.fbx`, `/vessel-model`): an orca-inspired spaceship with three bespoke turrets the crew
    aims (`OrcaHullRig`). Jets, hit box and chase camera are still the Sparrow's and need fitting.
  - Same status: compiled and parity-checked, not flown in the editor.
- **Round 5 (2026-10-21).**
  - Each gunner seat has **its own gun**: gunner 1 the Serpent's sniper (12 s cooldown; every hostile prism in a thin
    cone dies, super-shielded ones included), gunner 2 the Brittle Star's rounds (overheats), gunner 3 the Grizzly's
    bomb (it never pushes the Orca).
  - Each seat has **its own missiles**, and an omni crystal gives every seat one.
  - The helm's shield **rides with the hull super-shielded, then drops in place as danger prisms**. That shield is the
    round's biggest measured gain: 120 against 77 in Anchorage.
  - Both crew cameras (the Orca's gunner view, the Wrasse's kite view) now have the game camera's post-processing.
  - Same status: compiled and parity-checked, not flown in the editor.

## 4. Open items, and what is asked of you

- **Fly it in the editor** (`T-20261018-5c29`; Yash can run the editor pass). Let the shaders re-import, check the pilot
  view's prisms look unchanged, then follow the Kite Navigator entry in `Docs/UNITY_VERIFICATION_CHECKLIST.md`.
  `T-20261017-aca2` is the original playtest task.
- **Build the two human seats** (`T-20261021-aa19`, Yash with Garrett). The decision (`T-20261017-b06e`) was made on
  2026-10-21: two seats, either device. The plan is in `KITE_NAVIGATOR.md`.
- **Register the seat as a duo design** (`T-20261017-efca`, open again). `cece/sharp-tesla-j0kd7z` moved the Goby to 18,
  its card reads "Goby and Pistol", and the Goby runs on the shared crew pieces (`ICrewSeat`, `CrewSeat`,
  `CrewPictureInPicture`, `CrewViewRender`, `check_crew_vessels.py`). That branch's move of the Wrasse onto them was
  withdrawn when it merged bleeding-edge again (2026-10-21): the Wrasse joins at step 2 of `KITE_NAVIGATOR.md`'s work
  order, after the branch lands.
- **Card icons:** the Wrasse still shows the Sparrow's four ability icons (`T-20261018-8ff7`).
- **Sounds:** eight empty FMOD slots (`T-20261017-3cbe`).
- **Catch the lab up** to rounds 4-4f (`T-20261018-3247`).
- **Fly the Orca in the editor**, round by round (`T-20261020-440c`, `T-20261021-9ef8`, `T-20261021-a73f`,
  `T-20261021-07cd`; Garrett or Yash), and **fit its prefab to the model** (`T-20261021-8afe`).

## 5. Latest activity (generated)

<!-- team_report:begin (generated by Tools/Build/team_report.py; do not edit) -->
_Generated 2026-10-21 from git (last 14 days), who_works_where.py and Docs/TEAM_TASKS.md: `python3 Tools/Build/team_report.py area crew-vessels --days 14`._

### Recent changes (25+)
- 2026-10-21 `00793bd50` docs(crew-lab): name round 8 in the lab page's header comment — on another branch
- 2026-10-21 `d08d78afd` feat(orca): rocket missiles with a faceted plume; the bomb's blast lit in the world, not a BURST ring — on another branch
- 2026-10-21 `a5bbd6454` feat(honeyguide): Garrett's first-play notes - centred board, toasts, right-click, cursor prisms, slip zip — on another branch
- 2026-10-21 `86d89e91f` feat(crew): pick and keep your seat on the Wrasse, on every device; the online plan — on bleeding-edge
- 2026-10-21 `e41f4d494` fix(honeyguide): draw the Dynamo board from the screen's corner, not its centre — on another branch
- 2026-10-21 `98c69145c` feat(orca): the hull rebuilt as a hovering assembly, its floating parts animated — on another branch
- 2026-10-21 `c47e51195` fix(orca): turret rest from bind poses, jets on the model's mouths; vessels are hovering assemblies — on another branch
- 2026-10-21 `5c9451c81` feat(honeyguide): the Honeyguide and Ratel in the Toybox Vessel Changer — on another branch
- 2026-10-21 `3d7c0c430` docs(crew): ship pass - view doc current, re-measured follow-up, lab learnings, refactor row — on bleeding-edge
- 2026-10-21 `a859581c4` fix(orca): shots fly down the aim; sniper keeps the Serpent's pip scope open (lab round 7) — on another branch
- 2026-10-21 `6fd866b2d` feat(crew): the Wrasse's juice - every effect is its motes' own petals, no rings; a steady small view — on bleeding-edge
- 2026-10-21 `e0050f8e9` feat(orca): its own hull model with three code-aimed turrets (/vessel-model) — on another branch
- 2026-10-21 `abdec3ecf` feat(orca): seats are elements, scoped sniper, one-at-a-time bomb, the fast gun's pen, crosshairs (lab round 6) — on another branch
- 2026-10-21 `920bc7e5d` feat(crew): the Wrasse's four limes are green-yellows - no blue, no orange — on bleeding-edge
- 2026-10-21 `072a96353` feat(crew): only the cord holds the Wrasse; its four lasso elements are four limes — on bleeding-edge
- 2026-10-21 `c5985f500` feat(orca): three distinct guns per seat, per-seat missiles, carried super-shield (lab round 5) — on another branch
- 2026-10-20 `b4fc7ddbf` feat(crew): the Wrasse's hand line above the totems; the Grouper's view fills the corner — on bleeding-edge
- 2026-10-20 `d42f8b177` feat(labmaker): Honeyguide Lab round 1 and the engine-free Dynamo core — on another branch
- 2026-10-20 `2adae812d` feat(crew): the Orca, the 4-seat vessel, in the Toybox Vessel Changer — on another branch
- 2026-10-20 `bc311e6c0` feat(lab): Crew of Four Lab round 4 - stabilized gunner turrets on outriggers, the 4-seat vessel — on another branch
- 2026-10-20 `01c24e802` feat(crew): the Wrasse and Grouper get real bodies; the kite flies the whole screen — on bleeding-edge
- 2026-10-20 `1719e93f9` feat(lab): Crew of Four Lab round 3 - one helm, three gunner seats, flown missiles — on another branch
- 2026-10-20 `47c0d54ca` feat(crew): AI Wrasses are two AIs; the flail trips toys, not the kite; one Sling for every input — on bleeding-edge
- 2026-10-20 `0c72f1eac` feat(lab): Crew of Four Lab round 2 - 3D with the game's chase camera — on another branch
- 2026-10-20 `b6293d5a3` feat(crew): the Wrasse's Sling, switches, and Wrasse Run (GameModes 68) — on bleeding-edge

### Also in flight on other branches
- `cece/happy-noether-m54hz6` (unattributed, 2026-10-21): docs(crew-lab): name round 8 in the lab page's header comment
- `claude/honeyguide-ratel` (unattributed, 2026-10-21): feat(honeyguide-lab): round 5, timed actives on the open buttons, no guns
- `cece/sharp-tesla-j0kd7z` (unattributed, 2026-10-19): fix(team-tasks): --check fails on merge conflict markers left in the board

### Open tasks (Docs/TEAM_TASKS.md)
- T-20261017-efca [open] dev: When the duo-vessel system (cece/sharp-tesla-j0kd7z) reaches bleeding-edge, register the Kite Navigator as a duo design so a hull can carry it from a mode or a menu, not only from the play-mode FrogletTools menu; keep its one swap (PilotSwapGesture) and its AI-in-every-empty-seat rule. (who: Garrett (duo-vessel owner))
- T-20261017-3cbe [open] content: Author FMOD events for the Kite Navigator's eight empty EventReference slots on KiteNavigatorConfig.asset: loop closed, loop fizzled, prism popped, Mass cluster, Time boost, Charge, steal, fauna struck. (who: anyone who does audio)
- T-20261017-aca2 [open] test: Playtest the Wrasse in the Unity Editor: open the Toybox, fly the Wrasse station in the Vessel Changer, then the 9 verify steps of its checklist entry (board draws, AI loops, 2/D-pad right swaps to the kite, each element's loop fires, a charged ram steals and scores, 1 swaps back with no stuck input or boost, the changer swaps cleanly in and out of the Wrasse). It has only been compiled headlessly. (who: Yash (offered), anyone with the Editor)
- T-20261018-3247 [open] dev: Bring the Crew Vessel Lab up to the Wrasse's rounds 4-4f, which were directed in Unity on 2026-10-18 (KITE_NAVIGATOR.md "Where it stands"). They cover: the kite view and the pilot view swapping (the small view lower left); the kite following the mouse or stick with no click; a two-stick pilot who spends saved Time boosts on the right trigger (no cooldown since 2026-10-21; Time L5 is an open slot); lime five-petal motes in the totem's proportions, each moving like its element; mote frequency weighted by the hull's petals; Charge rams on fauna; Eject below Charge L5 and Steal at it; shielded Mass clusters at Mass L5; Space L5 blasts through shields and super shields; the Omni crystal's burst of motes; and out-of-plane prisms seen with depth from the plane. Then re-run parity.cjs and verify_lab.cjs and republish. (who: any session; Garrett to sign off)
- T-20261018-8ff7 [open] content: Draw the Wrasse's four ability-card icons. Since round 4d the cards read the navigator (WrasseHUDController: Time = saved boosts, Charge = kite rams, Space = last blast's kills, Mass = flash on a cluster) and ElementalAbilityMaps/Wrasse.asset names them Space Blast, Saved Boosts, Mass Cluster and Kite Ram, but the sprites on the HUD (SparrowHUDVariant) are still the Sparrow's gun, missile and stance icons. (who: Garrett (vessels, icons); any session for the variant wiring)
- T-20261018-5c29 [open] test: Fly the Wrasse's round 4d in the Unity Editor: let BlockGraph and the swarm member shaders re-import with the new PrismDepthPlane.hlsl include, confirm the pilot's own view of prisms is unchanged, then run Docs/UNITY_VERIFICATION_CHECKLIST.md's Wrasse steps 2-4 (two-thumb flight, the HUD counts, the five-petal motes, out-of-plane prisms opening with distance from the plane and dithering above it). (who: Garrett (sign-off); Yash can run the editor pass)
- T-20261020-d102 [open] test: Playtest the Crew of Four Lab (round 4, the 4-seat vessel) by hand, on PC and phone: take the helm and a gunner seat in each of the four games at I2, compare the Round 4 preset (stabilized turrets on outriggers) with the Hull-locked turrets control from a gunner seat, and record on the Decisions tab which seats felt like a job, which felt like waiting, and whether the turret view feels steady enough. The AI cannot feel sway (its points are the same with and without stabilization), and the shield's worth is within noise, so a person at the helm decides both. (who: Yash (offered), Garrett, anyone)
- T-20261020-1607 [open] decision: Rule on the Crew of Four Lab round 3 questions: the ammo economy (missiles per omni crystal and the omni share; 3 and 40% proposed because 2 and 25% left magazines empty 70-80% of a match), a shared magazine for the three gunners or one per seat, whether AI gunners keep a missile for a human gunner, the helm's shield riding with the hull or dropped in place (its 50-70 u radius is the DangerHemisphereConfig asset's), what gives gunners a job in the Ring Race (they are worth nothing there today), and how many petals a blast takes from a hull (2 proposed). (who: Garrett (vessels, product decisions))
- T-20261020-440c [open] test: Fly the Orca (the 4-seat vessel) in the Unity editor from the Toybox Vessel Changer on cece/happy-noether-m54hz6: the helm (two thumbs, right-trigger boost tank, left-trigger danger shield), then keys 2-4 for each gunner turret (stabilized aim while the AI turns the hull, Release launches a missile you fly, a second Release bursts it, an omni crystal through a missile adds 3 to the Charge card's count), then 1 back to the helm. Steps and expected results: Docs/UNITY_VERIFICATION_CHECKLIST.md 'The Orca'. (who: Garrett (owner), Yash (offered))
- T-20261020-3967 [open] decision: Orca design calls the port left open: (1) the four level-5 upgrades (Charge missiles, Space burst, Mass shield, Time boost tank - none designed, every UpgradeLabel is empty), (2) the hull's turn rates (the lab's 0.55 x Sparrow gives roll 16.5, likely slow - the Wrasse went to 120), (3) the shield on a touch screen (the left trigger has no touch equivalent), (4) which machine decides bursts once the crew is networked. (who: Garrett (vessels, product decisions))
- T-20261021-9ef8 [open] test: Fly the Orca's round 5 in the Unity editor on cece/happy-noether-m54hz6, and check the crew cameras' post-processing: the Wrasse's kite view and the Orca's turret view should have bloom like the pilot view; the helm shield rides white (super-shielded) then drops red (danger) ~4 s later and stays ~20 s; seat 2 sniper beam kills a super-shielded wall and counts down 12 s; seat 3 fast gun overheats after ~6 s; seat 4 bomb bursts on a second press and never pushes the Orca; an omni crystal gives every seat a missile. Steps: Docs/UNITY_VERIFICATION_CHECKLIST.md 'Orca round 5'. (who: Garrett (owner), Yash (offered))
- T-20261021-a73f [open] test: Fly the Orca's round 6 in the Unity editor on cece/happy-noether-m54hz6: the person's seat lights its ability card (helm Time, 2 Space, 3 Mass, 4 Charge) and each gunner card shows its own missiles; the sniper is always scoped with its ring and countdown; the fast gun's view sits over the turret and a slow sweep draws a clean line of thin prisms ~120 u out; the bomb has no cooldown, one at a time (Fire throws, Fire bursts, the next press throws again); every gun has its own crosshair. (who: Garrett (owner), Yash (offered))
- T-20261021-8afe [open] dev: Fit the Orca prefab to its new model (Orca.fbx, 36 u long, 49 u across): the hit box, skimmer, jets (move them to the model's jet.L.mouth / jet.R.mouth, z -14.75) and flare are still the Sparrow's, sized for a 12 u hull; decide the chase camera distance (the convention says ~113 u back, the lab's camBack is 50: change it in the lab first). (who: Garrett (owner, the feel), Yash (offered))
- T-20261021-3fbb [open] test: Fly the Orca's round 7 in the Unity editor on cece/happy-noether-m54hz6, boosting and turning hard: the fast gun's rounds and the bomb leave the barrel tips and fly straight through their crosshairs (not sideways with the hull); the sniper's view is normal with the Serpent's pip scope always open in the upper left and the beam passing through both reticles; missiles leave the turret tubes; the Serpent's own scope is unchanged. (who: Garrett (owner), Yash (offered))
- T-20261021-07cd [open] test: Fly the Orca's round 9 in the Unity editor on cece/happy-noether-m54hz6: launch a missile from any gunner seat (F): it is the skyburst rocket with an igniting, flickering motor, it banks into your turns, and a lit faceted plume trail swells behind it and stays after the burst; in seat 4 the bomb has no ring and no BURST text, and while a bomb is out the prisms its blast will break (not yours) glow in your colour. (who: Garrett (owner), Yash (offered))
- T-20261020-d09f [open] test: Round 7 of the Wrasse on cece/loving-fermat-ntdnbu: reimport Grouper.fbx and WrasseKite.fbx, open Wrasse.prefab, then fly it - the Grouper and Wrasse bodies and their puppetry, the whole-screen kite, the 30% zoom centred on the Grouper, yaw in place, the drawn cord, let go by lift / held click / held trigger, top speed 364 and the doubled lasso, and two machines seeing each other's Wrasse (who: Yash (offered) runs the Editor pass; Garrett judges the feel)
- T-20261020-a9f5 [open] dev: Fit the Wrasse hull's parts to the Grouper model: a hit box sized to Docs/VesselModels/Grouper.md (it is still the Sparrow's flat 10.3 x 0.46 x 4.4 box, the Grouper is 5.6 u tall), the two jets re-homed to the engine mouths (+-0.78, -0.30, -2.62), FlareEngine pointed at the Grouper's Engine slot, and the Wrasse's CrystalHullFusion bakes re-baked on the Grouper mesh (who: Yash (offered) or anyone with the Editor; Garrett signs off the fit)
- T-20261020-8dbc [open] test: Round 8 of the Wrasse on cece/loving-fermat-ntdnbu: on the kite, the hand stops at a line just above the ability cards while the kite swings past it and off the bottom; the Grouper's view fills the box left of the cards and below the line; the Grouper's view keeps a larger faint Wrasse stand-in. Check a phone and a wide monitor (who: Garrett (feel); Yash (offered) can run it)
- T-20261021-3d24 [open] test: Rounds 8b and 9 of the Wrasse on cece/loving-fermat-ntdnbu: the hand stops a small margin inside the screen's sides and top while the Wrasse swings past every edge on its cord; the four lasso elements' motes are four green-yellows with no blue or orange (Charge #a0ff00, Mass #5ec600, Space #40ff00, Time #9bcf00); say if any two are hard to tell apart in play (who: Garrett (feel and colour call); Yash (offered) can run it)
- T-20261021-d063 [open] test: Round 10 of the Wrasse on cece/loving-fermat-ntdnbu: the juice. No effect rings; each loop's motes become its effect (fizzle sputters out grey, Space explodes with the blast, Mass blocks morph into the prisms, Time streaks onto the Grouper, Charge orbits onto the Wrasse); the lasso cinches; the Grouper's small view no longer pumps in and out of its box (who: Garrett (feel: is it bespoke, unique, dynamic and polished?); Yash (offered) can run it)
- T-20261021-aa19 [open] dev: Build two human seats on the Wrasse and Grouper (Garrett, 2026-10-21: "wrasse and grouper, are two seats. players can select, change seats etc. both can be played on mobile or pc"), in the plan's order: (1) prism parity for one human online - pops, Space blasts and Mass clusters (seeded) applied on every peer, which today happen only on the Wrasse's machine; (2) land cece/sharp-tesla-j0kd7z and put the seat on ICrewSeat; (3) seat occupancy on the Player (CrewSeatOccupancy, RequestSeat_ServerRpc, PilotSwap.TryValidateSeatServer, offers between two humans, AI fills an empty seat); (4) the navigator seat simulated on its holder's machine, boosts and rams sent to the hull owner; (5) one owner for the swap gesture on a crew hull (seats first, then the arena ring); (6) seat stations in the Vessel Changer and two party members on one crew hull. (who: Yash (multiplayer) with Garrett; builds on cece/sharp-tesla-j0kd7z's CrewSeat)
<!-- team_report:end -->
