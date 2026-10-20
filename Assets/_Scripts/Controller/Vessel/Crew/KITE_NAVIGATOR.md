# Kite Navigator: the Kite & Tail crew seat (on the Wrasse)

A crew seat for a second player on one hull. It ships on the **Wrasse** (`VesselClassType.Wrasse = 19`, card **Wrasse
and Grouper**): the navigator's kite is the Wrasse, the pilot's hull the Grouper. The design is the Crew Vessel Lab's
round 3 candidate, which Garrett signed off on 2026-10-17 (`Docs/Studios/CrewVesselLab.html`, write-up in
`Docs/Studios/CREW_VESSEL_LAB.md`), then **rounds 4 to 4f (2026-10-18), which Garrett directed in Unity after flying
it**; see "Round 4" below. The rounds after 3 are not in the lab yet (task `T-20261018-3247`).

**Where it stands (after round 6, 2026-10-20)**, in one place; the rounds below say why each part is the way it is:
- **The Grouper (pilot):** the Sparrow's hull with its guns, missiles and buttons removed, flown on two sticks
  (`VesselTransformer`; pitch / yaw 100, roll 120). The right trigger (right Shift; both thumbs lifted on a phone)
  spends a boost the Wrasse saved.
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
- **Unverified:** nothing has been played in the editor yet (`Docs/UNITY_VERIFICATION_CHECKLIST.md`, task
  `T-20261018-5c29`), and the seat runs on one machine (`T-20261017-b06e`).

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
| The pilot controls the boost: the trigger on a gamepad (round 4b) | `InputStatus.RightTriggerAnalog` > 0.5: the pad's right trigger, or right Shift on the keyboard. On a touch screen, with no trigger, lifting both thumbs (the touch strategy's `Idle`). Round 4d made the Time gift discrete saved boosts (below). (Round 4 boosted on a centred stick, which fired by itself whenever the pilot flew straight.) |
| The ship's petals set how often each element comes | weight = `moteBaseWeight` (1) + `moteWeightPerLevel` (1) × the hull's level in it. |
| Charge works on fauna | A charged head kills a creature it passes through (`Fauna.Jousted`, sim authority only), for `faunaChargeCost` (1). The AI rams creatures too. |
| Mass clusters shielded at Mass L5 | `prismProperties.IsShielded` before `Initialize`, as the Thresher's armoured trail. |
| Charge: knock crystals out, steal them at Charge L5 | The ram's form: Eject, then Steal at L5. |
| Space blasts destroy shielded and super-shielded prisms at Space L5 | Super shields inside the blast's reach are dropped first (`Prism.DeactivateShields`, the Rhino sword's sequence), and the blast goes out with `DevastatingOverride = true`. |
| The lasso 50% longer (round 4b) | `tailLength` 1.6 → 2.4 board radii (384 u); the lab's default moved with it (parity's asset check holds them equal). |
| An Omni crystal: 5 s of a longer lasso and a kite view zoomed out twice as far | Replaced in round 4e by a burst of motes (below). |
| Boosts: 2 s cooldown, none at Time L5 | `boostCooldownSeconds` (2) after a boost ends; 0 at Time L5. |

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

## Seats: the swap is the arena's

The swap uses the arena's pilot-swap gesture (`PilotSwapGesture`: D-pad left / right, keys 1 / 2), with
`PilotSwap.CooldownSeconds` as its cooldown. It moves the local human between the hull and the kite. An AI squad mate
always flies the empty seat:

- **The human navigates.** The hull's own `AIPilot` flies the hull (`IVessel.ToggleAIPilot(true)`). The human's flight
  input is paused (`InputController.SetPause(true)`), so the pointer and the sticks belong to the board.
- **The human flies.** `KiteNavigatorAI` flies the kite. It moves an aim point at a thumb's speed, with aim noise, and
  presses it. It never writes the kite (the input-only rule).
  - Each think, it rams if charged, else loops the fattest one-element cluster, else pops the nearest hostile prism.
  - The default skill is Hard (`aiSkill`).

## Files

| File | What |
|---|---|
| `KiteNavigatorCore.cs` | The kite: move, tail, loop detection, loop resolution, growth, blast radius. Engine-free; ported line for line from the lab's pure functions. |
| `KiteNavigatorAI.cs` | The AI navigator (the lab's `navAI` / `planLoop`). Engine-free. |
| `KiteNavigatorConfigSO.cs` | Every number; each tooltip names its lab key. Asset: `Assets/_SO_Assets/VesselActions/KiteNavigator/KiteNavigatorConfig.asset`. |
| `KiteNavigator.cs` | The seat on the hull: board frame, motes, prism and rival shadows, thumb, head contacts, effects, swap. |
| `Assets/_Prefabs/Spacevessels/Wrasse.prefab`, `Tools/Build/author_wrasse_assets.py` | The Wrasse hull (the Sparrow plus this seat) and the tool that authors and registers it. |
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
   small square in the lower left, with the AI flying the kite. Pull the right trigger (right Shift; lift both thumbs on a
   phone) to spend a boost a Time loop saved. The Time card counts them, the Charge card counts the kite's rams, the
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
python3 Tools/Build/author_wrasse_assets.py --check     # the hull: no Sparrow kit, two sticks, the Wrasse HUD, the map
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

- **Networking.** Everything runs on the hull's local human's machine: the board, the motes, the AI and the effects.
  A second human on another machine needs the seat replicated, plus a decision on which machine decides the effects.
- **Arena swap.** In an arena match, the swap gesture also asks the arena to swap hulls while the human flies.
  `InputController` polls it, and is paused only while the human navigates. A crew hull in an arena needs one owner
  for the gesture.
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
- **`KiteNavigator.cs` is 967 lines and owns seven jobs**: board frame, motes, prism / rival / creature shadows, input,
  head contacts, loop effects (with L5s and the Omni burst), and boosts. The effects and the boost bank are the natural
  split: they read the config and the hull, never the board. Measured: the "tail: loops and their effects"
  section runs lines 709-941 (233 of 967). Before splitting, list every field only those methods touch.
- **The plane depth reaches two shader paths**: `PrismFlightSqrDistance` (every BlockGraph prism) and the swarm
  members' spread. `ExplodingBlockGraph` (debris; constant distance) and `UnstablePrismGraph` (camera distance used
  only for its flicker) keep the camera's distance in the kite view, which is incompleteness, not a defect. The kite
  camera sees `viewDepth` (400 u) either side of the plane; mass beyond that is clipped.
- **The generated-asset audit's 8 findings on `Wrasse.prefab`** (stale `ShowTopMostFoldoutHeaderGroup` keys, the
  `R_VesselElementStatsHandler.cs` file/class name) are inherited from `Sparrow.prefab`, which carries the same ones.
