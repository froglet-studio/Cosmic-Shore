# Kite Navigator: the Kite & Tail crew seat (on the Wrasse)

A crew seat for a second player on one hull. It ships on the **Wrasse** (`VesselClassType.Wrasse = 19`, card **Wrasse
and Grouper**): the navigator's kite is the Wrasse, the pilot's hull the Grouper. The design is the Crew Vessel Lab's
round 3 candidate, which Garrett signed off on 2026-10-17 (`Docs/Studios/CrewVesselLab.html`, write-up in
`Docs/Studios/CREW_VESSEL_LAB.md`), then **rounds 4 to 4f (2026-10-18), which Garrett directed in Unity after flying
it**; see "Round 4" below. The rounds after 3 are not in the lab yet (task `T-20261018-3247`).

**Where it stands (after round 6, 2026-10-20)**, in one place; the rounds below say why each part is the way it is:
- **The Grouper (pilot):** the Sparrow's hull with its guns, missiles and buttons removed, flown on two sticks
  (`VesselTransformer`; pitch / yaw 100, roll 120). The right trigger (right Shift; on a phone, lift the left thumb
  and fly on with the right) spends a boost the Wrasse saved; the Time card names it ("The phone boost", below).
- **The Wrasse (navigator):** a kite on the hull's x-z plane, held on an invisible rope by EVERY input (**the Sling**,
  round 6): a thumb (drag and it trails, small circles whirl it round in big ones, lift to let it fly), the mouse and
  a thumbstick (hands that never let go; the stick places the hand on the board) and the AI squad mate. Its head
  pops hostile prisms and rams rivals and creatures, and threads every race's rings (round 6). It does not trip
  Toybox toys; the Thresher's ball does.
- **AI crews:** an AI-flown Wrasse is two AIs, the hull's `AIPilot` for the Grouper and the navigator AI for the
  Wrasse (round 6b). Its tail lassos motes of one element, drawn as lime five-petal
  flowers in the totem's proportions, each moving like its element.
- **The views:** the kite view is a second, top-down orthographic camera drawn by the game. It is a small square in the
  lower left while you pilot and full screen while you navigate, with the pilot camera swapped into the square. Out of
  the plane, prisms open with distance FROM the plane and dither in a cone above it. The pilot sees a faint kite and
  faint dots.
- **The HUD:** the Time card counts saved boosts, Charge the kite's rams, Space the last blast's kills, and Mass flashes
  on a cluster (`WrasseHUDController`).
- **L5s:** Mass clusters arrive shielded, Charge steals instead of knocking crystals loose, Space breaks shields and
  super shields, and Time drops the boost cooldown. An **Omni crystal** drops a cluster of each element on the board.
- **Seats (2026-10-21):** keys 1 / 2 pick the Grouper or the Wrasse, the D-pad and a double tap on the small view step
  to the other seat, and the pick is remembered per hull class (`CrewSeatChoice`), so the person starts where they last
  chose. Online it is two human seats on any mix of phones and PCs: decided by Garrett 2026-10-21, planned in "Two
  humans, two seats" below, not built yet.
- **Unverified:** nothing has been played in the editor yet (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, task
  `T-20261018-5c29`), and the seat still runs on one machine (the plan below).

One player flies the hull. The **navigator** plays a one-thumb 2D game on a **board**, which is the hull's own x-z plane
with the world projected onto it. The navigator flies a **kite**:

- **The head** pops hostile prisms it touches (`Prism.Damage`). It also rams rival hulls.
- **The tail** is a lasso. When the kite crosses its own tail, it closes a loop.
- **A loop round motes of one element** fires that element's effect. The effect grows by `base × growth^(n − 1)` for
  n motes, so with growth 2 each extra mote doubles it:

| Element | Effect | In the game |
|---|---|---|
| Space | a blast at the loop's centre that destroys hostile prisms | `ExplosionHelper.CreateExplosion` with `AOEExplosion.prefab`, sparing our domain; `MaxScale = 60 × 2^(n−1)` (cap 640) |
| Mass | a cluster of our domain's prisms | the hull's own `PrismSpawnChannel` and prism type, sized like the Sparrow's trail prism (2 × 2 × 5); `6 × 2^(n−1)` prisms (cap 240), at most 40 laid per frame |
| Time | boosts saved for the pilot | `round(2^(n−1))` boosts of 1.5 s each (at most 20, the lab's 30 s cap), spent one at a time on the pilot's trigger (round 4d) |
| Charge | kite power | `1 × 2^(n−1)` petals; the next ram on a rival knocks out that many, and a ram kills a creature for one |

A loop that holds more than one element **fizzles** (`mixedLoops = Fizzle`).

**A ram is a scored Strike.** PvP is petals only (LOCKED). The ram goes through the same gate as every contact weapon:
`ElementalTransfer.IsDecidedHere`, then `CombatHitDrain.TryAdmit(…, CombatHitClass.Strike, stealCooldown,
ElementalDebuffSources.VesselContact)`. It then moves whole petals one at a time from the rival's fullest element
(`ElementalTransfer.ApplyAuthoritative`: the Eject form, crystals anyone can take; the Steal form at Charge L5).
Finally it raises `OnCombatHitLanded`, the scoreboard's channel.

**Motes belong to the navigator.** Only the kite view draws them in their petal shapes, the pilot sees faint dots, and
they are not game crystals.

## Round 4 (Garrett, 2026-10-18)

| Asked | In the game |
|---|---|
| The kite and its petals visible to the pilot but not in the way | The pilot's camera sees a faint see-through kite (`pilotKiteAlpha` 0.22) and faint lime dots (`pilotDotAlpha` 0.35), drawn only for that camera (`RenderParams.camera`). |
| The kite's screen small in the lower left while piloting; swapped when playing the kite | `KiteNavigatorView`: piloting, the kite view is a square `pipSize` (0.32 of the screen height) in the lower left. Navigating, the kite view is full screen and the pilot camera (`CameraManager.ViewCamera`) is lifted over it in the SAME small square (round 4b: "the kite view when flying the pilot was good; use that"); its rect and depth are restored on the way back. |
| Use the game's rendering for the kite | The kite view is a second camera looking straight down the hull's up axis onto its x-z plane, orthographic, so it draws the real world (round 4d: `viewDepth` 400 u above and below the plane, below). The kite, tail, motes and rings are world meshes drawn only for it. The 2D UI board (`KiteBoardGraphic`) is gone. |
| Prisms out of the plane: unlit, not gone (round 4b) | Superseded in round 4d: the prisms themselves are drawn, with depth measured from the plane (below). |
| Every element lime, told apart by its petal shape | Motes are lime, each drawn in its element's petal outline from the HUD (`ElementPetalGeometry.Outline`: Mass a triangle, Space a kite, Time a diamond, Charge a five-sided gem). |
| The kite follows the mouse or thumbstick; no click | Mouse: the kite flies to the cursor. Stick: it flies the way the stick leans, as fast as it leans (circle the stick, the kite circles). Touch: to a point 21 mm above the thumb. The last-used device leads. |
| The pilot is a one-thumb flyer with no buttons | No buttons: every ability binding is removed. Round 4d moved the hull back to two thumbs (below). |
| The pilot controls the boost: the trigger on a gamepad (round 4b) | RT: the hull binds `RightStickAction` to `CrewTriggerRT` (`CrewTriggerActionSO` → `KiteNavigator.SetTrigger`), raised by the pad's right trigger, right Shift, and a phone's left-thumb lift. Round 4d made the Time gift discrete saved boosts (below). (Round 4 boosted on a centred stick, which fired by itself whenever the pilot flew straight. Until 2026-10-21 the seat read `InputStatus.RightTriggerAnalog`, which no phone writes, and a phone boosted on both thumbs off: "The phone boost", below.) |
| The ship's petals set how often each element comes | weight = `moteBaseWeight` (1) + `moteWeightPerLevel` (1) × the hull's level in it. |
| Charge works on fauna | A charged head kills a creature it passes through (`Fauna.Jousted`, sim authority only), for `faunaChargeCost` (1). The AI rams creatures too. |
| Mass clusters shielded at Mass L5 | `prismProperties.IsShielded` before `Initialize`, as the Thresher's armoured trail. |
| Charge: knock crystals out, steal them at Charge L5 | The ram's form: Eject, then Steal at L5. |
| Space blasts destroy shielded and super-shielded prisms at Space L5 | Super shields inside the blast's reach are dropped first (`Prism.DeactivateShields`, the Rhino sword's sequence), and the blast goes out with `DevastatingOverride = true`. |
| The lasso 50% longer (round 4b) | `tailLength` 1.6 → 2.4 board radii (384 u); the lab's default moved with it (parity's asset check holds them equal). |
| An Omni crystal: 5 s of a longer lasso and a kite view zoomed out twice as far | Replaced in round 4e by a burst of motes (below). |
| Boosts: 2 s cooldown, none at Time L5 | Retired 2026-10-21 (Garrett: "the grouper should not have a cooldown on its boost forward ability. as it is already limited by the wrasse's ability to collect"). A held trigger chains saved boosts back to back; the field is gone and Time L5 is an open slot. |

**Round 4b (the same day, after flying round 4):** the petals could not be seen. Every instanced draw used
`Sprites/Default`, whose instanced path takes its colour from `unity_SpriteRendererColorArray`, which only a
SpriteRenderer fills, so it drew at zero alpha. Instanced draws now use URP/Unlit set up as a transparent, double-sided
surface (as GobyPistolDuo draws). The tail and the loops, single meshes whose vertices carry their fade, keep
`Sprites/Default` drawn without instancing.

**Round 4c (2026-10-18):**
- **No Sparrow kit.** "The pilot is shooting when I am controlling the kite": the Sparrow's `AIPilot.abilities` cycled
  its guns and missiles whenever the autopilot flew, which is whenever the human is on the kite. `author_wrasse_assets.py`
  now strips the Sparrow's kit from the Wrasse:
  - the AI's ability cycle;
  - the guns, the bullet and Skyburst missile factories and their four action executors;
  - the missile rearm, the barrel roll, the Omni ward and the Time-L5 immunity;
  - the Omni ward effect from `WrasseImpactorDataContainer`;
  - the gun telemetry, which becomes `DefaultVesselTelemetry`.

  `--check` holds the prefab to having none of them and no dangling local reference.
  - **Kept:** the hull's flight, trail, model, animation, camera, jets, the hull-versus-prism effects, the AI's boost
    policy (`check_vessel_shared_channels` requires it; it is inert with no boost binding), and the `ResourceSystem`'s
    three Sparrow meters (shared world effects index a hull's meters by number, so emptying the list risks
    out-of-range; nothing fills them now).
  - **Still the Sparrow's** (in 4c): the HUD and the ability map. Round 4d gave the Wrasse its own (below).
- **Each mote wears its crystal's effect, in lime** (superseded by round 4d's five-petal flowers), round its petal (`KiteNavigatorView.CrystalEffect`, from the shipped
  crystals' numbers):
  - Charge: the seam walk, a bolt hopping round a pentagon every ~0.2 s with the last two seams thinning behind it.
  - Mass: the columns, six small triangles climbing out to 1.5× and down to 0.6 scale, then dropping back.
  - Space: the group spins, five kites turning 72° in 1.0 s alternating with an inner three turning 120°.
  - Time: the flip wave, eight diamonds flipping ring by ring on a 2 s loop.

**Round 4d (2026-10-18):**
- **Two thumbs.** "Lets go back to two thumb flying for the pilot so it can choose the fly in a way that brings more mass
  into plane for the kite." `author_wrasse_assets.py` swaps the Sparrow's `SingleStickVesselTransformer` for its base,
  `VesselTransformer` (no field of its own, so the swap is the script guid); `--check` holds it. The Wrasse left
  `OneThumbVesselCoverageTests.OneThumbVessels` and joined the two-stick row of `ONE_THUMB_MOUSE_CONTROLS.md`.
  - **The hull still has the Sparrow's tuning**: `ThrottleScaler` 50, `MinimumSpeed` 10, pitch/yaw 80, roll 30. On two
    sticks the throttle is the sticks' spread (`XDiff`), so top speed is the Sparrow's 60, reached by spreading the
    thumbs; the two-stick hulls (the Squirrel: 0 / 60 / 120 / 120 / 130) turn harder. Re-tune on the Wrasse in Unity.
- **The HUD reads the navigator.** `WrasseHUDController` replaces `SparrowHUDController` on the hull (the generator
  retrofits it, wired to the seat). On the Sparrow's HUD view:
  - **Time:** the saved boosts as a number, and the boost's recharge on the card's veil.
  - **Charge:** the rams the kite holds, as a number. The icon is shown again: the Sparrow's view hides it until its guns
    report.
  - **Space:** how many prisms the last Space blast destroyed, counting up while it runs.
  - **Mass:** the card flashes each time a Mass cluster is laid (`PlayAbilityFlash`).

  The counts are generated into the cards at runtime (the Shrike's missile count is the pattern). Their text is the
  `countTemplate` field (`{count}`), so the words are the team's. The Wrasse's ability map has its own cards now ("Space
  Blast", "Saved Boosts", "Mass Cluster", "Kite Ram", each with its L5), every input 0, so no Sparrow button chip is
  drawn.
- **Five petals per mote, moving like its element.** Each mote is a five-petal flower, as the element amount indicator
  draws (`ElementalBarsConfigSO` petal count and spacing). This replaces round 4c's crystal effects.
  - Space spins, 72° every 1.1 s.
  - Time flips all its petals, one after another on a 2 s loop.
  - Mass moves each petal in and out.
  - Charge runs a blue plasma spark round each petal's outline.
- **Saved boosts.** A Time loop of n motes saves `round(Grow(1, n, growth, max))` boosts, up to
  `timeBoostCap / timeBoostSeconds` (20). Each trigger pull spends one, for `timeBoostSeconds` (1.5 s). The kite view
  shows one lime dot per saved boost round the hull.
- **Out-of-plane prisms never disappear.** "They should just use the normal prism depth shader instead but use distance
  from the plane for depth. For the prisms above the plane just use the dither effect in a cone to preserve the viewing
  area visibility."
  - The kite camera sits `viewDepth` (400 u) over the plane and sees as far below it. The ghost boxes and the ±reachY
    clip are gone.
  - **Depth from the plane:** the prisms' own depth look is the face spread (faces part with distance). For this
    camera's render only, it measures distance OUT OF THE PLANE: `PrismDepthPlane.cs` publishes `_PrismDepthPlane` in
    `beginCameraRendering` and zeroes it in `endCameraRendering`. It is read by `PrismDepthPlane.hlsl`, through
    `PrismFlightSqrDistance` (every BlockGraph prism) and the swarm members' spread.
    - A prism `depthFullSpreadAt` (60 u) from the plane reads as far as the pilot view's farthest mass.
    - The touchable slice stays crisp.
    - Every other camera reads the all-zero default and is unchanged.
  - **The dither cone above the plane:** for the same render, the occlusion corridor is re-shaped into a cone from the
    kite lens down to the plane, sized so its core covers the view at the plane
    (`PrismOcclusionCorridor.BeginViewOverride`, identity-guarded to the bound hull, saved and restored).
    - The corridor's length is 1.5× the lens height, so its base is solid exactly at the plane.
    - Prisms above the board dither (the shipped SHATTER kernel) to `ditherCoreAlpha` (0.35) in the core.
    - The touchable slice reads nearly solid; prisms on and below the plane are solid.
    - `ditherNearScale` (0.5) is the cone's radius at the lens, as a share of its radius at the plane.
  - The kite, its tail, the motes and the rings are drawn just under the lens, so no prism covers them.

**Round 4e (2026-10-18, after flying 4d):**
- **The Omni crystal drops motes; no zoom** ("I don't like when it zooms out from the omni crystal ... if they are low on
  elemental crystals the omnicrystal can inject a bunch to make up for low production"). `KiteNavigator.OnOmniCrystal`
  lays one cluster of each element (`omniMotesPerElement`, 6) evenly round the board at `omniClusterRadius` (0.5 R),
  each with a pop ring, over the usual mote count. The zoom fields (`omniSeconds`, `omniBoardScale`, `omniZoomRate`)
  are gone.
- **The flowers, set apart.** Each petal's inner tip sits `petalGap` (0.5) petal-lengths off the centre, and is
  `petalWidth` (0.65) of its shape's width. A flower is now 1.5× as wide.
  - **Space** spins without pausing (`spaceSpinDegreesPerSecond`, 150). Round 4d stepped 72° at a time, and a
    five-petal flower turned 72° looks exactly the same, so the spin could not be seen.
  - **Mass** moves out and back 20% further (`massPulse` 0.6 → 0.72).
  - **Time** flips as before, with the separation.
  - **Charge**: a flickering blue halo under each petal shows as a plasma rim (`chargeHaloScale` 1.35), with a bright
    spark lapping each petal's outline (`chargeSparkSize` 0.32 of the mote). Round 4d's plasma bars were about a pixel
    wide in the full-screen view, which is why nobody saw them.
- **A more agile pilot, the roll especially.** Pitch and yaw 80 → 100, roll 30 → 120. On two sticks, `RollScaler` is a
  real roll axis; the Sparrow's 30 only banked its single stick into turns. `author_wrasse_assets.py` writes these once,
  while the hull still carries the Sparrow's numbers, so a value tuned in Unity afterwards is never overwritten.

**Round 4f (2026-10-18):**
- **The totem's proportions** ("lets have all of the blooms get closer to matching the sizes shapes and spacing of the
  ones used to display elements above the ability icons"). Each petal now sits where the totem's element flower puts
  it, measured from `Resources/ElementPetals/<element>_petal.png` (each sprite is the whole flower, turned 72° per
  petal; traced from its alpha). In flower radii:

  | Petal | Centre out | Outline scale |
  |---|---|---|
  | Charge | 0.445 | 0.367 |
  | Mass | 0.395 | 0.453 |
  | Space | 0.461 | 0.383 |
  | Time | 0.461 | 0.383 |

  Every petal is scaled uniformly. Round 4e's `petalWidth` squashed every shape, which is why the Space dart looked
  wrong, so it is gone, and so is `petalGap`. `moteSize` is now the flower's radius (4.5 u).
- **Mass:** three petals held in, two pushed out by `massPulse` (0.72 of the radius) on a bump. A new pair, never
  neighbours, takes over every `massStepSeconds` (0.5 s). `massPetalsOut` is 2.
- **Charge:** ONE blue bolt laps all five petals. It runs each petal's outline from the apex, then hops to the next,
  every `chargeBoltLapSeconds` (1.6 s), with `chargeBoltTrail` (7) fading sparks behind a head of `chargeSparkSize`
  (0.22 of the radius). The per-petal halos and sparks are gone.

Every "L5" is the fleet's gate, `ElementalAbilityHandler.IsUpgradeActive(element)`. The Wrasse's map lists all four
elements: each unlocks at level 5 and relocks below 4.

## Round 5: the phone (Garrett, Android development build, 2026-10-19)

"I played wrasse and grouper but the wrasse and its lasso elements didn't show up. When I double tap the pip window to
the other vessel it should switch."

- **Nothing drew in a player build.** `KiteNavigatorView` took URP/Unlit from `Shader.Find`; nothing in the project
  references that shader, so the build stripped it, the fallback (Sprites/Default, no instancing) made
  `RenderMeshInstanced` throw every frame, and the small view showed the world with no kite, tail or motes. The shader
  now ships through `Assets/Resources/KiteNavigatorUnlit.mat` (transparent, instancing on) and the view tints copies of
  it. The same `Shader.Find("Universal Render Pipeline/Unlit")` shape is in 12 other runtime files (counted
  2026-10-19 with `grep -rln 'Shader.Find("Universal Render Pipeline/Unlit")' Assets/_Scripts`, Editor folders
  excluded; see Follow-ups). The material now puts URP/Unlit in the build, but only its transparent, instanced
  variants: those files now FIND the shader in a player, where they used to get null, and run on the nearest kept
  variant until each ships its own material.
- **Double-tap the small view to swap seats.** Two short taps (each under 0.3 s, within 0.45 s and 120 px of each
  other) inside the lower-left view call the same `PollSwap` the 1 / 2 keys and the D-pad do. Short is what keeps the
  flying left thumb, which lands in that corner, from counting. While navigating, a touch that starts on the small view
  no longer aims the kite. A mouse double-click does the same in the Editor.
- **The ability cards are buttons now** for every vessel, the Wrasse included (`Docs/ABILITY_LOCKUP.md`, "Tap a card").
  All four Wrasse map entries are `Input: 0`, so its cards are not pressable until they name an input.

## Round 6: the Sling, switches and Wrasse Run (Garrett, 2026-10-20)

| Asked | In the game |
|---|---|
| "Sling is excellent. bring it to unity" (the Wrasse Lasso Lab, `Docs/Studios/WRASSE_LASSO_LAB.md`) | `KiteNavigatorCore.Sling`, the lab's `stepKite` 'sling' + `ropeForce` + `rimBounce` line for line: the pointer is a hand on a `slingRope` 42 u rope that pulls back only when stretched (`slingStiffness` 260, `slingDamping` 5 on the stretch rate measured against the moving hand), `slingDrag` 0.35 while held, `slingFreeDrag` 0.9 once let go, `slingMaxSpeed` 520, bouncing off the board's rim (×1.4 of the outward speed). `pointerControl` (Sling, the asset's value) picks it for a touch and the mouse; no thumb offset (the rope keeps the kite off the thumb). A lifted thumb lets go; the mouse is a hand that never lets go (the no-click rule). A thumbstick leans the kite as before (Follow), and the AI squad mate aims (Follow). Held to the page by `Tools/Build/kite_navigator_lab/sling_parity.cjs` (3 240 frames, 10 loops, worst 0.002 × tolerance; three planted dial changes caught; the asset's 9 sling fields equal the lab's). |
| "make it so the wrasse can hit switches" | The kite is the hull's `IRingThreadProbe` (the Thresher ball's interface): `TryGetRingProbe` returns the kite's world point and an epoch bumped whenever the kite is re-placed. So it threads **every gate race's rings** (`GateRaceController`), the Skim Race's (`CrystalRaceRing`) and a mode preview's (`ModePreviewGateCourse`). (It tripped Toybox toys too until round 6b flipped that to the Thresher's ball.) False while no navigator is seated, so a remote hull threads with the hull alone. |
| Round 6b, the same day: "AI vessels should be controlled by two AI, one for the wrasse, and one for the grouper" | An AI-flown Wrasse that THIS machine simulates (`ShouldRun`: not the local pilot's hull, on autopilot, `Player.IsNetworkOwner`, not a local user) seats its kite too, flown by `KiteNavigatorAI`, with no view, swap, static handle or human input (`IsAICrew`). The hull's `AIPilot` is the Grouper's AI; it spends a saved boost while the next race ring is ≥ `aiBoostRingDistance` (180 u) away and ahead (`AIPilotWantsBoost`; outside a race, as they come) - the same rule now applies whenever the human navigates. `aiCrewNavigates` (on) switches it. |
| Round 6b: "lets flip things so the wrasse does not activate toys, but the thresher flail does" | The kite is an `IRingThreadProbe` only (race rings); `ThresherExecutor` is the `ISwitchThreadProbe` (the ball trips toys, `Toy.Update`). |
| Round 6b: "the gamepad and AI should be consistent with mouse and touch" | `control` (was `pointerControl`, `FormerlySerializedAs`) applies to every input. With Sling a thumbstick is a hand that never lets go, placed on the board as the mouse is: centred = the board's centre, a full lean `slingStickReach` (0.6) board radii out. The AI's aim is the hand on the rope (`KiteNavigatorAI.Control`): a loop is whirled with a hand circle `aiSlingLead` (0.25) × the rope that leads the kite a quarter turn round the cluster (the lab's sling pilot), a ring / rival / prism is reached by pulling the hand 0.6 ropes past it, and a loop plan ends when the kite closes a loop (`OnLoopClosed`). `KiteNavigatorSlingTests` runs the AI on the core: it whirls a loop round a cluster and drags the kite through a goal; planted defects fail both. |
| A Wrasse and Grouper race | **Wrasse Run** (`GameModes.WrasseRun = 68`, `Assets/_Scripts/Controller/Arcade/WRASSERUN.md`): a ring counts only when the kite threads it. While a gate race has a next ring for this hull (`GateRaceController.TryGetNextRing`): Time motes come more often (`raceTimeMoteBonus` 2 added to their weight), the AI squad mate values a Time loop ×`raceTimeLoopPreference` 3, and it flies the kite through the lit ring first whenever the board plane cuts its mouth (the ring's centre within 0.85 of its radius of the plane, its axis at least 0.35 in the plane), aiming a point just past the mouth along the ring's axis. |

## Round 7: two real bodies, the whole screen, the cord (Garrett, 2026-10-20)

| Asked | In the game |
|---|---|
| "use the new model building capabilities to give the wrasse and grouper proper 3d vessels with animations etc." | Two `/vessel-model` hulls: `Grouper.fbx` (`specs/grouper.py`, ~12 u, the Sparrow's span and length within 5%, a deep body with a serrated crest, wing-pod engines and a fan tail; bones `hull jaw fin.L fin.R dorsal tail.1 tail.2`) and `WrasseKite.fbx` (`specs/wrassekite.py`, 16 u, slim with two long vanes; bones `hull spine.1 spine.2 tail fin.L fin.R`). Both have four real element morphs, which the hull's `VesselAnimation` drives with no new code. Dossiers: `Docs/VesselModels/Grouper.md`, `WrasseKite.md`. `author_wrasse_assets.py` nests both under the root by name. The Grouper is the ship geometry, so the domain paint lands on its slot 0. The Sparrow's mesh renderer is off; its bones, jets, colliders and Animator stay. `WrasseCrewBodies` (on the root) animates them from measured motion. The **Grouper**'s tail beats faster with speed and bends out of a turn, its fins scull and bank, and its jaw opens and dorsal rises above cruise (a boost). The **Wrasse** swims a wave down its spine, paced by its speed across the board, and flaps its fins. The flat kite diamond is gone once the model draws (`BodyDrawsKite`). |
| "the wrasse and grouper should be visible to others" | The Grouper is the networked hull. The Wrasse is published by the machine that flies it (the pilot's, or the host for an AI crew) through `WrasseCrewBodies`' owner-write NetworkVariables: a seated flag, the kite's offset from the hull (0.05 u steps) and its rotation (1° steps). Every other peer places the model there, eased (`remoteSmoothing` 14/s), riding the hull's interpolated transform. The board, motes and effects stay local (Follow-ups). |
| "slow down the max speed of the wrasse to 70% of current" | `slingMaxSpeed` 520 → **364**. |
| "double the max length of the lasso" | `tailLength` 2.4 → **4.8** board radii (768 u). Both labs follow (`SHIPPED` / `tailLen`; the slider now reaches 8). The Crew Vessel Lab's "Long tail" preset moves to 7.2. |
| "Let the wrasse travel the whole screen space" | `wholeScreen` (on): the kite's bounds are the kite view's rectangle, not the 160 u disc. They are half depth R × `viewScale`, half width × the screen's aspect, less the kite's radius (`KiteNavigator.ScreenSettings`). `KiteNavigatorCore` clamps, bounces per axis (each edge reverses its own axis, ×1.4) and answers `InBounds` / `BoundsReach` on the rectangle; mote spawns, prism and creature queries, rival shadows and the AI's loop plans read the same bounds. |
| "zoom out 30% more and center the grouper in the wrasses camera" | `viewScale` **1.352** (1.04 × 1.3): the kite camera's `orthographicSize` is R × viewScale, centred on the hull (`ViewCentreZ` 0) instead of the board's old forward anchor. |
| "when the grouper yaws ... it should rotate in place instead of dragging all the crystals around. the wrasse camera can slowly move toward the groupers so it aligns after medium distances, but doesn't disturb the wrasse" | `KiteBoardFrame.Heading` (3, the default): the board's up follows the hull's at the old ease rate. Its forward turns toward the hull's heading by `1 − exp(−|speed| · dt / headingAlignDistance)` (220 u), so it catches up over distance flown, not time. A yaw in place leaves the board, its motes and the kite where they are. |
| "make the wrasse cord visible to the wrasse player" | While the rope is held (`CordHeld`), `KiteNavigatorView` draws it from the hand to the kite, `cordWidth` 0.6 u, `cordAlpha` 0.75. It shows in the kite view only; other players do not see it. |
| "holding a trigger pulled, lifting a thumb, or holding a mouse click should still make the cord disappear and fling the wrasse. releasing the trigger/click or touching back down reattached the tether" | Touch: lifting the thumb lets go and touching down grabs again (unchanged). Mouse: holding either button lets go (a press that starts on the small view is the swap's, not a let-go). Gamepad: holding either trigger lets go. Releasing grabs again, with the hand where the cursor or stick puts it. |

Gates this round:
- `sling_parity.cjs` passes at worst 0.006 × tolerance, and all four planted controls are caught.
  - Its "tow" scenario's hand swing moved 60 → 64 u. At the new top speed, the 60 u swing grazed the 42 u rope at 41.97 u for one frame. Float32 C# and the page's doubles disagree on "taut" at that edge, so the gate was comparing rounding.
  - It now reports its first structural mismatch apart from its worst one.
- `parity.cjs` passes, and so do both labs' `verify_lab.cjs`.
- `KiteNavigatorSlingTests` gained three whole-screen tests. Its tow test's settle window is 90 frames (at 364 u/s, the opening 100 u yank takes ~1.5 s).
- `author_wrasse_assets.py --self-test` holds the bodies, with six negative controls.
- Wrasse Run's detection clamp is 3800, twice the kite's new worst world speed of ~1 860 u/s at a 2.4:1 screen's corner (`WRASSERUN.md` §5).

## Round 8: the hand line and the Grouper's corner (Garrett, 2026-10-20)

| Asked | In the game |
|---|---|
| "pick a nice horizontal line just above the ability totems and don't let the cursor travel below that line" | The **hand line** sits `handLineGap` (1.2% of the screen) above the top of the HUD's ability cards. `AbilityLockupView.TryGetCardsScreenRect` gives the cards' screen rect, read live, so the line follows the HUD's layout and the screen's shape. With no cards it sits at `handLineFallback` (20%). While the human navigates, the hand (a thumb, the cursor, the stick's point) is clamped to the line or above (`KiteNavigator.TryHandFloor`). A full stick lean now spans the screen above the line, not the whole screen. |
| "but allow the wrasse to extend across that line and off the screen" | (Superseded by round 8b, below: the kite may now pass every edge, held only by the cord.) The first cut ran the bottom edge 40 u past the screen (`offscreenOverhang`). |
| "to the left of the totem fill the space below that line with the grouper's view and don't let that view go above that line" | While navigating, the pilot camera's rect runs from the screen's lower-left margin to the cards' left edge and up to the hand line, each less `viewMargin`. A tap or click that starts in it is still the swap's. With no cards to fit beside, it falls back to the old square. While piloting, the kite's small view is unchanged. |
| "The grouper can keep a larger representative view of the wrasse" | The pilot view's faint kite stays drawn even when the Wrasse model draws, at `pilotKiteScale` (2.5) × the kite's radius. |

Also fixed: `KiteNavigatorConfigTests` still asserted the board frame was `Eased` after round 7 set `Heading`, so it would have failed in the Editor. It now asserts `Heading` and pins the round-7 and round-8 screen numbers.

## Round 8b and 9: only the cord holds the Wrasse; four limes (Garrett, 2026-10-21)

| Asked | In the game |
|---|---|
| "the edges of the screen on the sides and top are still backwards with the mouse able to go past where it should and the wrasse not able to go where it should. the wrasse should only be limited by the tether. the mouse should be limited by a small margin significantly less than the cord length." | The screen's rectangle (`_s`) is no longer a wall for the kite. It stays the board's CONTENT (mote spawns, prism and rival queries, the AI's plans), and the kite flies `FlightSettings()`: that rectangle plus `kiteOffscreenReach` (240 u) on every side. The cord holds the kite; only a long fling meets the far wall. Round 8's bottom-only `offscreenOverhang` is gone. While the human navigates, the hand is clamped to `TryHandRect`: `handEdgeMargin` (10 u, under a quarter of the 42 u cord) inside the screen's sides and top, with the hand line as its bottom. A stick spans that whole rectangle, the way the mouse does. Wrasse Run's clamp is 5200, sized to the kite's far corner (~890 u out). |
| "make all four of the lasso elements different hues, saturations and values within the lime green color family" ... "the largest tetrahedron within that space" | Each element's motes (kite view) and faint dots (pilot view) wear their own lime, `KiteNavigatorConfig.elementColors`. The first cut was Charge `#ffff00`, Mass `#a1c417`, Space `#00ff00`, Time `#c0fb93`; round 9b replaced it. They are derived and checked by `Tools/Build/kite_mote_colors.py`; the boundary, the tetrahedron and the assignment are in `Docs/VesselModels/COLOR.md` §5b. The tail and the saved-boost dots keep the palette's lime. The Wrasse Lasso Lab draws the same four (`LIME_E`). |

### Round 9b: green-yellow only (Garrett, 2026-10-21)

"pull time [and] space away from jade and charge away from gold" - "it has to have green in the yellow and yellow in the
green. it can not have any blue or orange at all." The boundary keeps only red + green: blue 0, green leading, and red a
quarter to three quarters of green (an HSV hue of 75–105°). The four are now:

| Element | Colour | Character |
|---|---|---|
| Charge | `#a0ff00` | bright yellow-green |
| Mass | `#5ec600` | the darkest, a deep green |
| Space | `#40ff00` | the greenest, with a quarter of yellow |
| Time | `#9bcf00` | the yellowest, a mid-value chartreuse |

All four are 0.02 apart (one just-noticeable difference) in value, saturation and hue, the most the blue-free sheet
allows. `Docs/VesselModels/COLOR.md` §5b has the numbers.

## The phone boost (Garrett, 2026-10-21)

"The grouper can not use boost on mobile. Fix this oversight and others."

- **Why.** The seat read `InputStatus.RightTriggerAnalog`, which only a pad and the keyboard write. A phone was left
  with "both thumbs off" (the touch strategy's `Idle`), which let go of the steering for every boost and was named on
  no card, so a phone pilot had no boost to find.
- **Now RT is an input binding, as on every other hull.** `Wrasse.prefab` binds `RightStickAction` to `CrewTriggerRT`
  (`CrewTriggerActionSO`, trigger 0), which calls `KiteNavigator.SetTrigger`. A pad's RT, right Shift and a phone's
  left-thumb lift all raise `RightStickAction` (a lift raises `OnlyRightStickAction` at once and `RightStickAction` for
  the thumb that flies on, the pair a pad raises), so it is one path through `R_VesselActionHandler` on every device.
  Holding chains boosts, with the cooldown between them, as a held trigger does. Both thumbs off no longer boosts (a
  released pad does not either).
- **The card names it.** The Wrasse map's Time card has `Input: 1` (RT), so a pad shows RT on it and a phone the
  lift-left-thumb glyph (`TouchControlProfiles`' new Wrasse row, ThumbLifts). The other three cards stay passive.
- **A held trigger is let go on a seat change**: the handler drops every release while the AI flies the hull, so
  `SetHumanNavigates` clears it.
- **The same gap on the Orca's helm** (boost on RT, shield on LT, both read from the analog triggers) is closed the same
  way: `ORCA_CREW.md`.
- **Known:** lifting the left thumb to double-tap the small view starts a boost if one is saved. Outside a race the AI,
  which takes the hull on that swap, spends saved boosts at once anyway.
- **Held to it:** `author_wrasse_assets.py --check` (the binding, the card input, the shared trigger assets; the
  self-test plants an unbound RT, an extra button, RT on the LT asset and a Time card naming nothing) and
  `author_touch_profiles.py --check` (a card that shows a lift must have that lift bound).

## Round 10: the juice, and a steady small view (Garrett, 2026-10-21)

"when playing a the wrasse the pip view of the grouper sometimes resizes into and out of its designated region." The
box came from the totem cards' live screen rect. That rect moved with every card press or upgrade tween, and dropped to
the fallback square on any frame the cards could not be read.
- `AbilityLockupView.TryGetCardsScreenRect` now measures each card AT REST, leaving out its own scale and rotation.
- `KiteNavigatorView.LatchCards` takes a new measurement only once it has held for 0.5 s (or the screen changed), and a
  failed read keeps the last good layout.

"lets loose as many of the flat ring effects as we can and juice up each effect. when more than one is lassoed it should
fizzle with no ring. space symbols should explode with explosion. mass symbols should look like they are morphing into
the 3d mass. time should morph on to the grouper. charge should morph onto the wrasse. this should be the juice. it
should be bespoke, unique, dynamic, and polished."

Every flat ring is gone: the Space blast ring, the pop, Mass, Time, Charge and steal rings, and the fizzle ring.
- **The lasso:** it cinches shut on what it caught: its outline is drawn in to the centre and thins over 0.4 s. A fizzle
  has no outline at all.
- **The motes become the effect:** the seat raises a `KiteBurst` (pooled, no allocation per pop) carrying the lassoed
  motes. `KiteNavigatorJuice` spawns each mote's five petals exactly where its flower drew them, then:

| Effect | The juice |
|---|---|
| Fizzle (more than one element) | The petals sputter in place, drain to grey, wilt and droop down the view, flickering out (`fizzleSeconds` 0.95). |
| Space | The petals spiral into the centre through the blast's 0.2 s fuse. As the real AOEExplosion fires, a white-hot five-petal star flares at the core, and 18 white shards and the petals leave on the evenly spread spokes of one radial burst. They ride the shock front (plus a kick out of the detonation), streaking and cooling back to Space's lime as they fade (`spaceRideSeconds` 1.1). |
| Mass | Each petal folds into its mote as a lit block appears. Each block (one per prism, up to 64) then rises out of the plane on an arc, grows to the prism's (2, 2, 5) with an overshoot, turns into the prism's attitude and lands in its exact place, flashing white. The seat lays the real prisms as the blocks arrive (`massMorphSeconds` 0.75, which is now also the Mass loop's delay). |
| Time | The petals wait in place for a stagger, then spiral onto the Grouper as accelerating streaks, lifting a little out of the plane so they read from the chase camera. They land on points over its hull as glinting chips that flash and fade (`timeFlightSeconds` 0.7). The saved-boost dots on the board show when they land. |
| Charge | The petals home onto the Wrasse on curved paths, swing into orbit at the charge pips' radius, and warm from lime to the pips' gold as they settle (`chargeFlightSeconds` 0.55). The pips and the kite's gold show when they arrive. |
| Pop | A popped prism bursts into six white shards that streak out and fade. |
| Steal / creature struck | Gold shards stream from the rival into the Wrasse. |
| Omni drop | Each new cluster blooms open, its petals bursting out unevenly and fading. |

How it draws: one vertex-coloured mesh per camera per frame, so every bit has its own colour, fade and flash. In the
kite view the petals lie flat just under the lens. In the pilot view they face the camera, and the Mass blocks are
shaded per face, so both read in 3D.

How it was checked:
- `KiteNavigatorJuice` reads a `KiteJuiceFrame` snapshot and `KiteJuiceSettings`, never the seat itself.
- `Tools/Build/kite_navigator_lab/juice_sheet.sh [out.png] [pilot]` compiles the shipped file outside Unity and renders
  every effect as a contact sheet, from above or from the chase camera. It fails on a non-finite vertex or an effect
  that draws nothing.
- `KiteNavigatorJuiceTests` (4 tests) check that every effect draws, stays finite and ends; that the Mass blocks are gone
  when the prisms take over; that a fizzle is petals only; and that the charge and boosts wait for their petals. Planted
  defects fail them.

## Seats: the swap is the arena's

The swap uses the arena's pilot-swap gesture (`PilotSwapGesture`: D-pad left / right, keys 1 / 2), with
`PilotSwap.CooldownSeconds` as its cooldown. It moves the local human between the hull and the kite. Since 2026-10-21
(Garrett: "players can select, change seats"), **keys pick** (1 the Grouper, 2 the Wrasse; picking the seat you are in
does nothing) and **the D-pad and the double tap step** to the other seat, as the Orca's keys 1-4 pick its seats. Every
pick is remembered on the device per hull class (`CrewSeatChoice`, PlayerPrefs). On the next bind the seat is taken once
the hull is the person's with live input (not parked on its AI, no countdown, modal or pause), so a respawn, a match or
the Vessel Changer starts them in the seat they chose. An AI squad mate always flies the empty seat:

- **The human navigates.** The hull's own `AIPilot` flies the hull (`IVessel.ToggleAIPilot(true)`). The human's flight
  input is paused (`InputController.SetPause(true)`), so the pointer and the sticks belong to the board.
- **The human flies.** `KiteNavigatorAI` flies the kite. It moves an aim point at a thumb's speed, with aim noise, and
  presses it. It never writes the kite (the input-only rule).
  - Each think, it rams if charged, else loops the fattest one-element cluster, else pops the nearest hostile prism.
  - The default skill is Hard (`aiSkill`).

## Two humans, two seats (plan, 2026-10-21)

Garrett, 2026-10-21: **"wrasse and grouper, are two seats. players can select, change seats etc. both can be played on
mobile or pc."** This answers `T-20261017-b06e`. It also corrects the audit line "one phone runs one human plus an AI;
two phones not supported": the target is one human per seat, on any mix of phones and PCs. The plan holds for every crew
vessel (the Goby and Pistol, the Orca's four seats, the Honeyguide and Ratel). It builds on `cece/sharp-tesla-j0kd7z`'s
`ICrewSeat`, `CrewSeat` and `CrewPictureInPicture`, and on the duo contract (`/duo-vessel` §2.1-2.2): a swap between
two humans is an offer both accept, and a human who leaves falls to the AI on the same frame.

**One human today, by device (read from the code on 2026-10-21; nothing editor-run):**

| | Keyboard + mouse | Gamepad | Phone |
|---|---|---|---|
| Pick / change seat | 1 Grouper, 2 Wrasse | D-pad steps | double tap the small view |
| Grouper | WASD + P ; L ' (`KeyboardInputStrategy`); right Shift boosts | two sticks; right trigger boosts | two thumbs (profile row: ThumbLifts); lift the left thumb to boost, the Time card shows its glyph |
| Wrasse | the mouse is the hand; a held click lets go | the left stick places the hand; a held trigger lets go | the first thumb is the hand; lifting it lets go |
| While on the Wrasse | flight input paused (`InputController.SetPause`), the hull's `AIPilot` flies | same | same; the paused touch strategy lets go of IdleAction |

The phone boost is the left-thumb lift since 2026-10-21 ("The phone boost", below); the both-thumbs-off boost this
paragraph used to describe is gone. The Wrasse's cards press nothing on a phone (the Time card's input is a thumb lift,
so `TouchControlsHud` makes it a glyph, not a button), so they claim no touches (`AbilityCardPress`), and the hand stops
at the line above them. The kite reads the raw primary touch (flight input is paused), so a touch that starts on a
control that owns it (`FlightTouchClaims`: a pressable card, the arena's swap tiles) is now skipped, as flight skips it.

**Who owns what:**
- **The hull's `NetworkObject` is owned by the machine of whoever holds the PILOT seat**: the human pilot's client, or
  the server for an AI pilot. `PilotSwap.TransferOwnershipServer` moves ownership today. Flight, trail, crystals,
  `IsBoosting` and the saved-boost bank stay there, so every existing owner gate is unchanged.
- **The navigator seat is simulated on the machine of whoever holds it**: the human navigator's client, else the hull's
  owner (today's solo and AI-crew rule). The board, motes, kite, tail and loop detection run there with no network hop,
  because the Sling's feel is a thumb's latency. Motes stay per-viewer and are never replicated (`/duo-vessel` §2.4).
- **The server owns occupancy.** A new `CrewSeatOccupancy` NetworkBehaviour on the Player prefab holds:
  - server-write `NetSeatHullId` (0 = none) and `NetSeatIndex`;
  - the owner-write kite pose, the three NetworkVariables of `WrasseCrewBodies` (flags, offset, rotation). They are used
    when a remote human navigates; the hull's own copies keep serving solo and AI crews.
- **The navigator's Player:** its `Player.Vessel` is the hull it rides (camera, HUD, `VesselVisionShading.LocalVessel`).
  Its `InputController` stays paused, and `IsLocalPilot` is false.
- **Audit first:** every system that assumes one Player per Vessel (`/duo-vessel` ANCHORS §3).
- **Rejected:**
  - a nested NetworkObject per seat: Netcode 2.13.3 allows it, but nothing here uses child ownership;
  - streaming the navigator's raw hand to the hull owner: every pull of the rope would wait a round trip.

**Which machine decides each effect.** Vessel rule 48: no consequence keyed on state only one machine has. Rule 52:
owner-decided state goes through a relay. §4.ac: an AOE is spawned on every peer. The navigator's machine decides, and
sends the decision as a `ServerRpc` on its `CrewSeatOccupancy`. Ownership is required (a player acts only for the seat
they hold), and the server checks the seat is theirs, then fans the decision out:

| Effect | Decided on | Applied |
|---|---|---|
| Pop (the head on a hostile prism) | navigator | `ClientRpc` to every peer with the head's swept segments and radius, batched per frame. Each peer damages its own prisms along them (prism destruction never replicates) |
| Space blast | navigator | `ClientRpc` with the centre, n and the L5 flag. Each peer spawns the `AOEExplosion`; its hits on networked objects no-op off the server |
| Mass cluster | navigator | `ClientRpc` with the centre, n, a seed and the shield flag. Each peer lays the same cluster (`FireMass` takes the seed, not `_rng`) |
| Saved boosts (Time) | navigator | a targeted `ClientRpc` to the hull owner, which banks and spends them. The count is replicated (owner-write, on the hull) for the navigator's HUD |
| Charge and steals | navigator (it holds the charge and detects the ram) | a ram request to the hull owner, which runs today's gate as the attacker's owner: `ElementalTransfer.IsDecidedHere`, `CombatHitDrain.TryAdmit`, `ApplyAuthoritative`, `OnCombatHitLanded` |
| Creature struck | navigator | `ServerRpc`; the server calls `Fauna.Jousted` (sim authority) |
| Race rings | the hull owner, as `GateRaceController` decides today | the kite probe answers from the replicated pose and epoch on a machine that does not simulate the seat |
| Omni crystal | the crystal's owner, as today | forwarded to the navigator's machine, which drops the motes |

**A gap in today's solo online play:** pops, blasts and Mass clusters happen only on the Wrasse's own machine, so the
other peers' prisms differ. Step 1 closes this before a second human sits down.

**Seats with two humans:**
- **A pick is a request.** A key, the D-pad, a double tap or a menu's seat card sends `RequestSeat(hull, seat)` to the
  server:
  - an empty or AI-held seat is granted at once, and the AI steps out;
  - a seat another human holds becomes an OFFER to them. They accept with the same gesture within a few seconds, and
    then both move or neither does. A swap is never forced.
  - When the pilot seat changes hands, the server moves the hull's ownership.
- **Leaving:** a human who leaves, disconnects or changes hull vacates the seat on the same frame. The AI takes it: the AI
  pilot on the server, or the AI navigator on the hull owner.
- **The remembered seat:** `CrewSeatChoice` is what a person ASKS for at spawn; the server places them.

**Phone vs PC:** no difference in authority, occupancy or offers. Only the input mapping differs (the table above), and
every effect crosses the network as a decision, never as device input.

**The arena swap conflict:**
- **The conflict:** on a pad or keyboard, `InputController` sends 1 / 2 and the D-pad to `Player.RequestPilotSwap` while
  the human pilots, and the seat reads the same keys. In an arena match, one press moves seats AND asks for a teammate's
  hull.
- **No conflict on a phone:** the arena's swap is the top-bar tiles (`PilotSwapIndicator`); the seat's is the double tap.
- **Resolution: one owner for the gesture.** On a crew hull the seat consumes it:
  - keys pick a seat on your own hull;
  - the D-pad steps through your hull's seats, then on round the team's ring. This answers the duo skill's open "ring"
    question: seats first, then teammates.
  - `InputController` asks `CrewSeat.TryConsumeSwap(dir)` and calls the arena only when the seat passes the step on.

**Work order:**
1. **Prism parity for one human online.** `KiteNavigator.cs`, with the effects split into `KiteNavigatorEffects.cs`
   (Follow-ups), and `WrasseCrewBodies.cs`, which gets the hull owner's pop, blast and Mass-seed RPCs. A seeded-cluster
   test. Check in MPPM: two players, one flies the Wrasse, and both see the same prisms go.
2. **Land `cece/sharp-tesla-j0kd7z`** (`T-20261017-efca`): the seat on `ICrewSeat`; `CrewSeat.PollSwap` returns the
   picked seat (keys pick, the D-pad steps, as here and on the Orca); the double tap into `CrewPictureInPicture`;
   `CrewSeatChoice` into `CrewSeat`.
3. **Occupancy:** `CrewSeatOccupancy` on the Player prefab (its Netcode hash changes: check the in-flight branches
   first); `Player.cs` `RequestSeat_ServerRpc`; `PilotSwap.cs` `TryValidateSeatServer` (same team, a crew hull, the seat
   free, AI-held or offered); `ICrewSeat` gains `SeatCount`, `Occupant(i)` and `IsSimulatedHere`.
4. **Authority:** `KiteNavigator.ShouldRun` becomes "this machine simulates the navigator seat"; effects go through the
   occupant, boosts and rams to the hull owner; `WrasseCrewBodies` reads the occupant's pose.
5. **The gesture owner:** `InputController.cs` asks `CrewSeat.TryConsumeSwap`; `PilotSwapIndicator.cs` shows the other
   seat as a tile on a crew hull. Affects: the arena's swap.
6. **The pick in menus:** `VesselChangerToy.BuildOptions` offers one station per seat on a class with `CrewNames`
   ("Grouper", "Wrasse"; Apply = `SelectVessel` plus `CrewSeatChoice.Set`). Two party members taking one crew hull is the
   generic "two players, one seat" piece (`PartySessionService`, the squad card).
7. **Hand tests** (MPPM with two players, then a phone and a PC): each human picks a seat and an offer swaps them; a
   leaver's seat falls to the AI; pops, blasts and clusters match on both screens; saved boosts reach the pilot; a ram
   steals once.

## Files

| File | What |
|---|---|
| `KiteNavigatorCore.cs` | The kite: move, tail, loop detection, loop resolution, growth, blast radius. Engine-free; ported line for line from the lab's pure functions. |
| `KiteNavigatorAI.cs` | The AI navigator (the lab's `navAI` / `planLoop`). Engine-free. |
| `KiteNavigatorConfigSO.cs` | Every number; each tooltip names its lab key. Asset: `Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset`. |
| `KiteNavigator.cs` | The seat on the hull: board frame, motes, prism and rival shadows, thumb, head contacts, effects, swap. |
| `CrewSeatChoice.cs` | The seat a person picked on a crew hull, remembered on the device per hull class (2026-10-21). |
| `Assets/_Prefabs/Spacevessels/Wrasse.prefab`, `Tools/Build/author_wrasse_assets.py` | The Wrasse hull (the Sparrow plus this seat) and the tool that authors and registers it. |
| `WrasseCrewBodies.cs` | The two bodies (round 7): places, swims and replicates the Wrasse model where the kite flies; puppets the Grouper from the hull's measured motion. Finds both models and their bones by name. |
| `KiteNavigatorJuice.cs`, `KiteBurst.cs` | The juice (round 10): every loop, pop, steal and Omni drop as a `KiteBurst`, its motes' petals choreographed per effect. Rendered offline by `Tools/Build/kite_navigator_lab/juice_sheet.sh`. |
| `KiteNavigatorView.cs` | The two views: the kite camera, the picture-in-picture swap, every kite, tail, mote and ring draw (the game's renderer, `Graphics.RenderMeshInstanced`). |
| `UI/Controller/WrasseHUDController.cs` | The four ability cards read the seat: saved boosts, charges, the last blast, the Mass flash. |
| `Utility/PrismDepthPlane.cs`, `_Graphics/Materials/Graphs/PrismDepthPlane.hlsl` | Prism depth from a plane, for one camera's render (the kite view). |
| `ImpactEffects/EffectsSO/Vessel Crystal Effects/KiteOmniCrystalEffectSO.cs` | An Omni crystal drops a burst of motes on the board (asset `KiteOmniCrystalEffect`, in `WrasseImpactorDataContainer`). |
| `Assets/_Scripts/Editor/Studios/KiteNavigatorHarnessMenu.cs` | **FrogletTools > Vessels > Kite Navigator > Seat On Local Hull (Play mode)** and **Remove**. |
| `Assets/_Scripts/Tests/Editor/KiteNavigatorCoreTests.cs`, `KiteNavigatorConfigTests.cs` | The core's rules, and the asset's numbers and wiring. |
| `Tools/Build/kite_navigator_lab/parity.cjs` | Compiles the shipped C# core and diffs it against the lab page frame by frame, over 6 scripted thumbs. Also diffs the asset against the page key by key. Planted controls must be caught. |

## Try it

**In the Toybox** (how every lab port is tried: Garrett, 2026-10-17):

1. Open the Toybox (menu freestyle) and fly into the **Vessel Changer**. Fly the **Wrasse and Grouper** station.
2. You fly the hull, a Sparrow body, with two thumbs (both sticks, or the keyboard) and no buttons. The kite view is a
   small square in the lower left, with the AI flying the kite. Pull the right trigger (right Shift; on a phone lift the
   left thumb and keep flying with the right) to spend a boost a Time loop saved. The Time card counts them, the Charge card counts the kite's rams, the
   Space card counts what the last blast destroyed, and the Mass card flashes when a cluster lands.
3. Press **2**, or D-pad right, to take the kite. The kite view fills the screen and your hull's view shrinks to the
   same small square in the lower left. Move the mouse or lean the stick; the kite follows. Loop motes of one petal shape.
4. Press **1**, or D-pad left, to take the hull back.

The same seat can ride any other hull in a match: **FrogletTools > Vessels > Kite Navigator > Seat On Local Hull
(Play mode)**.

## The Wrasse hull

**The name** (Garrett, 2026-10-18: "lets give this vessel a good name based on a mutualistic pair"): the **cleaner
wrasse** and the big fish it keeps. The wrasse darts round its host picking parasites off, and the host gets cleaned
while the wrasse gets fed. On this hull, the navigator's kite darts round the hull popping the enemy prisms around it,
and what its lasso gathers feeds the pilot (blasts, clusters, saved boosts, rams). The hull was called the Kite until
then. The hull-level names moved with it: `VesselClassType.Wrasse` (still 19), `Wrasse.prefab`, `SO_Class_Wrasse`,
`ElementalAbilityMaps/Wrasse`, `WrasseImpactorDataContainer`, `WrasseHUDController` and
`Tools/Build/author_wrasse_assets.py`. Every `.meta` guid was kept, so no reference changed. The SEAT keeps its name:
the navigator still flies a kite, so `KiteNavigator*`, `KiteOmniCrystalEffect`, this doc and the lab's "Kite & Tail"
are unchanged.

**Each subvessel has its own name** (Garrett, 2026-10-18: "so its card will read wrasse and ___. pick a fish that the
wrasse cleans"). The navigator's kite is the **Wrasse**; the pilot's hull is the **Grouper**, the reef fish cleaner
wrasses are best known for cleaning. `SO_Vessel` now has `CrewNames` (Wrasse, Grouper) and `CrewNameTemplate`
(`{0} and {1}`), and `SO_Vessel.CardName` joins them, so every card reads **Wrasse and Grouper**:
- the Toybox Vessel Changer's station;
- the hangar grid card, the detail view and the abilities view;
- the arcade launch panel, the vessel info popup, the squad card and the Maelstrom pool row.

`Name` stays "Wrasse": it is the vessel's key, and the hangar's cloud unlocks are keyed by it. A one-seat vessel leaves
`CrewNames` empty and its card reads `Name`. The Goby & Pistol duo (`cece/sharp-tesla-j0kd7z`) can set its own
`CrewNames` when it lands.

The Vessel Changer reads class assets through its definition's `vesselArt` list. That list comes from the in-flight
Toybox branch `cece/determined-goodall-o1klfo` (the picture of each vessel); this branch carries the same three hunks,
byte for byte, so the two merge cleanly.

The navigator is a seat, not a hull, and the Vessel Changer offers hulls. So the seat ships on its own hull class:
`VesselClassType.Wrasse = 19`.

- **The hull:** `Wrasse.prefab` is a clone of `Sparrow.prefab` with `KiteNavigator` on its root, wired to
  `KiteNavigatorConfig.asset`. `Tools/Build/author_wrasse_assets.py` authors and registers it; it is the duo branch's
  `author_goby_assets.py` cloned for the Sparrow.
- **Its registrations:**
  - `SO_Class_Wrasse`, owned from the start, in both class lists;
  - the Vessel Prefab Container and `DefaultNetworkPrefabs`;
  - `ElementalAbilityMaps/Wrasse`, the Sparrow's map with the Wrasse's own cards, every input 0 (round 4d);
  - four `CrystalHullFusionConfig` entries, the Sparrow's;
  - `ToyVesselRoster.Default` and `EnumIntegrityTests` in code.
- **The checks:** `--check` validates the shipped set and `--self-test` proves each check fires.
- **The id:** 16 is BrittleStar on bleeding-edge (and the Goby on `cece/sharp-tesla-j0kd7z`), and 17 is the Angler
  (`cece/zealous-feynman-po8k8d`). 18 is left for the Goby, which must move off 16.

**The seat only wakes on the local pilot's own hull while a human flies it**, the same rule `GobyPistolDuo` follows:
- its transform must be `VesselVisionShading.LocalVessel`;
- the autopilot must be off, unless this seat started it.

That covers the Vessel Changer's swap-in, where the new hull arrives on autopilot until the changer hands control
back, as well as any menu that parks the hull on its AI. On an AI hull or a remote copy, the component sits idle.

## Where the game differs from the lab

- **Round 4 is not in the lab yet**: the views, the two-thumb pilot, the saved boosts, the HUD counts, the five-petal
  motes, the element weights, the L5 upgrades, the Omni burst and the fauna rams were directed in Unity (above; task
  `T-20261018-3247`).
- **While the AI flies the hull, it spends the Time gift as it comes** (it has no trigger).
- **The Space blast is the real `AOEExplosion`.** Through its `ExplosionImpactor` it can also hit rival hulls, which
  the lab's blast never did.
- **The AI's prism lead uses the hull's velocity.** A prism's drift across the board comes from the hull's velocity and
  ignores the board's own turn. The lab differenced each prism's shadow.
- **The kite view always shows**, even while the human flies, so the pilot can watch the AI work. It takes the pointer only
  while the human navigates.
- **The kite view sees 400 u either side of the plane**, depth measured from the plane and the mass above it dithered; the
  lab drew prisms beyond the slice as faint ghosts.
- **The Omni burst comes only on the Wrasse hull**: the play-mode menu's seat on another hull has no Omni effect in its
  container.

## Gates

```
node Tools/Build/kite_navigator_lab/parity.cjs        # core vs page, asset vs page, planted controls
bash Tools/Build/unity_refcompile/run.sh              # the player compile
python3 Tools/Build/author_wrasse_assets.py --check     # the hull: no Sparrow kit, two sticks, the Wrasse HUD, the map, both bodies
node Tools/Build/kite_navigator_lab/sling_parity.cjs  # the Sling vs the Wrasse Lasso Lab, asset vs page
bash Tools/Build/kite_navigator_lab/juice_sheet.sh out.png [pilot]   # every effect's juice, rendered; fails on NaN or an empty effect
python3 Tools/Build/vessel_model/build.py grouper --check && python3 Tools/Build/vessel_model/build.py wrassekite --check
python3 Tools/Shaders/verify_swarm_member_pose.py     # the swarm members' spread with the depth-plane include
```

## Follow-ups

- **The Sling is not in the Crew Vessel Lab** (it came from the Wrasse Lasso Lab, which models the board alone). The
  thumb-travel and loop numbers there are the lab's AI; a phone playtest decides the feel (`T-20261020-bbc3` closed
  with the pick, playtest task on the board).
- **An AI crew's kite runs where its hull is simulated** (the host): its loops' effects are the host's, as a human
  crew's are its own machine's (the seat's networking follow-up).

- **Other runtime `Shader.Find("Universal Render Pipeline/Unlit")` calls** (ToyFactory, ToyModelBuilder, AstroLeagueArena, AstroLeagueBall, BlackHole, ThresherExecutor, SniperBeam, BrittleStarExecutor, ShrikeExecutor, GrizzlyClawConeVisual, ArcLightningFallback, SegmentSpawnerPreview: 12, counted 2026-10-19) carry
  the same build-stripping risk; each should get a Resources material for the variant it uses (T-20261019-41bb).
- **`cece/sharp-tesla-j0kd7z`** moves the swap into `CrewSeat.PollSwap` and the small view into `CrewPictureInPicture`:
  carry the double-tap and the Resources material there when it merges.

- **Networking.** The Wrasse's BODY is replicated (round 7, `WrasseCrewBodies`). Everything else still runs on the
  machine that flies the hull: the board, the motes, the AI and the effects. Decided 2026-10-21 (two human seats, either
  device); the plan and its work order are in "Two humans, two seats" above.
- **The Grouper still wears the Sparrow's colliders, jets and engine flare** (round 7). The hit box is the Sparrow's
  flat 10.3 × 0.46 × 4.4 box, but the Grouper is ~5.6 u tall. The jets ride the Sparrow's bones. `FlareEngine`
  flashes the hidden Sparrow's slot 3, not the Grouper's Engine slot. `CrystalHullFusionConfig`'s Wrasse bakes are the
  Sparrow mesh's, so a collected crystal's faces land on the Sparrow's surface inside the Grouper. Each needs an Editor
  pass: a collider sized to `Grouper.md`, jets re-homed to the engine mouths (±0.78, −0.30, −2.62), the flare pointed at
  the Grouper's renderer, and a re-bake.
- **The Wrasse body's puppetry signs are first-cut.** Jaw / dorsal open on +pitch and the tail bends out of a turn by
  reasoning from Unity's axes, not from a flight. Each is a signed inspector number on `WrasseCrewBodies`.
- **Arena swap.** In an arena match, the swap gesture also asks the arena to swap hulls while the human flies.
  `InputController` polls it, and is paused only while the human navigates. The resolution (the seat consumes the
  gesture; the D-pad steps through the seats, then the team's ring) is in "Two humans, two seats" above.
- **Duo vessels.** When the duo-vessel system lands (branch `cece/sharp-tesla-j0kd7z`, skill `/duo-vessel`), fold the
  Wrasse into it as a duo design. It already follows the same shape: one hull class, the seat on the root, the
  arena's swap.
- **Sounds.** All eight `EventReference` slots ship empty: loop closed, fizzle, pop, Mass, Time, Charge, steal, creature.
- **The HUD is the Sparrow's view under a Wrasse controller** (inconsistency, a fix). `WrasseHUDController` drives
  `SparrowHUDVariant` and re-enables the Charge and Space icons that `SparrowHUDView.Initialize` hides. The icons are
  the Sparrow's art. A Wrasse HUD prefab variant (Create ▸ Prefab Variant, never a copy) with the kite's four icons
  removes both (`T-20261018-8ff7`).
- **The Vessel Changer's `vesselArt` list is carried twice** (a merge risk, not debt). `SO_VesselList`'s probe form,
  `VesselChangerToyDefinitionSO.vesselArt` and the `Toy_VesselChanger.asset` wiring are byte-identical copies of the
  in-flight `cece/determined-goodall-o1klfo` (the Toy Box's vessel pictures). Whichever lands second should merge
  clean; if that branch edits those three hunks first, take its version.
- **A station label falls back to the enum's name** (the legacy literal shape). `VesselChangerToy.LabelOf` shows
  `SO_Vessel.CardName` and falls back to `VesselClassType.ToString()` when the definition has no `vesselArt` list or
  the list misses a class. The shipped `Toy_VesselChanger.asset` wires `SO_Classlist_All`, which holds every rostered
  class, so the fallback does not show today.
- **`KiteNavigator.cs` is 1308 lines and owns seven jobs** (re-measured 2026-10-21, `wc -l`; it was 967 before rounds 6-10):
  board frame, motes, prism / rival / creature shadows, input, head contacts, loop effects (with L5s, the Omni burst
  and the bursts for the juice), and boosts. The effects and the boost bank are the natural split: they read the config
  and the hull, never the board. Measured: the "tail: loops and their effects" section runs lines 1011-1264 (254 of
  1308); the juice itself already lives apart (`KiteNavigatorJuice.cs`). Before splitting, list every field only those
  methods touch.
- **The plane depth reaches two shader paths**: `PrismFlightSqrDistance` (every BlockGraph prism) and the swarm
  members' spread. `ExplodingBlockGraph` (debris; constant distance) and `UnstablePrismGraph` (camera distance used
  only for its flicker) keep the camera's distance in the kite view, which is incompleteness, not a defect. The kite
  camera sees `viewDepth` (400 u) either side of the plane; mass beyond that is clipped.
- **The generated-asset audit's 8 findings on `Wrasse.prefab`** (stale `ShowTopMostFoldoutHeaderGroup` keys, the
  `R_VesselElementStatsHandler.cs` file/class name) are inherited from `Sparrow.prefab`, which carries the same ones.
