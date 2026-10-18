# Kite Navigator: the Kite & Tail crew seat

A crew seat for a second player on one hull. The design is the Crew Vessel Lab's round 3 candidate, which Garrett
signed off on 2026-10-17 (`Docs/Studios/CrewVesselLab.html`, write-up in `Docs/Studios/CREW_VESSEL_LAB.md`), then
**round 4 (2026-10-18), which Garrett directed in Unity after flying it**; see "Round 4" below. Round 4 is not in the
lab yet (task `T-20261018-3247`).

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
| Time | boost for the pilot | `1.5 × 2^(n−1)` s of `IsBoosting` (cap 30 s), spent on the pilot's trigger |
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
| Use the game's rendering for the kite | The kite view is a second camera looking straight down the hull's up axis onto its x-z plane, orthographic, clipped to the touchable slice (±reachY), so it draws the real world. The kite, tail, motes and rings are world meshes drawn only for it. The 2D UI board (`KiteBoardGraphic`) is gone. |
| Prisms out of the plane: unlit, not gone (round 4b) | The camera's clip still removes them from the lit render; a second, deeper prism query (`ghostDepth` 240 u, at most `ghostCap` 1500) draws each one back as an unlit box in its domain's tint × `ghostBrightness` (0.45), `ghostAlpha` (0.55), on the floor of the view under everything in the slice. |
| Every element lime, told apart by its petal shape | Motes are lime, each drawn in its element's petal outline from the HUD (`ElementPetalGeometry.Outline`: Mass a triangle, Space a kite, Time a diamond, Charge a five-sided gem). |
| The kite follows the mouse or thumbstick; no click | Mouse: the kite flies to the cursor. Stick: it flies the way the stick leans, as fast as it leans (circle the stick, the kite circles). Touch: to a point 21 mm above the thumb. The last-used device leads. |
| The pilot is a one-thumb flyer with no buttons | The Kite is the Sparrow's one-thumb hull (`SingleStickVesselTransformer`; listed in `OneThumbVesselCoverageTests`) with every ability binding removed. |
| The pilot controls the boost: the trigger on a gamepad (round 4b) | `InputStatus.RightTriggerAnalog` > 0.5: the pad's right trigger, or the left mouse button with the single-stick mouse. On a touch screen, with no trigger, lifting the thumb (the touch strategy's `Idle`). A boost runs while held and the Time gift lasts. (Round 4 boosted on a centred stick, which fired by itself whenever the pilot flew straight.) |
| The ship's petals set how often each element comes | weight = `moteBaseWeight` (1) + `moteWeightPerLevel` (1) × the hull's level in it. |
| Charge works on fauna | A charged head kills a creature it passes through (`Fauna.Jousted`, sim authority only), for `faunaChargeCost` (1). The AI rams creatures too. |
| Mass clusters shielded at Mass L5 | `prismProperties.IsShielded` before `Initialize`, as the Thresher's armoured trail. |
| Charge: knock crystals out, steal them at Charge L5 | The ram's form: Eject, then Steal at L5. |
| Space blasts destroy shielded and super-shielded prisms at Space L5 | Super shields inside the blast's reach are dropped first (`Prism.DeactivateShields`, the Rhino sword's sequence), and the blast goes out with `DevastatingOverride = true`. |
| The lasso 50% longer (round 4b) | `tailLength` 1.6 → 2.4 board radii (384 u); the lab's default moved with it (parity's asset check holds them equal). |
| An Omni crystal: 5 s of a longer lasso and a kite view zoomed out twice as far | `KiteOmniCrystalEffectSO` in the Kite's own `KiteImpactorDataContainer` (omni list) calls `KiteNavigator.OnOmniCrystal`: the board radius eases to ×2 (`omniBoardScale`) for `omniSeconds` (5). The camera zooms with it, the kite may fly twice as far, and the lasso, measured in board radii, doubles. |
| Boosts: 2 s cooldown, none at Time L5 | `boostCooldownSeconds` (2) after a boost ends; 0 at Time L5. |

**Round 4b (the same day, after flying round 4):** the petals could not be seen. Every instanced draw used
`Sprites/Default`, whose instanced path takes its colour from `unity_SpriteRendererColorArray`, which only a
SpriteRenderer fills, so it drew at zero alpha. Instanced draws now use URP/Unlit set up as a transparent, double-sided
surface (as GobyPistolDuo draws). The tail and the loops, single meshes whose vertices carry their fade, keep
`Sprites/Default` drawn without instancing.

Every "L5" is the fleet's gate, `ElementalAbilityHandler.IsUpgradeActive(element)`. The Kite's map lists all four
elements: each unlocks at level 5 and relocks below 4.

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
| `Assets/_Prefabs/Spacevessels/Kite.prefab`, `Tools/Build/author_kite_assets.py` | The Kite hull (the Sparrow plus this seat) and the tool that authors and registers it. |
| `KiteNavigatorView.cs` | The two views: the kite camera, the picture-in-picture swap, every kite, tail, mote and ring draw (the game's renderer, `Graphics.RenderMeshInstanced`). |
| `ImpactEffects/EffectsSO/Vessel Crystal Effects/KiteOmniCrystalEffectSO.cs` | An Omni crystal widens the board (asset `KiteOmniCrystalEffect`, in `KiteImpactorDataContainer`). |
| `Assets/_Scripts/Editor/Studios/KiteNavigatorHarnessMenu.cs` | **FrogletTools > Vessels > Kite Navigator > Seat On Local Hull (Play mode)** and **Remove**. |
| `Assets/_Scripts/Tests/Editor/KiteNavigatorCoreTests.cs`, `KiteNavigatorConfigTests.cs` | The core's rules, and the asset's numbers and wiring. |
| `Tools/Build/kite_navigator_lab/parity.cjs` | Compiles the shipped C# core and diffs it against the lab page frame by frame, over 6 scripted thumbs. Also diffs the asset against the page key by key. Planted controls must be caught. |

## Try it

**In the Toybox** (how every lab port is tried: Garrett, 2026-10-17):

1. Open the Toybox (menu freestyle) and fly into the **Vessel Changer**. Fly the **Kite** station.
2. You fly the hull, a Sparrow, with one thumb (the stick, the mouse or a touch) and no buttons. The kite view is a small
   square in the lower left, with the AI flying the kite. Hold the right trigger (left mouse button; lift your thumb on
   a phone) to spend the boost a Time loop gave you.
3. Press **2**, or D-pad right, to take the kite. The kite view fills the screen and your hull's view shrinks to the
   same small square in the lower left. Move the mouse or lean the stick; the kite follows. Loop motes of one petal shape.
4. Press **1**, or D-pad left, to take the hull back.

The same seat can ride any other hull in a match: **FrogletTools > Vessels > Kite Navigator > Seat On Local Hull
(Play mode)**.

## The Kite hull

The navigator is a seat, not a hull, and the Vessel Changer offers hulls. So the seat ships on its own hull class:
`VesselClassType.Kite = 19`.

- **The hull:** `Kite.prefab` is a clone of `Sparrow.prefab` with `KiteNavigator` on its root, wired to
  `KiteNavigatorConfig.asset`. `Tools/Build/author_kite_assets.py` authors and registers it; it is the duo branch's
  `author_goby_assets.py` cloned for the Sparrow.
- **Its registrations:**
  - `SO_Class_Kite`, owned from the start, in both class lists;
  - the Vessel Prefab Container and `DefaultNetworkPrefabs`;
  - `ElementalAbilityMaps/Kite`, the Sparrow's map;
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

- **Round 4 is not in the lab yet**: the views, the one-thumb pilot, the thumb-lift boost, the element weights, the L5
  upgrades, the Omni zoom and the fauna rams were directed in Unity (above).
- **While the AI flies the hull, it spends the Time gift as it comes** (it has no trigger).
- **The Space blast is the real `AOEExplosion`.** Through its `ExplosionImpactor` it can also hit rival hulls, which
  the lab's blast never did.
- **The AI's prism lead uses the hull's velocity.** A prism's drift across the board comes from the hull's velocity and
  ignores the board's own turn. The lab differenced each prism's shadow.
- **The kite view always shows**, even while the human flies, so the pilot can watch the AI work. It takes the pointer only
  while the human navigates.
- **The kite view lights only the slice the kite can touch** (±reachY); farther prisms are unlit boxes (the lab drew them
  as faint ghosts).
- **The Omni zoom comes only on the Kite hull**: the play-mode menu's seat on another hull has no Omni effect in its
  container.

## Gates

```
node Tools/Build/kite_navigator_lab/parity.cjs        # core vs page, asset vs page, planted controls
bash Tools/Build/unity_refcompile/run.sh              # the player compile
```

## Follow-ups

- **Networking.** Everything runs on the hull's local human's machine: the board, the motes, the AI and the effects.
  A second human on another machine needs the seat replicated, plus a decision on which machine decides the effects.
- **Arena swap.** In an arena match, the swap gesture also asks the arena to swap hulls while the human flies.
  `InputController` polls it, and is paused only while the human navigates. A crew hull in an arena needs one owner
  for the gesture.
- **Duo vessels.** When the duo-vessel system lands (branch `cece/sharp-tesla-j0kd7z`, skill `/duo-vessel`), fold the
  Kite into it as a duo design. It already follows the same shape: one hull class, the seat on the root, the
  arena's swap.
- **Sounds.** All eight `EventReference` slots ship empty: loop closed, fizzle, pop, Mass, Time, Charge, steal, creature.
- **The generated-asset audit's 8 findings on `Kite.prefab`** (stale `ShowTopMostFoldoutHeaderGroup` keys, the
  `R_VesselElementStatsHandler.cs` file/class name) are inherited from `Sparrow.prefab`, which carries the same ones.
